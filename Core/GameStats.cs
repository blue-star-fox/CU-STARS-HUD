using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CuStatsHud.Core
{
    /// <summary>
    /// 游戏行为统计。
    ///
    /// 采集方式（都已在反编译源码里核对过）：
    ///   挖掘 / 放置 —— <c>WorldGeneration.SetBlock(pos, block)</c>：block == 0 是挖掉、&gt; 0 是放置；
    ///                  只统计玩家附近的方块，避免世界生成时被计入。
    ///   道具使用   —— <c>Body.UseItem</c> / <c>Body.UseItemInHand</c>（只统计本地玩家）。
    ///   攻击       —— <c>Body.Attack</c>（只统计本地玩家）。
    ///   移动 / 高度 —— 由本地玩家每帧位置累加，单帧位移过大时判定为传送并跳过。
    ///
    /// 无 Unity 依赖，可单元测试。
    /// </summary>
    public sealed class GameStats
    {
        /// <summary>文件格式版本。</summary>
        public const int FormatVersion = 1;

        /// <summary>单帧位移超过这个值就当作传送 / 切场景，不计入距离与高度。</summary>
        public const double TeleportDistance = 40.0;

        // ---------- 会话 ----------
        public int Launches;
        public string FirstUtc;
        public string LastUtc;
        /// <summary>累计游玩时长（秒）。</summary>
        public double PlaySeconds;

        // ---------- 交互类 ----------
        /// <summary>挖掘掉的方块数。</summary>
        public long BlocksDug;
        /// <summary>放置的方块数。</summary>
        public long BlocksPlaced;
        /// <summary>
        /// 使用道具次数（**只含真正动用了物品的调用**）。
        ///
        /// 游戏在没有可用物品时会把手上的动作变成一次空手挥击，那一次不改变任何物品，
        /// 因此**直接丢弃、不计入任何计数**（早先版本把它单列成「空手挥击」，现已去掉）。
        /// 这样「本项总数」与下方按 id 分类的明细之和口径一致。
        /// </summary>
        public long ItemsUsed;
        /// <summary>可交互场景物使用次数（各机器的 OnUse 入口）。</summary>
        public long Interactions;
        /// <summary>容器存 / 取次数。</summary>
        public long ContainerOps;
        /// <summary>向商人购买次数。</summary>
        public long Purchases;

        // ---------- 分类明细（按名字） ----------
        /// <summary>各类道具的使用次数（键是 <c>Item.id</c>）。</summary>
        public readonly Dictionary<string, long> ItemUses = new Dictionary<string, long>(StringComparer.Ordinal);
        /// <summary>各类道具的制作次数（键是配方名）。</summary>
        public readonly Dictionary<string, long> Crafted = new Dictionary<string, long>(StringComparer.Ordinal);

        /// <summary>明细的最大种类数：防止异常情况下无限膨胀。</summary>
        public const int MaxDetailKinds = 200;

        // ---------- 技能 / 经验 ----------
        /// <summary>三围等级（力量 / 韧性 / 智力）。</summary>
        public int LevelSTR;
        public int LevelRES;
        public int LevelINT;
        /// <summary>累计获得的经验（按等级换算后累加，首次观测值为基线）。</summary>
        public double ExpGained;
        /// <summary>累计升级次数。</summary>
        public long LevelUps;
        /// <summary>是否已经建立经验基线（首次读到玩家技能时建立）。</summary>
        private bool _hasSkillBaseline;
        private double _baselineExp;
        private int _lastLevelSum;

        // ---------- 角色数据 ----------
        /// <summary>累计移动距离（含垂直分量）。</summary>
        public double DistanceMoved;
        /// <summary>累计行走距离（只算水平分量）。</summary>
        public double DistanceWalked;
        /// <summary>累计下降高度。</summary>
        public double HeightDescended;
        /// <summary>累计上升高度。</summary>
        public double HeightAscended;
        /// <summary>最大移动速度（单位 / 秒）。</summary>
        public double MaxSpeed;
        /// <summary>最长的一次连续下落高度。</summary>
        public double LongestFall;

        // ---------- 位置锚点 / 下落追踪（不序列化） ----------
        private double _lastX;
        private double _lastY;
        private bool _hasAnchor;
        private double _currentFall;
        private bool _falling;

        /// <summary>平均移动速度（累计距离 / 累计时长）。</summary>
        public double AverageSpeed
        {
            get { return PlaySeconds > 0.5d ? DistanceMoved / PlaySeconds : 0d; }
        }

        // ---------------------------------------------------------------
        // 记录
        // ---------------------------------------------------------------

        public void NoteLaunch(DateTime utcNow)
        {
            Launches++;

            string stamp = utcNow.ToUniversalTime()
                .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(FirstUtc)) FirstUtc = stamp;
            LastUtc = stamp;
        }

        /// <summary>记录一次方块变化。block == 0 视为挖掘，其余视为放置。</summary>
        public void NoteBlock(ushort block)
        {
            if (block == 0) BlocksDug++;
            else BlocksPlaced++;
        }

        /// <summary>记录一次可交互场景物使用（各类机器的 OnUse）。</summary>
        public void NoteInteraction()
        {
            Interactions++;
        }

        /// <summary>记录一次容器存取。</summary>
        public void NoteContainerOp()
        {
            ContainerOps++;
        }

        /// <summary>记录一次购买。</summary>
        public void NotePurchase()
        {
            Purchases++;
        }

        /// <summary>
        /// 记录一次道具使用（带 id，可用于分类）。
        ///
        /// <b>只在确实动用了物品时调用。</b>空手挥击不计数，
        /// 否则「道具使用」总数会掺进空挥，与分类明细之和永久对不上。
        /// </summary>
        public void NoteItemUse(string itemId)
        {
            ItemsUsed++;
            Bump(ItemUses, itemId);
        }

        /// <summary>
        /// 把旧版存档里「无法区分」的历史数据清理掉。
        ///
        /// 旧版把空手挥击也计进了 <see cref="ItemsUsed"/>，但当时没有分类 id，
        /// 因此老数据里 <c>itemsUsed - 明细之和</c> 就是那部分空挥的近似值。
        /// 既然空挥不计数，这个差额就该去掉 —— 把 <see cref="ItemsUsed"/> 收敛到明细之和，
        /// 明细本身原样保留（真实使用过的道具一条都不会丢）。
        ///
        /// 幂等：收敛后差额为 0，再调用不会重复处理。
        /// </summary>
        /// <returns>本次去掉的数量（0 表示无需清理）。</returns>
        public long DropLegacyItemUseGap()
        {
            long detailSum = 0L;
            foreach (KeyValuePair<string, long> pair in ItemUses)
            {
                if (pair.Value > 0L) detailSum += pair.Value;
            }

            if (ItemsUsed <= detailSum) return 0L;

            long gap = ItemsUsed - detailSum;
            ItemsUsed = detailSum;
            return gap;
        }

        /// <summary>记录一次制作，并按配方名分类。</summary>
        public void NoteCraft(string recipeName)
        {
            CraftedTotal++;
            Bump(Crafted, recipeName);
        }

        /// <summary>累计制作次数（明细之和，单独存一份以便显示）。</summary>
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

            // 超过上限就不再记录新种类，但已有种类继续累加
            if (map.Count >= MaxDetailKinds) return;
            map[key] = 1;
        }

        /// <summary>
        /// 把某次观测到的技能状态并入统计。
        ///
        /// 「累计获得经验」不是直接加 <c>exp</c>（那只是当前等级内的进度），
        /// 而是按等级换算成总经验、减去首次观测的基线——这样换存档后基线会重建，
        /// 不会把新角色的初始经验算成"获得"。
        /// </summary>
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

        /// <summary>重置技能基线（换存档 / 重生时调用）。</summary>
        public void ResetSkillBaseline()
        {
            _hasSkillBaseline = false;
            _lastLevelSum = 0;
        }

        /// <summary>取某项明细里次数最多的若干条（降序）。</summary>
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

        /// <summary>
        /// 用本地玩家的当前位置推进移动统计。
        /// 单帧位移超过 <see cref="TeleportDistance"/> 时判定为传送 / 切场景，只重设锚点。
        /// </summary>
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
                // 传送 / 切场景：不计入，并中断下落追踪
                _falling = false;
                _currentFall = 0d;
                return;
            }

            DistanceMoved += step;
            DistanceWalked += Math.Abs(dx);

            if (dy < 0d)
            {
                HeightDescended += -dy;

                // 连续下落：只在持续下降时累加，上升或落地即结算
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

        /// <summary>丢弃位置锚点（切场景 / 读档后调用，避免把跳变算成位移）。</summary>
        public void DropAnchor()
        {
            _hasAnchor = false;
            _falling = false;
            _currentFall = 0d;
        }

        /// <summary>清空全部统计（保留启动次数与时间戳）。</summary>
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

        // ---------------------------------------------------------------
        // 序列化（明文 key=value，写盘前整体加密）
        // ---------------------------------------------------------------

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

        /// <summary>键里不能出现 = 或换行，否则会破坏 key=value 结构。</summary>
        private static string Escape(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            return key.Replace('=', '_').Replace('\n', '_').Replace('\r', '_');
        }

        /// <summary>解析；单行损坏只跳过该行。</summary>
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

                // 分类明细：item.<id> / craft.<名字>
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

        // ---------------------------------------------------------------
        // 展示辅助
        // ---------------------------------------------------------------

        /// <summary>把游戏单位换算成直观的距离文本（1 单位视作 1 米）。</summary>
        public static string FormatDistance(double units)
        {
            if (units < 0d) units = 0d;
            if (units < 1000d) return units.ToString("0.0", CultureInfo.InvariantCulture) + " m";
            return (units / 1000d).ToString("0.00", CultureInfo.InvariantCulture) + " km";
        }

        /// <summary>大数字加千分位。</summary>
        public static string FormatCount(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>速度文本。</summary>
        public static string FormatSpeed(double unitsPerSecond)
        {
            if (unitsPerSecond < 0d) unitsPerSecond = 0d;
            return unitsPerSecond.ToString("0.0", CultureInfo.InvariantCulture) + " u/s";
        }

        /// <summary>秒数格式化成「x 小时 y 分」。</summary>
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
