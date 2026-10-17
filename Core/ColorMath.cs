using System;

namespace CuStatsHud.Core
{
    
    public static class ColorMath
    {
        
        public static void HsvToRgb(float h, float s, float v, out float r, out float g, out float b)
        {
            h = Wrap01(h);
            s = Clamp01(s);
            v = Clamp01(v);

            if (s <= 0.00001f)
            {
                r = g = b = v;
                return;
            }

            float sector = h * 6f;
            int index = (int)Math.Floor(sector);
            if (index >= 6) index = 5;

            float f = sector - index;
            float p = v * (1f - s);
            float q = v * (1f - s * f);
            float t = v * (1f - s * (1f - f));

            switch (index)
            {
                case 0: r = v; g = t; b = p; return;
                case 1: r = q; g = v; b = p; return;
                case 2: r = p; g = v; b = t; return;
                case 3: r = p; g = q; b = v; return;
                case 4: r = t; g = p; b = v; return;
                default: r = v; g = p; b = q; return;
            }
        }

        public static float Wrap01(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            value -= (float)Math.Floor(value);
            return value < 0f ? value + 1f : value;
        }

        public static float Clamp01(float value)
        {
            if (float.IsNaN(value)) return 0f;
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        public static float BaseValue(float dark, float step)
        {
            
            return 0.055f + 0.285f * Clamp01(dark) * Clamp01(step);
        }

        public static float BaseSaturation(float saturation, float weight)
        {
            return 0.06f + 0.16f * Clamp01(saturation) * Clamp01(weight);
        }
    }
}
