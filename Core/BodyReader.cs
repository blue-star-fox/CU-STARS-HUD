using System;
using System.Collections.Generic;
using UnityEngine;

namespace CuStatsHud.Core
{
    
    internal sealed partial class BodyReader
    {
        private const float ScanCacheSeconds = 2f;

        private readonly MpBridge _mp;
        private readonly List<MpBridge.Entry> _players = new List<MpBridge.Entry>(16);

        private readonly List<BodySnapshot> _mateDetails = new List<BodySnapshot>(8);
        private int _mateDetailUsed;

        private Body[] _sceneBodies;
        private float _sceneScanAt = -999f;

        private bool _showAllSecondary;

        public Body LocalBody { get; private set; }

        public BodyReader(MpBridge mp)
        {
            _mp = mp;
        }

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

        private Body ResolveLocalBody()
        {
            
            try
            {
                Body mpBody = _mp.GetLocalBodyObject() as Body;
                if (mpBody != null) return mpBody;
            }
            catch (Exception)
            {
                
            }

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
                
            }
        }

        private void BuildVitals(BodySnapshot s, Body b)
        {
            List<VitalReading> v = s.Vitals;

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
                
                volumeText = VitalRules.Num2(b.bloodToLitersBody(volume));
            }
            catch (Exception)
            {
                volumeText = VitalRules.Num1(volume);
            }
            Row(v, "循环系统", "剩余血量", volumeText, "L", "blood",
                VitalRules.BloodVolume(volume, bleeding), "≥4.3 L（100 为基准）",
                VitalRules.Normalize(volume, 0f, 150f));

            float bleed = b.totalBleedSpeed;
            
            string bleedText = VitalRules.BleedReadout(bleed, b.bloodToLiters);
            Row(v, "循环系统", "流血速率", bleedText, "", "blood",
                VitalRules.BleedSpeed(bleed), "无出血",
                VitalRules.Normalize(bleed, 0f, 0.2f));

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

            float consciousness = b.consciousness;
            Row(v, "神经 / 感知", "意识清醒度", VitalRules.Pct(consciousness), "", "conscious",
                VitalRules.Consciousness(consciousness), "≥30%",
                VitalRules.Normalize(consciousness, 0f, 100f));

            float pain = b.averagePain;
            
            Row(v, "神经 / 感知", "疼痛", VitalRules.Pct(pain), "", "pain",
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

            Row(v, "代谢 / 内环境", "辐射剂量", VitalRules.Num1(b.radiationSickness * 0.3f), "gy", "rad",
                VitalRules.Radiation(b.radiationSickness), "0.0 gy",
                VitalRules.Normalize(b.radiationSickness, 0f, 100f));

            Row(v, "代谢 / 内环境", "疾病程度", VitalRules.Pct(b.sicknessAmount), "", "sick",
                VitalRules.Sickness(b.sicknessAmount), "0%",
                VitalRules.Normalize(b.sicknessAmount, 0f, 100f));

            AddSecondary(v, "代谢 / 内环境", "败血性休克", VitalRules.Pct(b.septicShock), "%", "sepsis",
                VitalRules.SepticShock(b.septicShock), "0%", b.septicShock > 1f);
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

            float stamina = b.stamina;
            Row(v, "体能 / 技能", "体力", VitalRules.Pct(stamina), "", "energy",
                VitalRules.Stamina(stamina), "≥30%",
                VitalRules.Normalize(stamina, 0f, 100f));

            Row(v, "体能 / 技能", "体重", VitalRules.Num1(b.weightOffset * 0.34f + 50f), "kg", "weight",
                VitalRules.Weight(b.weightOffset), "50 kg",
                VitalRules.Normalize(b.weightOffset * 0.34f + 50f, 30f, 120f));

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
