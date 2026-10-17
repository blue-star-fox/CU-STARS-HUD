using System.Collections.Generic;

namespace CuStatsHud.Core
{
    
    public enum Severity
    {
        Unknown = -1,
        Normal = 0,
        
        Notice = 1,
        
        Warning = 2,
        
        Critical = 3,
    }

    public struct VitalReading
    {
        
        public string Label;
        
        public string Value;
        
        public string Unit;
        
        public string Icon;
        
        public string Group;
        
        public Severity Severity;
        
        public string Limits;
        
        public float Bar;
        
        public bool Secondary;

        public bool ChannelCovered;

        public bool HasBar
        {
            get { return Bar >= 0f; }
        }
    }

    public struct LimbReading
    {
        
        public string Name;
        
        public float Health;
        public float SkinHealth;
        public float MuscleHealth;
        public float Pain;
        
        public float Infection;
        
        public float Bleed;
        
        public float BleedLpm;
        public bool Dismembered;
        public bool Broken;
        public bool Dislocated;
        public bool Splinted;
        public bool BlockedBleeding;
        public bool Infected;
        public int Shrapnel;
        
        public bool IsVital;
        public bool IsHead;
        public bool IsAbdomen;
        public bool IsArm;
        public bool IsLeg;
        
        public Severity Severity;
    }

    public struct MateReading
    {
        public string Name;

        public ulong SteamId;

        public object Avatar;

        public float Distance;
        public bool Alive;
        public bool Conscious;
        public bool Breathing;
        
        public Severity Severity;
        
        public string Summary;
        
        public float BloodBar;
        public float HeartRate;
        public float BloodOxygen;
        public float Consciousness;
        public float Pain;
        
        public string WorstLimb;

        public BodySnapshot Detail;
    }

    public sealed class BodySnapshot
    {
        
        public bool Valid;
        
        public string Error;

        public string PlayerName;
        public bool InMultiplayer;

        public bool Alive;
        public bool Conscious;
        public bool Breathing;
        public bool Sleeping;
        public bool Dying;
        public bool CriticallyDying;
        public bool CardiacArrest;

        public float HeartRate;
        public float BloodOxygen;
        
        public float RespiratoryPerMin;
        public float Fibrillation;

        public Severity Overall;
        
        public string AlertText;
        
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
