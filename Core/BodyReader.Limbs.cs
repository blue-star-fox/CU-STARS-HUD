using System;
using System.Collections.Generic;
using UnityEngine;

namespace CuStatsHud.Core
{
    internal sealed partial class BodyReader
    {
        
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
                
                try
                {
                    float lpm = b.bloodToLiters(limb.bleedAmount * limb.bleedSpeedMult) * 60f;
                    r.BleedLpm = limb.blockedBleeding ? 0f : lpm;
                }
                catch (Exception)
                {
                    r.BleedLpm = 0f;
                }
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

    }
}
