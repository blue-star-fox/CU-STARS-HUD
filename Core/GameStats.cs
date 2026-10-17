using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CuStatsHud.Core
{
    
    public sealed class GameStats
    {
        
        public const int FormatVersion = 1;

        public const double TeleportDistance = 40.0;

        public int Launches;
        public string FirstUtc;
        public string LastUtc;
        
        public double PlaySeconds;

        public long BlocksDug;
        
        public long BlocksPlaced;
        
        public long ItemsUsed;
        
        public long Interactions;
        
        public long ContainerOps;
        
        public long Purchases;

        public readonly Dictionary<string, long> ItemUses = new Dictionary<string, long>(StringComparer.Ordinal);
        
        public readonly Dictionary<string, long> Crafted = new Dictionary<string, long>(StringComparer.Ordinal);

        public const int MaxDetailKinds = 200;

        public int LevelSTR;
        public int LevelRES;
        public int LevelINT;
        
        public double ExpGained;
        
        public long LevelUps;
        
        private bool _hasSkillBaseline;
        private double _baselineExp;
        private int _lastLevelSum;

        public double DistanceMoved;
        
        public double DistanceWalked;
        
        public double HeightDescended;
        
        public double HeightAscended;
        
        public double MaxSpeed;
        
        public double LongestFall;

        private double _lastX;
        private double _lastY;
        private bool _hasAnchor;
        private double _currentFall;
        private bool _falling;

        public double AverageSpeed
        {
            get { return PlaySeconds > 0.5d ? DistanceMoved / PlaySeconds : 0d; }
        }

        public void NoteLaunch(DateTime utcNow)
        {
            Launches++;

            string stamp = utcNow.ToUniversalTime()
                .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(FirstUtc)) FirstUtc = stamp;
            LastUtc = stamp;
        }

        public void NoteBlock(ushort block)
        {
            if (block == 0) BlocksDug++;
            else BlocksPlaced++;
        }

        public void NoteItemUse()
        {
            ItemsUsed++;
        }

        public void NoteInteraction()
        {
            Interactions++;
        }

        public void NoteContainerOp()
        {
            ContainerOps++;
        }

        public void NotePurchase()
        {
            Purchases++;
        }

        public void NoteItemUse(string itemId)
        {
            ItemsUsed++;
            Bump(ItemUses, itemId);
        }

        public void NoteCraft(string recipeName)
        {
            CraftedTotal++;
            Bump(Crafted, recipeName);
        }

        public long CraftedTotal;

        private static void Bump(Dictionary<string, long> map, string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            long current;
            if (map.TryGetValue(key, out current))
            {
                map[key] = current + 1;
                return;
            }

            if (map.Count >= MaxDetailKinds) return;
            map[key] = 1;
        }

        public void NoteSkills(int str, int res, int inte, double totalExp, bool newBaseline)
        {
            LevelSTR = str;
            LevelRES = res;
            LevelINT = inte;

            if (newBaseline || !_hasSkillBaseline)
            {
                _hasSkillBaseline = true;
                _baselineExp = totalExp;
                _lastLevelSum = str + res + inte;
                return;
            }

            int levelSum = str + res + inte;
            if (levelSum > _lastLevelSum) LevelUps += levelSum - _lastLevelSum;
            _lastLevelSum = levelSum;

            double gained = totalExp - _baselineExp;
            if (gained > 0d) ExpGained = gained;
        }

        public void ResetSkillBaseline()
        {
            _hasSkillBaseline = false;
            _lastLevelSum = 0;
        }

        public static List<KeyValuePair<string, long>> TopOf(Dictionary<string, long> map, int count)
        {
            List<KeyValuePair<string, long>> all =
                new List<KeyValuePair<string, long>>(map);
            all.Sort(delegate (KeyValuePair<string, long> a, KeyValuePair<string, long> b)
            {
                int byCount = b.Value.CompareTo(a.Value);
                return byCount != 0 ? byCount : string.CompareOrdinal(a.Key, b.Key);
            });

            if (all.Count > count) all.RemoveRange(count, all.Count - count);
            return all;
        }

        public void TickMovement(double x, double y, double deltaSeconds)
        {
            if (deltaSeconds > 0d) PlaySeconds += deltaSeconds;

            if (!_hasAnchor)
            {
                _lastX = x;
                _lastY = y;
                _hasAnchor = true;
                return;
            }

            double dx = x - _lastX;
            double dy = y - _lastY;
            double step = Math.Sqrt(dx * dx + dy * dy);

            _lastX = x;
            _lastY = y;

            if (step > TeleportDistance)
            {
                
                _falling = false;
                _currentFall = 0d;
                return;
            }

            DistanceMoved += step;
            DistanceWalked += Math.Abs(dx);

            if (dy < 0d)
            {
                HeightDescended += -dy;

                if (!_falling)
                {
                    _falling = true;
                    _currentFall = 0d;
                }
                _currentFall += -dy;
                if (_currentFall > LongestFall) LongestFall = _currentFall;
            }
            else
            {
                HeightAscended += dy;
                _falling = false;
                _currentFall = 0d;
            }

            if (deltaSeconds > 0.0001d)
            {
                double speed = step / deltaSeconds;
                if (speed > MaxSpeed) MaxSpeed = speed;
            }
        }

        public void DropAnchor()
        {
            _hasAnchor = false;
            _falling = false;
            _currentFall = 0d;
        }

        public void ResetAll()
        {
            BlocksDug = 0;
            BlocksPlaced = 0;
            ItemsUsed = 0;
            Interactions = 0;
            ContainerOps = 0;
            Purchases = 0;
            DistanceMoved = 0d;
            DistanceWalked = 0d;
            HeightDescended = 0d;
            HeightAscended = 0d;
            MaxSpeed = 0d;
            LongestFall = 0d;
            CraftedTotal = 0;
            ExpGained = 0d;
            LevelUps = 0;
            ItemUses.Clear();
            Crafted.Clear();
            ResetSkillBaseline();
            DropAnchor();
        }

        public string Serialize()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("v=").Append(FormatVersion).Append('\n');
            sb.Append("launches=").Append(Launches).Append('\n');
            sb.Append("first=").Append(FirstUtc ?? string.Empty).Append('\n');
            sb.Append("last=").Append(LastUtc ?? string.Empty).Append('\n');
            sb.Append("playSeconds=").Append(Num(PlaySeconds)).Append('\n');
            sb.Append("blocksDug=").Append(BlocksDug).Append('\n');
            sb.Append("blocksPlaced=").Append(BlocksPlaced).Append('\n');
            sb.Append("itemsUsed=").Append(ItemsUsed).Append('\n');
            sb.Append("interactions=").Append(Interactions).Append('\n');
            sb.Append("containerOps=").Append(ContainerOps).Append('\n');
            sb.Append("purchases=").Append(Purchases).Append('\n');
            sb.Append("distance=").Append(Num(DistanceMoved)).Append('\n');
            sb.Append("walked=").Append(Num(DistanceWalked)).Append('\n');
            sb.Append("descended=").Append(Num(HeightDescended)).Append('\n');
            sb.Append("ascended=").Append(Num(HeightAscended)).Append('\n');
            sb.Append("maxSpeed=").Append(Num(MaxSpeed)).Append('\n');
            sb.Append("longestFall=").Append(Num(LongestFall)).Append('\n');
            sb.Append("craftedTotal=").Append(CraftedTotal).Append('\n');
            sb.Append("levelSTR=").Append(LevelSTR).Append('\n');
            sb.Append("levelRES=").Append(LevelRES).Append('\n');
            sb.Append("levelINT=").Append(LevelINT).Append('\n');
            sb.Append("expGained=").Append(Num(ExpGained)).Append('\n');
            sb.Append("levelUps=").Append(LevelUps).Append('\n');

            AppendMap(sb, "item", ItemUses);
            AppendMap(sb, "craft", Crafted);
            return sb.ToString();
        }

        private static void AppendMap(StringBuilder sb, string prefix, Dictionary<string, long> map)
        {
            foreach (KeyValuePair<string, long> pair in map)
            {
                sb.Append(prefix).Append('.').Append(Escape(pair.Key)).Append('=').Append(pair.Value).Append('\n');
            }
        }

        private static string Escape(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            return key.Replace('=', '_').Replace('\n', '_').Replace('\r', '_');
        }

        public void Deserialize(string text)
        {
            ItemUses.Clear();
            Crafted.Clear();

            if (string.IsNullOrEmpty(text)) return;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length == 0) continue;

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();

                if (key.StartsWith("item.", StringComparison.Ordinal))
                {
                    PutDetail(ItemUses, key.Substring(5), value);
                    continue;
                }
                if (key.StartsWith("craft.", StringComparison.Ordinal))
                {
                    PutDetail(Crafted, key.Substring(6), value);
                    continue;
                }

                Apply(key, value);
            }
        }

        private static void PutDetail(Dictionary<string, long> map, string name, string value)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (map.Count >= MaxDetailKinds) return;

            long parsed;
            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) return;
            if (parsed <= 0L) return;

            map[name] = parsed;
        }

        private void Apply(string key, string value)
        {
            switch (key)
            {
                case "launches": Launches = (int)Long(value, Launches); return;
                case "first": FirstUtc = value; return;
                case "last": LastUtc = value; return;
                case "playSeconds": PlaySeconds = Double(value, PlaySeconds); return;
                case "blocksDug": BlocksDug = Long(value, BlocksDug); return;
                case "blocksPlaced": BlocksPlaced = Long(value, BlocksPlaced); return;
                case "itemsUsed": ItemsUsed = Long(value, ItemsUsed); return;
                case "interactions": Interactions = Long(value, Interactions); return;
                case "containerOps": ContainerOps = Long(value, ContainerOps); return;
                case "purchases": Purchases = Long(value, Purchases); return;
                case "distance": DistanceMoved = Double(value, DistanceMoved); return;
                case "walked": DistanceWalked = Double(value, DistanceWalked); return;
                case "descended": HeightDescended = Double(value, HeightDescended); return;
                case "ascended": HeightAscended = Double(value, HeightAscended); return;
                case "maxSpeed": MaxSpeed = Double(value, MaxSpeed); return;
                case "longestFall": LongestFall = Double(value, LongestFall); return;
                case "craftedTotal": CraftedTotal = Long(value, CraftedTotal); return;
                case "levelSTR": LevelSTR = (int)Long(value, LevelSTR); return;
                case "levelRES": LevelRES = (int)Long(value, LevelRES); return;
                case "levelINT": LevelINT = (int)Long(value, LevelINT); return;
                case "expGained": ExpGained = Double(value, ExpGained); return;
                case "levelUps": LevelUps = Long(value, LevelUps); return;
            }
        }

        private static string Num(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static long Long(string text, long fallback)
        {
            long parsed;
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed : fallback;
        }

        private static double Double(string text, double fallback)
        {
            double parsed;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                ? parsed : fallback;
        }

        public static string FormatDistance(double units)
        {
            if (units < 0d) units = 0d;
            if (units < 1000d) return units.ToString("0.0", CultureInfo.InvariantCulture) + " m";
            return (units / 1000d).ToString("0.00", CultureInfo.InvariantCulture) + " km";
        }

        public static string FormatCount(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        public static string FormatSpeed(double unitsPerSecond)
        {
            if (unitsPerSecond < 0d) unitsPerSecond = 0d;
            return unitsPerSecond.ToString("0.0", CultureInfo.InvariantCulture) + " m/s";
        }

        public static string FormatDuration(double seconds)
        {
            if (seconds < 0d) seconds = 0d;

            long total = (long)Math.Floor(seconds);
            long hours = total / 3600L;
            long minutes = (total % 3600L) / 60L;
            long secs = total % 60L;

            if (hours > 0) return hours + " 小时 " + minutes + " 分";
            if (minutes > 0) return minutes + " 分 " + secs + " 秒";
            return secs + " 秒";
        }
    }
}
