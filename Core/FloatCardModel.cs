using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CuStatsHud.Core
{
    /// <summary>浮动副本卡片的来源类型。</summary>
    public enum FloatCardKind
    {
        /// <summary>生命体征卡片（单个指标）。</summary>
        Vital = 0,
        /// <summary>肢体卡片（单条肢体）。</summary>
        Limb = 1,
        /// <summary>队友卡片（一名玩家）。</summary>
        Mate = 2
    }

    /// <summary>一张浮动副本卡片：来源 + 屏幕坐标 + 自己的透明度 / 大小 / 摆放方式。</summary>
    public struct FloatCard
    {
        public FloatCardKind Kind;

        /// <summary>来源标识：指标名 / 肢体短名 / 队友名。</summary>
        public string Key;

        /// <summary>屏幕坐标（左上角）。</summary>
        public float X;
        public float Y;

        /// <summary>该副本自己的不透明度，与主面板、与其它副本互不影响。</summary>
        public float Opacity;

        /// <summary>尺寸倍率（相对该卡片的自然尺寸，1 = 自然大小）。</summary>
        public float Scale;

        /// <summary>是否按方格摆放：开启后位置严格遵守网格，且不再做屏幕边缘吸附。</summary>
        public bool GridSnap;
    }

    /// <summary>
    /// 浮动卡片的纯逻辑：序列化、边界约束、边缘吸附。
    /// 无 Unity 依赖，可单元测试。
    /// </summary>
    public static class FloatCardRules
    {
        /// <summary>副本数量上限（避免把屏幕铺满、也避免配置无限膨胀）。</summary>
        public const int MaxCards = 12;

        public const float MinOpacity = 0.25f;
        public const float MaxOpacity = 1f;

        /// <summary>组件卡尺寸倍率范围。</summary>
        public const float MinScale = 0.6f;
        public const float MaxScale = 1.8f;

        /// <summary>拖动时与屏幕边缘的距离小于该值就吸附（未缩放像素）。</summary>
        public const float SnapDistance = 18f;

        public static float ClampOpacity(float value)
        {
            if (value < MinOpacity) return MinOpacity;
            if (value > MaxOpacity) return MaxOpacity;
            return value;
        }

        /// <summary>尺寸倍率；0 或负数（旧配置没有这个字段）视为自然尺寸。</summary>
        public static float ClampScale(float value)
        {
            if (value <= 0f) return 1f;
            if (value < MinScale) return MinScale;
            if (value > MaxScale) return MaxScale;
            return value;
        }

        /// <summary>把坐标吸附到方格：取最近的格点。</summary>
        public static float SnapToGrid(float value, float cell)
        {
            if (cell <= 0f) return value;
            return (float)Math.Round(value / cell) * cell;
        }

        /// <summary>
        /// 滚动列表的视口可见性判断（用于跳过视口外的条目）。
        ///
        /// 传入的必须是**组内坐标**（0 = 视口顶部，向下为正）。
        /// 这一点很容易搞错：Layout 在滚动时以 <c>-offset</c> 作为起始 y，
        /// 条目坐标本身就是组内坐标；若误用内容坐标（offset ~ offset+height）去比较，
        /// 滚到列表下半段时整表都会被判成“视口外”而消失。
        /// </summary>
        public static bool VisibleInViewport(float itemTop, float itemBottom, float viewHeight, float margin)
        {
            if (itemBottom < -margin) return false;
            if (itemTop > viewHeight + margin) return false;
            return true;
        }

        /// <summary>
        /// 组件卡落点/拖动位置的统一入口。
        ///
        /// <paramref name="gridCell"/> &gt; 0 表示「按方格摆放」：位置严格遵守网格，
        /// 不再做屏幕边缘吸附（用户要的是“严格遵守方格”）；两种模式下都会夹进屏幕内。
        /// </summary>
        public static float PlaceAxis(float value, float size, float limit, float snapDistance, float gridCell)
        {
            if (gridCell > 0f)
            {
                float snapped = ClampAxis(SnapToGrid(ClampAxis(value, size, limit), gridCell), size, limit);
                return snapped;
            }
            return SnapAxis(value, size, limit, snapDistance);
        }

        /// <summary>
        /// 把坐标夹进屏幕内。屏幕比卡片还小时贴 0（保证整卡可见，而不是越界）。
        /// </summary>
        public static float ClampAxis(float value, float size, float limit)
        {
            float max = limit - size;
            if (max <= 0f) return 0f;
            if (value < 0f) return 0f;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// 先夹进屏幕，再在靠近屏幕边缘时吸附上去。
        /// 吸附结果本身就在界内，所以不会被又被推出去。
        /// </summary>
        public static float SnapAxis(float value, float size, float limit, float snapDistance)
        {
            value = ClampAxis(value, size, limit);

            float max = limit - size;
            if (max <= 0f) return 0f;
            if (snapDistance <= 0f) return value;

            if (value <= snapDistance) return 0f;
            if (max - value <= snapDistance) return max;
            return value;
        }

        /// <summary>两张副本是否指向同一来源（避免同一张卡片被拖出多份）。</summary>
        public static bool SameCard(FloatCard a, FloatCard b)
        {
            return a.Kind == b.Kind && string.Equals(a.Key, b.Key, StringComparison.Ordinal);
        }

        /// <summary>
        /// 拖拽落点 → 卡片左上角坐标。
        /// 以拖拽点为中心放置（从侧栏拖出来时更符合直觉），随后夹进屏幕并吸附边缘。
        /// </summary>
        public static void DropAt(float pointX, float pointY, float width, float height,
            float screenWidth, float screenHeight, float snapDistance, out float x, out float y)
        {
            x = SnapAxis(pointX - width * 0.5f, width, screenWidth, snapDistance);
            y = SnapAxis(pointY - height * 0.5f, height, screenHeight, snapDistance);
        }

        /// <summary>
        /// 序列化为配置字符串：<c>kind|key|x|y|opacity|scale|gridSnap</c>，多条用 <c>;</c> 分隔。
        /// 数字一律用 InvariantCulture，避免某些区域设置的小数点差异把配置写坏。
        /// </summary>
        public static string Serialize(IList<FloatCard> cards)
        {
            if (cards == null || cards.Count == 0) return string.Empty;

            StringBuilder sb = new StringBuilder();
            int count = Math.Min(cards.Count, MaxCards);
            for (int i = 0; i < count; i++)
            {
                FloatCard card = cards[i];
                if (string.IsNullOrEmpty(card.Key)) continue;

                if (sb.Length > 0) sb.Append(';');
                sb.Append((int)card.Kind).Append('|')
                    .Append(Sanitize(card.Key)).Append('|')
                    .Append(card.X.ToString("0.#", CultureInfo.InvariantCulture)).Append('|')
                    .Append(card.Y.ToString("0.#", CultureInfo.InvariantCulture)).Append('|')
                    .Append(ClampOpacity(card.Opacity).ToString("0.##", CultureInfo.InvariantCulture)).Append('|')
                    .Append(ClampScale(card.Scale).ToString("0.##", CultureInfo.InvariantCulture)).Append('|')
                    .Append(card.GridSnap ? '1' : '0');
            }

            return sb.ToString();
        }

        /// <summary>
        /// 解析配置字符串，结果追加到 <paramref name="into"/>（先清空）。
        /// 单条损坏只跳过该条，不影响其它组件卡。
        /// 兼容早期只有前 5 个字段的写法（缺少的按自然尺寸 / 不吸网格处理）。
        /// </summary>
        public static void Deserialize(string text, List<FloatCard> into)
        {
            if (into == null) return;
            into.Clear();
            if (string.IsNullOrEmpty(text)) return;

            string[] parts = text.Split(';');
            for (int i = 0; i < parts.Length && into.Count < MaxCards; i++)
            {
                string part = parts[i];
                if (string.IsNullOrEmpty(part)) continue;

                string[] fields = part.Split('|');
                if (fields.Length < 5) continue;

                int kindValue;
                if (!int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out kindValue)) continue;
                if (kindValue < 0 || kindValue > 2) continue;

                string key = fields[1];
                if (string.IsNullOrEmpty(key)) continue;

                float x;
                float y;
                float opacity;
                if (!float.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out x)) continue;
                if (!float.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) continue;
                if (!float.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out opacity))
                {
                    opacity = MaxOpacity;
                }

                FloatCard card;
                card.Kind = (FloatCardKind)kindValue;
                card.Key = key;
                card.X = x;
                card.Y = y;
                card.Opacity = ClampOpacity(opacity);
                card.Scale = 1f;
                card.GridSnap = false;

                float scale;
                if (fields.Length >= 6
                    && float.TryParse(fields[5], NumberStyles.Float, CultureInfo.InvariantCulture, out scale))
                {
                    card.Scale = ClampScale(scale);
                }

                if (fields.Length >= 7)
                {
                    string flag = fields[6].Trim();
                    card.GridSnap = flag == "1" || string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
                }

                into.Add(card);
            }
        }

        /// <summary>配置里的字段分隔符不能在 key 里出现。</summary>
        private static string Sanitize(string key)
        {
            return key.Replace('|', '_').Replace(';', '_').Trim();
        }
    }
}
