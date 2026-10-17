using UnityEngine;

namespace CuStatsHud.UI
{
    
    internal static class TextureFactory
    {
        private static Texture2D New(int w, int h, FilterMode filter)
        {
            Texture2D tex = new Texture2D(Mathf.Max(1, w), Mathf.Max(1, h), TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = filter;
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        public static Texture2D Solid(Color color)
        {
            Texture2D tex = New(1, 1, FilterMode.Point);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        public static Texture2D Circle(int diameter, Color color)
        {
            diameter = Mathf.Max(4, diameter);

            Texture2D tex = New(diameter, diameter, FilterMode.Bilinear);
            Color32[] pixels = new Color32[diameter * diameter];
            float half = (diameter - 1) * 0.5f;
            float radius = half;

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x - half;
                    float dy = y - half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    float alpha = Mathf.Clamp01(0.5f - dist);
                    pixels[y * diameter + x] = new Color(color.r, color.g, color.b, color.a * alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D Rounded(int size, int radius, Color color)
        {
            size = Mathf.Max(8, size);
            radius = Mathf.Clamp(radius, 1, size / 2 - 1);

            Texture2D tex = New(size, size, FilterMode.Bilinear);
            Color32[] pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            float inner = half - radius;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x - half) - inner;
                    float qy = Mathf.Abs(y - half) - inner;
                    float ox = Mathf.Max(qx, 0f);
                    float oy = Mathf.Max(qy, 0f);
                    float dist = Mathf.Min(Mathf.Max(qx, qy), 0f) + Mathf.Sqrt(ox * ox + oy * oy) - radius;
                    float alpha = Mathf.Clamp01(0.5f - dist);
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D RoundedOutline(int size, int radius, float thickness, Color color)
        {
            size = Mathf.Max(8, size);
            radius = Mathf.Clamp(radius, 1, size / 2 - 1);
            thickness = Mathf.Clamp(thickness, 1f, radius);

            Texture2D tex = New(size, size, FilterMode.Bilinear);
            Color32[] pixels = new Color32[size * size];

            float half = (size - 1) * 0.5f;
            float insetHalf = half - thickness;
            float insetRadius = Mathf.Max(0f, radius - thickness);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float outer = SdRounded(x, y, half, half - radius, radius);
                    float inner = SdRounded(x, y, insetHalf, Mathf.Max(0f, insetHalf - insetRadius), insetRadius);

                    float alpha = Mathf.Clamp01(0.5f - outer) - Mathf.Clamp01(0.5f - inner);
                    if (alpha < 0f) alpha = 0f;

                    pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static float SdRounded(float x, float y, float center, float inner, float radius)
        {
            float qx = Mathf.Abs(x - center) - inner;
            float qy = Mathf.Abs(y - center) - inner;
            float ox = Mathf.Max(qx, 0f);
            float oy = Mathf.Max(qy, 0f);
            return Mathf.Min(Mathf.Max(qx, qy), 0f) + Mathf.Sqrt(ox * ox + oy * oy) - radius;
        }

        public static Texture2D Capsule(int w, int h, Color color)
        {
            w = Mathf.Max(4, w);
            h = Mathf.Max(4, h);

            Texture2D tex = New(w, h, FilterMode.Bilinear);
            Color32[] pixels = new Color32[w * h];
            float hx = (w - 1) * 0.5f;
            float hy = (h - 1) * 0.5f;
            float radius = Mathf.Min(hx, hy);
            float innerX = hx - radius;
            float innerY = hy - radius;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float qx = Mathf.Abs(x - hx) - innerX;
                    float qy = Mathf.Abs(y - hy) - innerY;
                    float ox = Mathf.Max(qx, 0f);
                    float oy = Mathf.Max(qy, 0f);
                    float dist = Mathf.Min(Mathf.Max(qx, qy), 0f) + Mathf.Sqrt(ox * ox + oy * oy) - radius;
                    float alpha = Mathf.Clamp01(0.5f - dist);
                    pixels[y * w + x] = new Color(color.r, color.g, color.b, color.a * alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D Grid(int cell, Color line)
        {
            cell = Mathf.Clamp(cell, 2, 64);

            Texture2D tex = New(cell, cell, FilterMode.Point);
            Color32[] pixels = new Color32[cell * cell];

            for (int y = 0; y < cell; y++)
            {
                for (int x = 0; x < cell; x++)
                {
                    bool onLine = x == 0 || y == 0;
                    pixels[y * cell + x] = onLine ? (Color32)line : new Color32(0, 0, 0, 0);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D VerticalGradient(int height, Color top, Color bottom)
        {
            height = Mathf.Max(2, height);
            Texture2D tex = New(1, height, FilterMode.Bilinear);
            for (int y = 0; y < height; y++)
            {
                float t = height == 1 ? 0f : (float)y / (height - 1);
                
                tex.SetPixel(0, y, Color.Lerp(bottom, top, t));
            }
            tex.Apply();
            return tex;
        }

        public static Texture2D Glow(int size, Color color)
        {
            size = Mathf.Clamp(size, 8, 256);
            Texture2D tex = New(size, size, FilterMode.Bilinear);
            Color32[] pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D Hairline(int width, Color color)
        {
            width = Mathf.Clamp(width, 2, 512);
            Texture2D tex = New(width, 1, FilterMode.Point);
            Color32[] pixels = new Color32[width];
            for (int i = 0; i < width; i++) pixels[i] = color;
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public static void Destroy(ref Texture2D tex)
        {
            if (tex == null) return;
            Object.Destroy(tex);
            tex = null;
        }
    }
}
