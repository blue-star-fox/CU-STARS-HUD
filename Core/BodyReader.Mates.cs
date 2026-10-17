using System;
using System.Collections.Generic;
using UnityEngine;

namespace CuStatsHud.Core
{
    internal sealed partial class BodyReader
    {
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

            if (!viaMod)
            {
                Body[] bodies = ScanSceneBodies(false);
                if (bodies == null) return;

                const int FallbackLimit = 12;

                int index = 0;
                for (int i = 0; i < bodies.Length && index < FallbackLimit; i++)
                {
                    Body body = bodies[i];
                    if (body == null || body == local) continue;
                    if (!SafeAlive(body)) continue;

                    string raw = body.gameObject != null ? body.gameObject.name : "?";
                    
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

    }
}
