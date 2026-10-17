using System;
using System.Collections.Generic;
using UnityEngine;

namespace CuStatsHud.Core
{
    /// <summary>
    /// 把游戏对象读成 <see cref="BodySnapshot"/>。
    ///
    /// 全部字段都是对 Assembly-CSharp 的直接访问（编译期校验），
    /// 只有联机玩家列表走反射（见 <see cref="MpBridge"/>）。
    /// 快照实例被复用，逐条 push 到 List 中，刷新不再产生大量小对象。
    /// </summary>
    internal sealed class BodyReader
    {
        private const float ScanCacheSeconds = 2f;

        private readonly MpBridge _mp;
        private readonly List<MpBridge.Entry> _players = new List<MpBridge.Entry>(16);

        /// <summary>队友详情快照池（复用实例，避免每轮为每名队友新建对象）。</summary>
        private readonly List<BodySnapshot> _mateDetails = new List<BodySnapshot>(8);
        private int _mateDetailUsed;

        private Body[] _sceneBodies;
        private float _sceneScanAt = -999f;

        /// <summary>是否始终列出次要指标（由配置 Monitor/ShowNormalSecondary 决定）。</summary>
        private bool _showAllSecondary;

        /// <summary>最近一次成功解析出的本地玩家 Body（供波形采样使用）。</summary>
        public Body LocalBody { get; private set; }

        public BodyReader(MpBridge mp)
        {
            _mp = mp;
        }

        /// <summary>采集一轮数据。返回是否成功读到本地玩家。</summary>
        public bool Read(BodySnapshot s, bool includeMates, bool showAllSecondary)
        {
            s.Clear();
            s.SampledAt = Time.unscaledTime;
            _showAllSecondary = showAllSecondary;

            _mp.EnsureInitialized();

            Body body = ResolveLocalBody();
            LocalBody = body;

            if (body == null)
            {
                s.Error = "未找到本地玩家（主菜单或存档尚未载入）";
                return false;
            }

            s.Valid = true;
            s.InMultiplayer = _mp.Available;
            s.PlayerName = _mp.GetLocalPlayerName();

            ReadFlags(s, body);
            BuildVitals(s, body);
            BuildLimbs(s, body);
            if (includeMates) BuildMates(s, body);

            s.Overall = AggregateSeverity(s);
            s.AlertText = BuildAlert(s);
            return true;
        }

        // ---------------------------------------------------------------
        // 本地玩家定位
        // ---------------------------------------------------------------

        private Body ResolveLocalBody()
        {
            // 1) 联机 MOD 的 LOCAL_PLAYER（最准）
            try
            {
                Body mpBody = _mp.GetLocalBodyObject() as Body;
                if (mpBody != null) return mpBody;
            }
            catch (Exception)
            {
                // 忽略，走下一级回退
            }

            // 2) 游戏自身的 PlayerCamera.main.body
            //    没有相机 ⇒ 还在主菜单/加载中，此时**不能**做场景扫描兜底，
            //    否则会把场景里任意 NPC 或残留对象当成本地玩家并显示它的体征。
            try
            {
                PlayerCamera camera = PlayerCamera.main;
                if (camera == null) return null;
                if (camera.body != null) return camera.body;
            }
            catch (Exception)
            {
                return null;
            }

            // 3) 相机已存在但 body 尚未就绪：才允许场景扫描兜底
            return FirstSceneBody();
        }

