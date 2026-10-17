using System;
using System.Globalization;

namespace CuStatsHud.Core
{
    /// <summary>
    /// 生命体征分级与格式化——纯逻辑，不依赖 UnityEngine，可独立单元测试。
    ///
    /// 阈值来源：全部取自游戏自身 <c>Assembly-CSharp</c> 的 Body.cs，
    /// 因此监护仪的告警红线和游戏内部“垂危/濒死”判定一致，而不是拍脑袋定的数字。
    /// 每条标注了出处；凡游戏未给出明确阈值、由本插件按医学常识估计的，标注为“经验值”。
    /// </summary>
    public static class VitalRules
    {
        // ===== 心率 Body.inCardiacArrest: heartRate &lt; 20；Body.fibrillationRising: heartRate &gt; 200 =====
        public const float HeartRateArrest = 20f;
        public const float HeartRateFibrillation = 200f;

        // ===== 血压 Body.isDying: &lt;80 或 &gt;170；Body.isCriticallyDying: &lt;70 =====
        public const float BloodPressureCriticalLow = 70f;
        public const float BloodPressureDyingLow = 80f;
        public const float BloodPressureDyingHigh = 170f;

        // ===== 血氧 Body.isDying: &lt;65；Body.isCriticallyDying: &lt;50 =====
        public const float BloodOxygenDying = 65f;
        public const float BloodOxygenCritical = 50f;

        // ===== 血量(0-100) Body.isDying: 出血且 &lt;40；Body.isCriticallyDying: 出血且 &lt;30 =====
        public const float BloodVolumeDying = 40f;
        public const float BloodVolumeCritical = 30f;

        // ===== 流血速率(L/min 内部量纲) Body.isDying: &gt;0.134；需关注: &gt;0.02 =====
        public const float BleedCritical = 0.134f;
        public const float BleedNotice = 0.02f;

        // ===== 体温 Body.isDying: &gt;41 或 &lt;29；Body.isCriticallyDying: &gt;41.5 或 &lt;27 =====
        public const float TempCriticalHigh = 41.5f;
        public const float TempCriticalLow = 27f;
        public const float TempDyingHigh = 41f;
        public const float TempDyingLow = 29f;

        // ===== 意识 Body.conscious: consciousness &gt; 30；Body.brainDying: &lt; 5 =====
        public const float ConsciousnessUnconscious = 30f;
        public const float ConsciousnessCritical = 5f;

        // ===== 疼痛(0-100) 经验值；Body 在 averagePain &gt; 50 且清醒时触发呼喊 =====
        public const float PainNotice = 25f;
        public const float PainWarning = 50f;
        public const float PainSevere = 75f;

        // ===== 大脑完整度 Body.alive: brainHealth &gt; 0；经验值分档 =====
        public const float BrainCritical = 25f;
        public const float BrainWarning = 50f;

        // ===== 呼吸 respiratoryRate 需 ÷4 得每分钟次数(见 Body.respiratoryRateReadout) =====
        public const float RespRatePerMinFactor = 0.25f;

        // ===== 心情 Body.aboveMedicalCutoff: &gt; -75；Body.allowUseItem: &gt; -20 =====
        public const float HappinessCritical = -75f;
        public const float HappinessWarning = -20f;

        // ===== 精力 Body.canTakeNap: energy &lt; 35 =====
        public const float EnergyNapThreshold = 35f;

        // ===== 饥饿/口渴 Body.isDying: hunger &lt; 10、thirst &lt; 10、thirst &gt; 175 且 brainHealth &lt; 50 =====
        public const float HungerDying = 10f;
        public const float ThirstDying = 10f;
        public const float ThirstDehydrated = 175f;

        // ===== 败血性休克 Body.isDying: &gt; 75；Body.isCriticallyDying: &gt; 82.5 =====
        public const float SepticWarning = 75f;
        public const float SepticCritical = 82.5f;

        // ===== 辐射病 Body.isDying: &gt; 60 =====
        public const float RadiationCritical = 60f;

        // ===== 中风 Body.isDying: &gt; 50 =====
        public const float StrokeCritical = 50f;

        // ===== 心颤 Body.isCriticallyDying: fibrillationProgress > 60；
        //        另：>75 时游戏会开始压制 ECG 波形幅度（Body.GetECGHeight）=====
        public const float FibrillationCritical = 60f;

        // ===== 血粘度 Body.fibrillationRising: &gt; 80（范围 -100..100）=====
        public const float ViscosityWarning = 80f;

        // ===== 免疫 Body.Update: 钳制在 0-200，正常基准 100 =====
        public const float ImmunityBaseline = 100f;

        // ===== 组织损伤 traumaAmount 钳制 0-100；Body.totalHappiness 中按 0.525/点扣心情 =====
        public const float TraumaNotice = 25f;

