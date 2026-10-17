using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CuStatsHud.Core
{
    
    public enum FloatCardKind
    {
        
        Vital = 0,
        
        Limb = 1,
        
        Mate = 2
    }

    public struct FloatCard
    {
        public FloatCardKind Kind;

        public string Key;

        public float X;
        public float Y;

        public float Opacity;

        public float Scale;

        public bool GridSnap;
    }

    public static class FloatCardRules
    {
        
        public const int MaxCards = 12;

        public const float MinOpacity = 0.25f;
        public const float MaxOpacity = 1f;

        public const float MinScale = 0.6f;
        public const float MaxScale = 1.8f;

        public const float SnapDistance = 18f;

        public static float ClampOpacity(float value)
        {
            if (value < MinOpacity) return MinOpacity;
            if (value > MaxOpacity) return MaxOpacity;
            return value;
        }

        public static float ClampScale(float value)
        {
            if (value <= 0f) return 1f;
            if (value < MinScale) return MinScale;
            if (value > MaxScale) return MaxScale;
            return value;
        }

        public static float SnapToGrid(float value, float cell)
        {
            if (cell <= 0f) return value;
            return (float)Math.Round(value / cell) * cell;
        }

        public static bool VisibleInViewport(float itemTop, float itemBottom, float viewHeight, float margin)
        {
            if (itemBottom < -margin) return false;
            if (itemTop > viewHeight + margin) return false;
            return true;
        }

        public static float PlaceAxis(float value, float size, float limit, float snapDistance, float gridCell)
        {
            if (gridCell > 0f)
            {
                float snapped = ClampAxis(SnapToGrid(ClampAxis(value, size, limit), gridCell), size, limit);
                return snapped;
            }
            return SnapAxis(value, size, limit, snapDistance);
        }

        public static float ClampAxis(float value, float size, float limit)
        {
            float max = limit - size;
            if (max <= 0f) return 0f;
            if (value < 0f) return 0f;
            if (value > max) return max;
            return value;
        }

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

        public static bool SameCard(FloatCard a, FloatCard b)
        {
            return a.Kind == b.Kind && string.Equals(a.Key, b.Key, StringComparison.Ordinal);
        }

        public static void DropAt(float pointX, float pointY, float width, float height,
            float screenWidth, float screenHeight, float snapDistance, out float x, out float y)
        {
            x = SnapAxis(pointX - width * 0.5f, width, screenWidth, snapDistance);
            y = SnapAxis(pointY - height * 0.5f, height, screenHeight, snapDistance);
        }

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

        private static string Sanitize(string key)
        {
            return key.Replace('|', '_').Replace(';', '_').Trim();
        }
    }
}