        private Body FirstSceneBody()
        {
            Body[] bodies = ScanSceneBodies(false);
            if (bodies == null) return null;
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != null) return bodies[i];
            }
            return null;
        }

        /// <summary>扫描场景内所有 Body，结果按 <see cref="ScanCacheSeconds"/> 缓存。</summary>
        private Body[] ScanSceneBodies(bool force)
        {
            float now = Time.unscaledTime;
            if (!force && _sceneBodies != null && now - _sceneScanAt < ScanCacheSeconds) return _sceneBodies;

            try
            {
                _sceneBodies = UnityEngine.Object.FindObjectsOfType<Body>();
            }
            catch (Exception)
            {
                _sceneBodies = null;
            }

            _sceneScanAt = now;
            return _sceneBodies;
        }

        // ---------------------------------------------------------------
        // 状态标志
        // ---------------------------------------------------------------

        private static void ReadFlags(BodySnapshot s, Body b)
        {
            try
            {
                s.Alive = b.alive;
                s.Conscious = b.conscious;
                s.Breathing = b.breathing;
                s.Sleeping = b.sleeping;
                s.Dying = b.isDying;
                s.CriticallyDying = b.isCriticallyDying;
                s.CardiacArrest = b.inCardiacArrest;

                s.HeartRate = b.heartRate;
                s.BloodOxygen = b.bloodOxygen;
                s.RespiratoryPerMin = b.respiratoryRate * VitalRules.RespRatePerMinFactor;
                s.Fibrillation = b.fibrillationProgress;
            }
            catch (Exception)
            {
                // 个别属性在极端状态下可能抛异常，保留已读到的部分
            }
        }

        // ---------------------------------------------------------------
        // 生命体征
        // ---------------------------------------------------------------

        private void BuildVitals(BodySnapshot s, Body b)
        {
            List<VitalReading> v = s.Vitals;

            // ===== 循环系统 =====
            float heartRate = b.heartRate;
            Row(v, "循环系统", "心率", VitalRules.Int0(heartRate), "bpm", "heart",
                VitalRules.HeartRate(heartRate), "50-120",
                VitalRules.Normalize(heartRate, 0f, 200f), false, true);

            float bp = b.bloodPressure;
            string bpText = string.IsNullOrEmpty(b.bloodPressureReadout) ? VitalRules.Int0(bp) : b.bloodPressureReadout;
            Row(v, "循环系统", "血压", bpText, "mmHg", "bp",
                VitalRules.BloodPressure(bp), "80-170",
                VitalRules.Normalize(bp, 0f, 200f));

            float spo2 = b.bloodOxygen;
            Row(v, "循环系统", "血氧", VitalRules.Int0(spo2), "%", "oxygen",
                VitalRules.BloodOxygen(spo2), "≥65",
                VitalRules.Normalize(spo2, 60f, 100f), false, true);

            float volume = b.bloodVolume;
            bool bleeding = b.totalBleedSpeed > 0f;
            string volumeText;
            try
            {
                volumeText = VitalRules.Num1(b.bloodToLitersBody(volume));
            }
            catch (Exception)
            {
                volumeText = VitalRules.Num1(volume);
            }
            Row(v, "循环系统", "剩余血量", volumeText, "L", "blood",
                VitalRules.BloodVolume(volume, bleeding), "≥4.3 L（100 为基准）",
                VitalRules.Normalize(volume, 0f, 150f));

            float bleed = b.totalBleedSpeed;
            string bleedText = VitalRules.BleedReadout(bleed, b.bloodToLitersBody);
            Row(v, "循环系统", "流血速率", bleedText, "", "blood",
                VitalRules.BleedSpeed(bleed), "无出血",
                VitalRules.Normalize(bleed, 0f, 0.2f));

            // 循环系统次要指标：仅在异常（或用户要求全显）时出现
            AddSecondary(v, "循环系统", "心颤", VitalRules.Pct(b.fibrillationProgress), "%", "fibrillation",
                VitalRules.Fibrillation(b.fibrillationProgress, b.fibrillationRising), "0%", b.fibrillationProgress > 0f);
            AddSecondary(v, "循环系统", "血粘度", VitalRules.Num1(b.bloodViscosity), "", "viscosity",
                VitalRules.Viscosity(b.bloodViscosity), "≤40", Math.Abs(b.bloodViscosity) > 40f);
            AddSecondary(v, "循环系统", "内出血", VitalRules.Num1(b.internalBleeding), "", "blood",
                VitalRules.InternalBleeding(b.internalBleeding), "0", b.internalBleeding > 0f);
            AddSecondary(v, "循环系统", "血胸", VitalRules.Num1(b.hemothorax), "", "lungs",
                VitalRules.Hemothorax(b.hemothorax), "0", b.hemothorax > 0f);
            AddSecondary(v, "循环系统", "中风程度", VitalRules.Pct(b.strokeAmount), "%", "brain",
                VitalRules.Stroke(b.strokeAmount), "0%", b.strokeAmount > 0f);
            if (b.hasPulmonaryEmbolism)
            {
                Row(v, "循环系统", "肺栓塞", "存在", "", "lungs", Severity.Critical, "无", -1f, true);
            }

            // ===== 神经 / 感知 =====
            float consciousness = b.consciousness;
            Row(v, "神经 / 感知", "意识清醒度", VitalRules.Pct(consciousness), "", "conscious",
                VitalRules.Consciousness(consciousness), "≥30%",
                VitalRules.Normalize(consciousness, 0f, 100f));

            float pain = b.averagePain;
            Row(v, "神经 / 感知", "疼痛", VitalRules.Num1(pain), "", "pain",
                VitalRules.Pain(pain), "0-25",
                VitalRules.Normalize(pain, 0f, 100f));

            float brain = b.brainHealth;
            Row(v, "神经 / 感知", "大脑完整度", VitalRules.Pct(brain), "", "brain",
                VitalRules.BrainHealth(brain), "≥50%",
                VitalRules.Normalize(brain, 0f, 100f));

            float mood = b.totalHappiness;
            Row(v, "神经 / 感知", "心情", VitalRules.Num1(mood) + "（基础 " + VitalRules.Num1(b.happiness) + "）", "", "mood",
                VitalRules.Happiness(mood), "≥-20",
                VitalRules.Normalize(mood, -100f, 100f));

            float energy = b.energy;
            Row(v, "神经 / 感知", "精力", VitalRules.Pct(energy), "", "energy",
                VitalRules.Energy(energy), "≥35%（越低越困）",
                VitalRules.Normalize(energy, 0f, 100f));

            AddSecondary(v, "神经 / 感知", "疼痛性休克", VitalRules.Pct01(b.painShock), "", "painshock",
                VitalRules.PainShock(b.painShock), "0%", b.painShock > VitalRules.PainShockNotice);
            AddSecondary(v, "神经 / 感知", "休克", VitalRules.Pct(b.shock), "%", "shock",
                VitalRules.Shock(b.shock), "0%", b.shock > VitalRules.ShockNotice);
            AddSecondary(v, "神经 / 感知", "组织损伤", VitalRules.Pct(b.traumaAmount), "%", "bandage",
                VitalRules.Trauma(b.traumaAmount), "0%", b.traumaAmount > VitalRules.TraumaNotice);

            // ===== 代谢 / 内环境 =====
            float hunger = b.hunger;
            Row(v, "代谢 / 内环境", "饥饿", VitalRules.Pct(hunger), "", "food",
                VitalRules.Hunger(hunger), "≥10%",
                VitalRules.Normalize(hunger, 0f, 100f));

            float thirst = b.thirst;
            Row(v, "代谢 / 内环境", "口渴", VitalRules.Pct(thirst), "", "water",
                VitalRules.Thirst(thirst), "10%-175%",
                VitalRules.Normalize(thirst, 0f, 175f));

            float temp = b.temperature;
            Row(v, "代谢 / 内环境", "体温", VitalRules.Num1(temp), "°C", "thermo",
                VitalRules.Temperature(temp), "35-39 °C",
                VitalRules.Normalize(temp, 25f, 45f));

            string respText = string.IsNullOrEmpty(b.respiratoryRateReadout)
                ? VitalRules.Int0(b.respiratoryRate * VitalRules.RespRatePerMinFactor) + "/m"
                : b.respiratoryRateReadout;
            Row(v, "代谢 / 内环境", "呼吸频率", respText, "", "lungs",
                VitalRules.Respiration(b.respiratoryRate, b.breathing), "12-24 /m",
                VitalRules.Normalize(b.respiratoryRate * VitalRules.RespRatePerMinFactor, 0f, 40f), false, true);

            AddSecondary(v, "代谢 / 内环境", "疾病程度", VitalRules.Pct(b.sicknessAmount), "%", "sick",
                VitalRules.Sickness(b.sicknessAmount), "0%", b.sicknessAmount > VitalRules.SicknessNotice);
            AddSecondary(v, "代谢 / 内环境", "败血性休克", VitalRules.Pct(b.septicShock), "%", "sepsis",
                VitalRules.SepticShock(b.septicShock), "0%", b.septicShock > 1f);
            AddSecondary(v, "代谢 / 内环境", "辐射病", VitalRules.Pct(b.radiationSickness), "%", "rad",
                VitalRules.Radiation(b.radiationSickness), "0%", b.radiationSickness > 1f);
            AddSecondary(v, "代谢 / 内环境", "血中毒素", VitalRules.Num1(b.venomTotal), "", "skull",
                VitalRules.Venom(b.venomTotal), "0", b.venomTotal > VitalRules.VenomNotice);
            AddSecondary(v, "代谢 / 内环境", "潮湿", VitalRules.Pct(b.wetness), "%", "water",
                VitalRules.Wetness(b.wetness), "0%", b.wetness > VitalRules.WetnessNotice);
            AddSecondary(v, "代谢 / 内环境", "污浊", VitalRules.Pct(b.dirtyness), "%", "dirty",
                VitalRules.Dirtyness(b.dirtyness), "0%", b.dirtyness > VitalRules.DirtynessNotice);
            AddSecondary(v, "代谢 / 内环境", "听力损伤", VitalRules.Pct(b.hearingLoss), "%", "hearing",
                VitalRules.HearingLoss(b.hearingLoss), "0%", b.hearingLoss > VitalRules.HearingLossNotice);

            Row(v, "代谢 / 内环境", "免疫强度", VitalRules.Int0(b.immunity), "", "immune",
                VitalRules.Immunity(b.immunity), "≥100（上限 200）",
                VitalRules.Normalize(b.immunity, 0f, 200f));

            // ===== 体能 / 技能 =====
            float stamina = b.stamina;
            Row(v, "体能 / 技能", "体力", VitalRules.Pct(stamina), "", "energy",
                VitalRules.Stamina(stamina), "≥30%",
                VitalRules.Normalize(stamina, 0f, 100f));

            float load = 0f;
            try
            {
                load = b.GetTotalEncumberance();
            }
            catch (Exception)
            {
                load = b.totalEncumberance;
            }
            Row(v, "体能 / 技能", "负重", VitalRules.Num1(load) + " / " + VitalRules.Num1(b.maxEncumberance), "", "weight",
                VitalRules.Load(load, b.maxEncumberance), "不超过上限",
                VitalRules.Normalize(load, 0f, Mathf.Max(0.01f, b.maxEncumberance)));

            float speed = 0f;
            try
            {
                if (b.rb != null) speed = b.rb.velocity.magnitude;
            }
            catch (Exception)
            {
                speed = 0f;
            }
            Row(v, "体能 / 技能", "移动速度", VitalRules.Num1(speed) + "（上限 " + VitalRules.Num1(b.actualMaxSpeed) + "）", "", "walk",
                Severity.Normal, "", -1f);

            BuildSkillRows(v, b);
        }

        private static void BuildSkillRows(List<VitalReading> v, Body b)
        {
            Skills skills = b.skills;
            if (skills == null)
            {
                Row(v, "体能 / 技能", "技能经验", "N/A", "", "sparkle", Severity.Unknown, "", -1f, true);
                return;
            }

            SkillRow(v, "力量 STR", "strength", skills.STR, skills.expSTR, skills.minSTR, skills.maxSTR, skills);
            SkillRow(v, "韧性 RES", "shield", skills.RES, skills.expRES, skills.minRES, skills.maxRES, skills);
            SkillRow(v, "智力 INT", "intellect", skills.INT, skills.expINT, skills.minINT, skills.maxINT, skills);
        }

        private static void SkillRow(List<VitalReading> v, string name, string icon, int level, float exp,
            int min, int max, Skills skills)
        {
            float bar = -1f;
            string value = level + " 级";
            try
            {
                if (max > min)
                {
                    bar = VitalRules.Clamp01(skills.ToNextNormalized(exp, min, max));
                    value = level + " 级（" + VitalRules.Int0(bar * 100f) + "%）";
                }
            }
            catch (Exception)
            {
                bar = -1f;
            }

            Row(v, "体能 / 技能", name, value, "", icon, Severity.Normal, "升级进度", bar);
        }

        /// <summary>次要指标：仅在异常时加入（除非用户开启“始终显示次要指标”）。</summary>
        private void AddSecondary(List<VitalReading> v, string group, string label, string value, string unit,
            string icon, Severity severity, string limits, bool abnormal)
        {
            if (!abnormal && !_showAllSecondary) return;
            Row(v, group, label, value, unit, icon, severity, limits, -1f, true);
        }

        private static void Row(List<VitalReading> list, string group, string label, string value, string unit,
            string icon, Severity severity, string limits = null, float bar = -1f, bool secondary = false,
            bool channelCovered = false)
        {
            VitalReading r = new VitalReading();
            r.Group = group;
            r.Label = label;
            r.Value = value;
            r.Unit = unit;
            r.Icon = icon;
            r.Severity = severity;
            r.Limits = limits;
            r.Bar = bar;
            r.Secondary = secondary;
            r.ChannelCovered = channelCovered;
            list.Add(r);
        }

        // ---------------------------------------------------------------
        // 肢体
        // ---------------------------------------------------------------

        private static void BuildLimbs(BodySnapshot s, Body b)
        {
            Limb[] limbs = b.limbs;
            if (limbs == null || limbs.Length == 0) return;

            float sum = 0f;
            int counted = 0;
            int infected = 0;
            int injured = 0;
            float worstHealth = float.MaxValue;
            string worstName = null;

            for (int i = 0; i < limbs.Length; i++)
            {
                Limb limb = limbs[i];
                if (limb == null) continue;

                LimbReading r = new LimbReading();
                r.Name = string.IsNullOrEmpty(limb.shortName)
                    ? (string.IsNullOrEmpty(limb.fullName) ? "肢体 " + (i + 1) : limb.fullName)
                    : limb.shortName;
                r.SkinHealth = limb.skinHealth;
                r.MuscleHealth = limb.muscleHealth;
                r.Health = (limb.skinHealth + limb.muscleHealth) * 0.5f;
                r.Pain = limb.pain;
                r.Infection = limb.infectionAmount;
                r.Bleed = limb.bleedAmount;
                r.Dismembered = limb.dismembered;
                r.Broken = limb.broken;
                r.Dislocated = limb.dislocated;
                r.Splinted = limb.splinted;
                r.BlockedBleeding = limb.blockedBleeding;
                r.Infected = limb.infected;
                r.Shrapnel = limb.shrapnel;
                r.IsVital = limb.isVital;
                r.IsHead = limb.isHead;
                r.IsAbdomen = limb.isAbdomen;
                r.IsArm = limb.isArm;
                r.IsLeg = limb.isLegLimb;
                r.Severity = VitalRules.Limb(r.Health, r.Infection, r.Bleed,
                    r.Dismembered, r.Broken, r.Dislocated, r.Infected, r.IsVital);

                s.Limbs.Add(r);

                sum += r.Health;
                counted++;
                if (r.Infected || r.Infection > 0.001f) infected++;
                if (r.Severity >= Severity.Warning) injured++;
                if (r.Health < worstHealth)
                {
                    worstHealth = r.Health;
                    worstName = r.Name;
                }
            }

            if (counted > 0)
            {
                s.AverageLimbHealth = sum / counted;
                s.WorstLimbHealth = worstHealth;
                s.WorstLimbName = worstName;
            }
            s.InfectedLimbCount = infected;
            s.InjuredLimbCount = injured;

            // 最严重的肢体排在最前，医生视角优先看要害
            s.Limbs.Sort(CompareLimbs);
        }

        private static int CompareLimbs(LimbReading a, LimbReading b)
        {
            int bySeverity = b.Severity.CompareTo(a.Severity);
            if (bySeverity != 0) return bySeverity;
            int byVital = (a.IsVital ? 0 : 1).CompareTo(b.IsVital ? 0 : 1);
            if (byVital != 0) return byVital;
            return a.Health.CompareTo(b.Health);
        }

        // ---------------------------------------------------------------
        // 队友
        // ---------------------------------------------------------------

        private void BuildMates(BodySnapshot s, Body local)
        {
            bool viaMod = false;
            _mateDetailUsed = 0;

            if (_mp.Available)
            {
                try
                {
                    if (_mp.CollectLivingPlayers(_players))
                    {
                        for (int i = 0; i < _players.Count; i++)
                        {
                            MpBridge.Entry entry = _players[i];

                            // is_local 优先于 body 比对：本地 Body 尚未解析时也能正确排除自己
                            if (entry.IsLocal)
                            {
                                if (!string.IsNullOrEmpty(entry.Name)) s.PlayerName = entry.Name;
                                continue;
                            }

                            Body body = entry.BodyObject as Body;
                            if (body == null || body == local) continue;

                            string name = string.IsNullOrEmpty(entry.Name) ? "玩家 " + (i + 1) : entry.Name;
                            s.Mates.Add(BuildMate(name, body, local, entry.Ping, entry.SteamId, entry.Avatar));
                        }
                        viaMod = true;
                    }
                }
                catch (Exception)
                {
                    viaMod = false;
                }
            }

            // 单机兜底：场景内的其它存活 Body（无法区分 NPC，故标注来源）
            if (!viaMod)
            {
                Body[] bodies = ScanSceneBodies(false);
                if (bodies == null) return;

                // 每名对象都要额外生成一份完整快照（供详情页复用），
                // 因此设上限，避免 NPC 很多的场景里白跑大量解析与字符串分配。
                const int FallbackLimit = 12;

                int index = 0;
                for (int i = 0; i < bodies.Length && index < FallbackLimit; i++)
                {
                    Body body = bodies[i];
                    if (body == null || body == local) continue;
                    if (!SafeAlive(body)) continue;

                    string raw = body.gameObject != null ? body.gameObject.name : "?";
                    // 单机兜底：没有 Steam 身份，也就不会有头像
                    s.Mates.Add(BuildMate("观察对象 " + (index + 1) + "（" + raw + "）", body, local, -1f, 0UL, null));
                    index++;
                }
            }
        }

        private static bool SafeAlive(Body b)
        {
            try
            {
                return b.alive;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private MateReading BuildMate(string name, Body body, Body local, float ping, ulong steamId, object avatar)
        {
            MateReading m = new MateReading();
            m.Name = name;
            m.Distance = -1f;
            m.SteamId = steamId;
            m.Avatar = avatar;

            try
            {
                m.Alive = body.alive;
                m.Conscious = body.conscious;
                m.Breathing = body.breathing;
                m.HeartRate = body.heartRate;
                m.BloodOxygen = body.bloodOxygen;
                m.Consciousness = body.consciousness;
                m.Pain = body.averagePain;
                m.BloodBar = VitalRules.Normalize(body.bloodVolume, 0f, 100f);

                if (local != null && local.transform != null && body.transform != null)
                {
                    Vector2 a = local.transform.position;
                    Vector2 c = body.transform.position;
                    m.Distance = Vector2.Distance(a, c);
                }

                m.Severity = MateSeverity(body);
                m.Summary = MateSummary(body);
                m.WorstLimb = WorstLimbOf(body);

                // 与本地玩家同源的完整快照：详情窗口直接复用同一套渲染代码，
                // 因此队友能看到的指标项与自己的完全一致（含次要指标的异常显示规则）。
                BodySnapshot detail = NextMateDetail();
                detail.Valid = true;
                detail.PlayerName = name;
                detail.InMultiplayer = _mp.Available;
                detail.SampledAt = Time.unscaledTime;
                ReadFlags(detail, body);
                BuildVitals(detail, body);
                BuildLimbs(detail, body);
                detail.Overall = AggregateSeverity(detail);
                detail.AlertText = BuildAlert(detail);
                m.Detail = detail;
            }
            catch (Exception)
            {
                m.Severity = Severity.Unknown;
                m.Summary = "读取失败";
                m.Detail = null;
            }

            if (ping > 0f) m.Summary = m.Summary + " · " + VitalRules.Int0(ping) + "ms";
            return m;
        }

        /// <summary>
        /// 取一个队友详情快照（池化复用）。每轮采集前会把池计数归零，
        /// 因此同一个实例每轮都会被 Clear 后重填，不会残留上轮数据。
        /// </summary>
        private BodySnapshot NextMateDetail()
        {
            if (_mateDetailUsed < _mateDetails.Count)
            {
                BodySnapshot reused = _mateDetails[_mateDetailUsed++];
                reused.Clear();
                return reused;
            }

            BodySnapshot created = new BodySnapshot();
            _mateDetails.Add(created);
            _mateDetailUsed++;
            return created;
        }

        private static Severity MateSeverity(Body b)
        {
            try
            {
                if (!b.alive) return Severity.Critical;
                if (b.isCriticallyDying) return Severity.Critical;

                Severity worst = Severity.Normal;
                if (b.isDying) worst = Severity.Warning;
                worst = VitalRules.Worse(worst, VitalRules.Consciousness(b.consciousness));
                worst = VitalRules.Worse(worst, VitalRules.BloodOxygen(b.bloodOxygen));
                worst = VitalRules.Worse(worst, VitalRules.BleedSpeed(b.totalBleedSpeed));
                worst = VitalRules.Worse(worst, VitalRules.BloodVolume(b.bloodVolume, b.totalBleedSpeed > 0f));
                worst = VitalRules.Worse(worst, VitalRules.Pain(b.averagePain));
                if (!b.breathing) worst = VitalRules.Worse(worst, Severity.Critical);
                return worst;
            }
            catch (Exception)
            {
                return Severity.Unknown;
            }
        }

        private static string MateSummary(Body b)
        {
            try
            {
                if (!b.alive) return "已死亡";
                if (b.inCardiacArrest) return "心脏骤停";
                if (!b.breathing) return "呼吸停止";

                List<string> parts = new List<string>(4);
                if (!b.conscious) parts.Add("昏迷");
                if (b.totalBleedSpeed >= VitalRules.BleedCritical) parts.Add("大出血");
                else if (b.totalBleedSpeed > VitalRules.BleedNotice) parts.Add("出血中");
                if (b.bloodOxygen < VitalRules.BloodOxygenDying) parts.Add("低血氧");
                if (b.septicShock > VitalRules.SepticWarning) parts.Add("败血性休克");
                if (b.averagePain > VitalRules.PainWarning) parts.Add("剧痛");
                if (b.sleeping) parts.Add("睡眠中");

                if (parts.Count == 0) return "状态稳定";

                string text = parts[0];
                for (int i = 1; i < parts.Count && i < 3; i++) text = text + " · " + parts[i];
                return text;
            }
            catch (Exception)
            {
                return "读取失败";
            }
        }

        private static string WorstLimbOf(Body b)
        {
            try
            {
                Limb[] limbs = b.limbs;
                if (limbs == null) return null;

                string text = null;
                Severity worst = Severity.Normal;
                for (int i = 0; i < limbs.Length; i++)
                {
                    Limb limb = limbs[i];
                    if (limb == null) continue;

                    float health = (limb.skinHealth + limb.muscleHealth) * 0.5f;
                    Severity sev = VitalRules.Limb(health, limb.infectionAmount, limb.bleedAmount,
                        limb.dismembered, limb.broken, limb.dislocated, limb.infected, limb.isVital);
                    if (sev <= Severity.Notice || sev <= worst) continue;

                    string name = string.IsNullOrEmpty(limb.shortName) ? limb.fullName : limb.shortName;
                    string flag = limb.dismembered ? "断肢"
                        : limb.broken ? "骨折"
                        : limb.dislocated ? "脱臼"
                        : limb.bleedAmount > 0.01f ? "出血"
                        : limb.infected || limb.infectionAmount > 1f ? "感染"
                        : "重伤";
                    worst = sev;
                    text = name + " " + flag;
                }
                return text;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---------------------------------------------------------------
        // 汇总
        // ---------------------------------------------------------------

        private static Severity AggregateSeverity(BodySnapshot s)
        {
            Severity worst = Severity.Normal;

            if (!s.Alive) return Severity.Critical;
            if (s.CriticallyDying) worst = Severity.Critical;
            else if (s.Dying) worst = Severity.Warning;
            if (!s.Breathing) worst = Severity.Critical;

            for (int i = 0; i < s.Vitals.Count; i++)
            {
                worst = VitalRules.Worse(worst, s.Vitals[i].Severity);
            }

            for (int i = 0; i < s.Limbs.Count; i++)
            {
                worst = VitalRules.Worse(worst, s.Limbs[i].Severity);
            }

            return worst;
        }

        /// <summary>
        /// 状态条文案：只表达「当前角色状态」本身，不再列举指标名——
        /// 指标名在下方卡片里已经有了，重复显示只会让界面变吵。
        /// </summary>
        private static string BuildAlert(BodySnapshot s)
        {
            if (!s.Alive) return "角色已死亡";
            if (s.CriticallyDying) return "濒死状态 · 需立即救治";
            if (s.Dying) return "垂危状态";

            switch (s.Overall)
            {
                case Severity.Critical:
                    return "危急状态";
                case Severity.Warning:
                    return "状态异常";
                case Severity.Notice:
                    return "状态欠佳";
                default:
                    return "状态良好";
            }
        }
    }
}
