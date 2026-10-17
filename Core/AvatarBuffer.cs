using System;

namespace CuStatsHud.Core
{
    
    public struct Rgba32
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public Rgba32(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }
    }

    public static class AvatarBuffer
    {
        
        private const float RingWidth = 1.2f;
        private const float RingStrength = 0.28f;

        public static int Index(int width, int x, int y)
        {
            return y * width + x;
        }

        public static Rgba32[] MakeCircular(Rgba32[] source, int sourceWidth, int sourceHeight, int size)
        {
            if (source == null || size <= 0) return null;
            if (sourceWidth <= 0 || sourceHeight <= 0) return null;
            if (source.Length < sourceWidth * sourceHeight) return null;

            Rgba32[] destination = new Rgba32[size * size];
            float center = (size - 1) * 0.5f;
            float radius = center + 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                    float coverage = radius - distance + 0.5f;
                    if (coverage <= 0f)
                    {
                        destination[Index(size, x, y)] = new Rgba32(0, 0, 0, 0);
                        continue;
                    }
                    if (coverage > 1f) coverage = 1f;

                    float sampleX = (x + 0.5f) * sourceWidth / size - 0.5f;
                    float sampleY = (y + 0.5f) * sourceHeight / size - 0.5f;
                    Rgba32 pixel = SampleBilinear(source, sourceWidth, sourceHeight, sampleX, sampleY);

                    if (distance > radius - RingWidth)
                    {
                        pixel = Mix(pixel, new Rgba32(255, 255, 255, 255), RingStrength);
                    }

                    pixel.A = (byte)(pixel.A * coverage);
                    destination[Index(size, x, y)] = pixel;
                }
            }

            return destination;
        }

        public static Rgba32 SampleBilinear(Rgba32[] source, int width, int height, float x, float y)
        {
            if (x < 0f) x = 0f;
            if (y < 0f) y = 0f;
            if (x > width - 1f) x = width - 1f;
            if (y > height - 1f) y = height - 1f;

            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = Math.Min(x0 + 1, width - 1);
            int y1 = Math.Min(y0 + 1, height - 1);
            float fx = x - x0;
            float fy = y - y0;

            Rgba32 c00 = source[Index(width, x0, y0)];
            Rgba32 c10 = source[Index(width, x1, y0)];
            Rgba32 c01 = source[Index(width, x0, y1)];
            Rgba32 c11 = source[Index(width, x1, y1)];

            return new Rgba32(
                Lerp4(c00.R, c10.R, c01.R, c11.R, fx, fy),
                Lerp4(c00.G, c10.G, c01.G, c11.G, fx, fy),
                Lerp4(c00.B, c10.B, c01.B, c11.B, fx, fy),
                Lerp4(c00.A, c10.A, c01.A, c11.A, fx, fy));
        }

        private static byte Lerp4(byte v00, byte v10, byte v01, byte v11, float fx, float fy)
        {
            float top = v00 + (v10 - v00) * fx;
            float bottom = v01 + (v11 - v01) * fx;
            float value = top + (bottom - top) * fy;
            if (value < 0f) value = 0f;
            if (value > 255f) value = 255f;
            return (byte)value;
        }

        public static Rgba32 Mix(Rgba32 from, Rgba32 over, float t)
        {
            if (t <= 0f) return from;
            if (t >= 1f) return over;

            return new Rgba32(
                (byte)(from.R + (over.R - from.R) * t),
                (byte)(from.G + (over.G - from.G) * t),
                (byte)(from.B + (over.B - from.B) * t),
                (byte)(from.A + (over.A - from.A) * t));
        }
    }
}