        // ===== 经验值分档（游戏未给出明确阈值，按监护习惯设置）=====
        public const float HemothoraxNotice = 5f;
        public const float InternalBleedNotice = 5f;
        public const float ShockNotice = 20f;
        public const float ShockWarning = 50f;
        public const float WetnessNotice = 75f;
        public const float DirtynessNotice = 50f;
        public const float HearingLossNotice = 10f;
        public const float VenomNotice = 1f;
        public const float PainShockNotice = 0.2f;
        public const float SicknessNotice = 1f;

        /// <summary>取两档中更危急的一档。</summary>
        public static Severity Worse(Severity a, Severity b)
        {
            return a >= b ? a : b;
        }

        // ---------------------------------------------------------------
        // 各指标分级
        // ---------------------------------------------------------------

        public static Severity HeartRate(float v)
        {
            if (v < HeartRateArrest || v > HeartRateFibrillation) return Severity.Critical;
            if (v < 50f || v > 150f) return Severity.Warning;
            if (v < 60f || v > 110f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity BloodPressure(float v)
        {
            if (v < BloodPressureCriticalLow || v > 185f) return Severity.Critical;
            if (v < BloodPressureDyingLow || v > BloodPressureDyingHigh) return Severity.Warning;
            if (v < 95f || v > 140f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity BloodOxygen(float v)
        {
            if (v < BloodOxygenCritical) return Severity.Critical;
            if (v < BloodOxygenDying) return Severity.Warning;
            if (v < 90f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity BloodVolume(float v, bool bleeding)
        {
            if (v < BloodVolumeCritical && bleeding) return Severity.Critical;
            if (v < BloodVolumeDying && bleeding) return Severity.Warning;
            if (v < 80f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity BleedSpeed(float v)
        {
            if (v > BleedCritical) return Severity.Critical;
            if (v > BleedNotice) return Severity.Warning;
            if (v > 0f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Temperature(float v)
        {
            if (v <= 0f) return Severity.Unknown; // 尚未初始化
            if (v < TempCriticalLow || v > TempCriticalHigh) return Severity.Critical;
            if (v < TempDyingLow || v > TempDyingHigh) return Severity.Warning;
            if (v < 35.5f || v > 38.5f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Consciousness(float v)
        {
            if (v < ConsciousnessCritical) return Severity.Critical;
            if (v < ConsciousnessUnconscious) return Severity.Warning;
            if (v < 70f) return Severity.Notice;
            return Severity.Normal;
        }

        /// <summary>
        /// 疼痛分级。注意：疼痛**最高只到警告**（PainSevere 以上同样是警告）——
        /// 游戏 isCriticallyDying 不依赖疼痛，若让疼痛单独把面板打成「危急」，
        /// 告警横幅会出现「危急 · 疼痛」这种误导性文案。
        /// </summary>
        public static Severity Pain(float v)
        {
            if (v > PainSevere) return Severity.Warning;
            if (v > PainWarning) return Severity.Warning;
            if (v > PainNotice) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity BrainHealth(float v)
        {
            if (v <= BrainCritical) return Severity.Critical;
            if (v < BrainWarning) return Severity.Warning;
            if (v < 80f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Respiration(float rawRate, bool breathing)
        {
            return RespirationPerMin(rawRate * RespRatePerMinFactor, breathing);
        }

        /// <summary>按“次/分钟”分级（UI 直接拿到的是已换算值，因此单独提供）。</summary>
        public static Severity RespirationPerMin(float perMin, bool breathing)
        {
            if (!breathing) return Severity.Critical;
            if (perMin <= 0f || perMin < 6f || perMin > 40f) return Severity.Critical;
            if (perMin < 10f || perMin > 30f) return Severity.Warning;
            if (perMin < 12f || perMin > 24f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Happiness(float v)
        {
            if (v < HappinessCritical) return Severity.Critical;
            if (v < HappinessWarning) return Severity.Warning;
            if (v < 0f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Energy(float v)
        {
            if (v < 10f) return Severity.Warning;
            if (v < EnergyNapThreshold) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Hunger(float v)
        {
            if (v <= 0f) return Severity.Critical;
            if (v < HungerDying) return Severity.Warning;
            if (v < 30f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Thirst(float v)
        {
            if (v < 0f || v > ThirstDehydrated) return Severity.Critical;
            if (v < ThirstDying) return Severity.Warning;
            if (v < 30f || v > 120f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Stamina(float v)
        {
            if (v < 5f) return Severity.Warning;
            if (v < 30f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity SepticShock(float v)
        {
            if (v > SepticCritical) return Severity.Critical;
            if (v > SepticWarning) return Severity.Warning;
            if (v > 1f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Sickness(float v)
        {
            if (v > 80f) return Severity.Warning;
            if (v > SicknessNotice) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Radiation(float v)
        {
            if (v > RadiationCritical) return Severity.Critical;
            if (v > 30f) return Severity.Warning;
            if (v > 1f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Fibrillation(float v, bool rising)
        {
            if (v > FibrillationCritical) return Severity.Critical;
            if (v > 1f && rising) return Severity.Warning;
            if (v > 0f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Stroke(float v)
        {
            if (v > StrokeCritical) return Severity.Critical;
            if (v > 10f) return Severity.Warning;
            if (v > 0f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Viscosity(float v)
        {
            if (v > ViscosityWarning) return Severity.Warning;
            if (v > 40f) return Severity.Notice;
            return Severity.Normal;
        }

        /// <summary>免疫：基准为 <see cref="ImmunityBaseline"/>，低于 40 明显受损。</summary>
        public static Severity Immunity(float v)
        {
            if (v < 40f) return Severity.Warning;
            if (v < 90f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Load(float current, float max)
        {
            if (max <= 0f) return Severity.Unknown;
            float t = current / max;
            if (t > 1f) return Severity.Warning;
            if (t > 0.8f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Hemothorax(float v)
        {
            if (v > 40f) return Severity.Critical;
            if (v > HemothoraxNotice) return Severity.Warning;
            if (v > 0f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity InternalBleeding(float v)
        {
            if (v > 60f) return Severity.Critical;
            if (v > 20f) return Severity.Warning;
            if (v > 0f) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Shock(float v)
        {
            if (v > 80f) return Severity.Critical;
            if (v > ShockWarning) return Severity.Warning;
            if (v > ShockNotice) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Trauma(float v)
        {
            if (v > 75f) return Severity.Warning;
            if (v > TraumaNotice) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Wetness(float v)
        {
            return v > WetnessNotice ? Severity.Notice : Severity.Normal;
        }

        public static Severity Dirtyness(float v)
        {
            return v > DirtynessNotice ? Severity.Notice : Severity.Normal;
        }

        public static Severity HearingLoss(float v)
        {
            if (v > 50f) return Severity.Warning;
            if (v > HearingLossNotice) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity PainShock(float v)
        {
            if (v > 0.6f) return Severity.Warning;
            if (v > PainShockNotice) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Venom(float v)
        {
            if (v > 30f) return Severity.Critical;
            if (v > 10f) return Severity.Warning;
            if (v > VenomNotice) return Severity.Notice;
            return Severity.Normal;
        }

        public static Severity Infection(float v)
        {
            if (v > 50f) return Severity.Warning;
            if (v > 1f) return Severity.Notice;
            return Severity.Normal;
        }

        // ---------------------------------------------------------------
        // 肢体分级
        // ---------------------------------------------------------------

        /// <summary>肢体健康分级：把断肢/骨折/脱臼/感染/出血都算进去。</summary>
        public static Severity Limb(float health, float infection, float bleed,
            bool dismembered, bool broken, bool dislocated, bool infected, bool vital)
        {
            if (dismembered) return Severity.Critical;
            if (bleed > 0.6f) return Severity.Critical;
            if (broken || dislocated) return vital ? Severity.Critical : Severity.Warning;
            if (infection > 50f || infected) return Severity.Warning;
            if (bleed > 0.01f) return Severity.Warning;
            if (health < 40f) return vital ? Severity.Warning : Severity.Notice;
            if (health < 80f || infection > 1f) return Severity.Notice;
            return Severity.Normal;
        }

        // ---------------------------------------------------------------
        // 格式化
        // ---------------------------------------------------------------

        public static string Int0(float v)
        {
            return ((int)Math.Round(v, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
        }

        public static string Num1(float v)
        {
            return v.ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static string Num2(float v)
        {
            return v.ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>0-100 的数值格式化为百分比整数。</summary>
        public static string Pct(float v)
        {
            return Int0(v) + "%";
        }

        /// <summary>0-1 的比例格式化为百分比整数。</summary>
        public static string Pct01(float v)
        {
            return Int0(v * 100f) + "%";
        }

        public static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }

        public static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        /// <summary>把数值线性映射到 0..1（用于范围条）。</summary>
        public static float Normalize(float v, float min, float max)
        {
            if (max - min <= 0.0001f) return 0f;
            return Clamp01((v - min) / (max - min));
        }

        /// <summary>流血速率换算为“升/分钟”：Body.bloodToLitersBody 给出体内升数比例，×60 得每分钟。</summary>
        public static string BleedReadout(float totalBleedSpeed, Func<float, float> toLiters)
        {
            if (totalBleedSpeed <= 0.0001f) return "无出血";
            try
            {
                if (toLiters != null) return Num2(toLiters(totalBleedSpeed) * 60f) + " L/min";
            }
            catch
            {
                // 回退到原始值
            }
            return Num2(totalBleedSpeed);
        }
    }
}
