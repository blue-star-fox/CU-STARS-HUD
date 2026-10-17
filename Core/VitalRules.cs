using System;
using System.Globalization;

namespace CuStatsHud.Core
{
    
    public static class VitalRules
    {
        
        public const float HeartRateArrest = 20f;
        public const float HeartRateFibrillation = 200f;

        public const float BloodPressureCriticalLow = 70f;
        public const float BloodPressureDyingLow = 80f;
        public const float BloodPressureDyingHigh = 170f;

        public const float BloodOxygenDying = 65f;
        public const float BloodOxygenCritical = 50f;

        public const float BloodVolumeDying = 40f;
        public const float BloodVolumeCritical = 30f;

        public const float BleedCritical = 0.134f;
        public const float BleedNotice = 0.02f;

        public const float TempCriticalHigh = 41.5f;
        public const float TempCriticalLow = 27f;
        public const float TempDyingHigh = 41f;
        public const float TempDyingLow = 29f;

        public const float ConsciousnessUnconscious = 30f;
        public const float ConsciousnessCritical = 5f;

        public const float PainNotice = 25f;
        public const float PainWarning = 50f;
        public const float PainSevere = 75f;

        public const float BrainCritical = 25f;
        public const float BrainWarning = 50f;

        public const float RespRatePerMinFactor = 0.25f;

        public const float HappinessCritical = -75f;
        public const float HappinessWarning = -20f;

        public const float EnergyNapThreshold = 35f;

        public const float HungerDying = 10f;
        public const float ThirstDying = 10f;
        public const float ThirstDehydrated = 175f;

        public const float SepticWarning = 75f;
        public const float SepticCritical = 82.5f;

        public const float RadiationCritical = 60f;

        public const float StrokeCritical = 50f;

        public const float FibrillationCritical = 60f;

        public const float ViscosityWarning = 80f;

        public const float ImmunityBaseline = 100f;

        public const float TraumaNotice = 25f;

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

        public static Severity Worse(Severity a, Severity b)
        {
            return a >= b ? a : b;
        }

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
            if (v <= 0f) return Severity.Unknown; 
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

        public static Severity Weight(float offset)
        {
            float abs = Math.Abs(offset);
            if (abs > 55f) return Severity.Critical;
            if (abs > 30f) return Severity.Warning;
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

        public static string Pct(float v)
        {
            return Int0(v) + "%";
        }

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

        public static float Normalize(float v, float min, float max)
        {
            if (max - min <= 0.0001f) return 0f;
            return Clamp01((v - min) / (max - min));
        }

        public static string BleedReadout(float totalBleedSpeed, Func<float, float> toLiters)
        {
            if (totalBleedSpeed <= 0.0001f) return "无出血";
            try
            {
                if (toLiters != null) return Num2(toLiters(totalBleedSpeed) * 60f) + " L/min";
            }
            catch
            {
                
            }
            return Num2(totalBleedSpeed);
        }
    }
}
