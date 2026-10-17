using System.Collections.Generic;

namespace CuStatsHud.Core
{
    /// <summary>监护仪分级。数值越大越危急，用于统一着色与排序。</summary>
    public enum Severity
    {
        Unknown = -1,
        Normal = 0,
        /// <summary>偏离但不构成威胁（如轻度疼痛、轻度脱水）。</summary>
        Notice = 1,
        /// <summary>需要关注（对应游戏 Body.isDying 的单项阈值）。</summary>
        Warning = 2,
        /// <summary>危及生命（对应游戏 Body.isCriticallyDying 的单项阈值）。</summary>
        Critical = 3,
    }

    /// <summary>一条生命体征读数。struct + 复用的 List，避免每轮刷新产生垃圾。</summary>
    public struct VitalReading
    {
        /// <summary>中文名称，如“心率”。</summary>
        public string Label;
        /// <summary>已格式化的主数值，如“78”。</summary>
        public string Value;
        /// <summary>单位/后缀，如“bpm”。</summary>
        public string Unit;
        /// <summary>图标种类（见 IconFactory.ShapePixel）。</summary>
        public string Icon;
        /// <summary>该行归属的分组标题。</summary>
        public string Group;
        /// <summary>分级结果。</summary>
        public Severity Severity;
        /// <summary>参考范围文本，如“50-120”；为空表示不显示。</summary>
        public string Limits;
        /// <summary>0..1 的主数值占比（用于横向范围条）；小于 0 表示不画条。</summary>
        public float Bar;
        /// <summary>是否只在异常时显示（次要指标）。</summary>
        public bool Secondary;

        /// <summary>
        /// 该指标已由波形区通道块常显覆盖（心率 / 血氧 / 呼吸）。
        /// 波形开启时列表里就不再重复显示；波形关闭时仍会出现在列表里，避免丢数据。
        /// 注意：无论是否显示，它都参与整体分级判定。
        /// </summary>
        public bool ChannelCovered;

        public bool HasBar
        {
            get { return Bar >= 0f; }
        }
    }

    /// <summary>一条肢体的监护读数。</summary>
    public struct LimbReading
    {
        /// <summary>显示名（shortName 优先，回退 fullName）。</summary>
        public string Name;
        /// <summary>皮肤与肌肉健康的均值，0..100。</summary>
        public float Health;
        public float SkinHealth;
        public float MuscleHealth;
        public float Pain;
        /// <summary>感染程度，0..100。</summary>
        public float Infection;
        /// <summary>出血量（Limb.bleedAmount 原值）。</summary>
        public float Bleed;
        public bool Dismembered;
        public bool Broken;
        public bool Dislocated;
        public bool Splinted;
        public bool BlockedBleeding;
        public bool Infected;
        public int Shrapnel;
        /// <summary>要害（Limb.isVital）：头/躯干等，掉血优先级最高。</summary>
        public bool IsVital;
        public bool IsHead;
        public bool IsAbdomen;
        public bool IsArm;
        public bool IsLeg;
        /// <summary>本肢体的最坏分级。</summary>
        public Severity Severity;
    }

    /// <summary>一名队友/其他存活玩家的监护摘要。</summary>
    public struct MateReading
    {
        public string Name;

        /// <summary>SteamID；0 表示未知（此时不会有头像）。</summary>
        public ulong SteamId;

        /// <summary>
        /// Steam 头像原图。实际类型是 UnityEngine.Texture2D——这里用 object 存放，
        /// 是为了让本文件保持「无 Unity 依赖」（它被单元测试工程直接编译），
        /// UI 层再 <c>as Texture2D</c> 取用。为 null 表示没有头像，界面不画。
        /// </summary>
        public object Avatar;

        /// <summary>距离本地玩家的米数；小于 0 表示无法测量。</summary>
        public float Distance;
        public bool Alive;
        public bool Conscious;
        public bool Breathing;
        /// <summary>综合分级。</summary>
        public Severity Severity;
        /// <summary>一句话摘要，如“失血性休克 · 昏迷”。</summary>
        public string Summary;
        /// <summary>相对血量 0..1（Blood.bloodVolume / 100，用于血条）。</summary>
        public float BloodBar;
        public float HeartRate;
        public float BloodOxygen;
        public float Consciousness;
        public float Pain;
        /// <summary>该队友最严重的肢体问题描述；为空表示肢体完好。</summary>
        public string WorstLimb;

        /// <summary>
        /// 该队友的完整快照（体征 + 肢体），由 BodyReader 复用实例填充。
        /// 详情窗口用它跑与本地玩家**完全相同**的渲染代码，因此信息项必然一样多；
        /// 列表卡片只用上面的摘要字段。为 null 表示本轮没取到（界面需容错）。
        /// </summary>
        public BodySnapshot Detail;
    }

    /// <summary>
    /// 一轮采集的完整快照。实例被复用（Clear + 重新填充），
    /// 因此 UI 层可以在渲染过程中自由遍历而不产生额外分配。
    /// </summary>
    public sealed class BodySnapshot
    {
        /// <summary>是否成功读到本地玩家。</summary>
        public bool Valid;
        /// <summary>读取失败时的说明文本。</summary>
        public string Error;

        /// <summary>本地玩家显示名（联机时取 NetPlayer.playername）。</summary>
        public string PlayerName;
        public bool InMultiplayer;

        public bool Alive;
        public bool Conscious;
        public bool Breathing;
        public bool Sleeping;
        public bool Dying;
        public bool CriticallyDying;
        public bool CardiacArrest;

        // 波形与派生显示需要的原始数值（避免 UI 反过来解析格式化文本）
        public float HeartRate;
        public float BloodOxygen;
        /// <summary>呼吸频率，已换算为“次/分钟”。</summary>
        public float RespiratoryPerMin;
        public float Fibrillation;

        /// <summary>综合分级，取所有主要指标中最坏的一档。</summary>
        public Severity Overall;
        /// <summary>告警横幅主文案，如“危急：失血性休克 · 心脏骤停”。</summary>
        public string AlertText;
        /// <summary>采集时刻的游戏时间（Time.unscaledTime），用于“数据陈旧”判断。</summary>
        public float SampledAt;

        public readonly List<VitalReading> Vitals = new List<VitalReading>(32);
        public readonly List<LimbReading> Limbs = new List<LimbReading>(16);
        public readonly List<MateReading> Mates = new List<MateReading>(8);

        public int InfectedLimbCount;
        public int InjuredLimbCount;
        public float AverageLimbHealth = 100f;
        public string WorstLimbName;
        public float WorstLimbHealth = 100f;

        public void Clear()
        {
            Valid = false;
            Error = null;
            PlayerName = null;
            InMultiplayer = false;
            Alive = false;
            Conscious = false;
            Breathing = false;
            Sleeping = false;
            Dying = false;
            CriticallyDying = false;
            CardiacArrest = false;
            HeartRate = 0f;
            BloodOxygen = 0f;
            RespiratoryPerMin = 0f;
            Fibrillation = 0f;
            Overall = Severity.Unknown;
            AlertText = null;
            SampledAt = 0f;
            Vitals.Clear();
            Limbs.Clear();
            Mates.Clear();
            InfectedLimbCount = 0;
            InjuredLimbCount = 0;
            AverageLimbHealth = 100f;
            WorstLimbName = null;
            WorstLimbHealth = 100f;
        }
    }
}
