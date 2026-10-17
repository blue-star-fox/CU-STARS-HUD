using System;

namespace CuStatsHud.Core
{
    
    public static class Easing
    {
        
        public static float OutCubic(float t)
        {
            t = Clamp01(t);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        public static float InOutCubic(float t)
        {
            t = Clamp01(t);
            if (t < 0.5f) return 4f * t * t * t;

            float f = -2f * t + 2f;
            return 1f - f * f * f * 0.5f;
        }

        public static float Smooth(float deltaTime, float tau)
        {
            if (deltaTime <= 0f) return 0f;
            if (tau <= 0.0001f) return 1f;

            float k = 1f - (float)Math.Exp(-deltaTime / tau);
            return Clamp01(k);
        }

        public static float Approach(float current, float target, float deltaTime, float tau, float snapEpsilon)
        {
            if (Math.Abs(target - current) <= snapEpsilon) return target;

            float k = Smooth(deltaTime, tau);
            float next = current + (target - current) * k;
            return Math.Abs(target - next) <= snapEpsilon ? target : next;
        }

        public static float Clamp01(float value)
        {
            if (float.IsNaN(value)) return 0f;
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
