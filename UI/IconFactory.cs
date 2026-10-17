using System.Collections.Generic;
using UnityEngine;

namespace CuStatsHud.UI
{
    
    internal sealed class IconFactory
    {
        private const float Design = 16f;
        private const int SubSamples = 3;

        private readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        private readonly int _size;

        public IconFactory(int pixelSize)
        {
            _size = Mathf.Clamp(pixelSize, 12, 34);
        }

        public Texture2D Get(string kind, Color color)
        {
            if (string.IsNullOrEmpty(kind)) return null;

            string key = kind + "|" + ((int)(color.r * 255f)) + "_" + ((int)(color.g * 255f)) + "_"
                + ((int)(color.b * 255f)) + "_" + ((int)(color.a * 255f)) + "|" + _size;

            Texture2D cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            Texture2D tex = Build(kind, color);
            _cache[key] = tex;
            return tex;
        }

        public void Dispose()
        {
            foreach (KeyValuePair<string, Texture2D> pair in _cache)
            {
                if (pair.Value != null) Object.Destroy(pair.Value);
            }
            _cache.Clear();
        }

        private Texture2D Build(string kind, Color color)
        {
            Texture2D tex = new Texture2D(_size, _size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;

            Color32[] pixels = new Color32[_size * _size];
            float step = 1f / SubSamples;
            float sampleWeight = 1f / (SubSamples * SubSamples);
            float unit = Design / _size;

            for (int y = 0; y < _size; y++)
            {
                for (int x = 0; x < _size; x++)
                {
                    float coverage = 0f;
                    for (int sy = 0; sy < SubSamples; sy++)
                    {
                        float py = (y + (sy + 0.5f) * step) * unit;
                        for (int sx = 0; sx < SubSamples; sx++)
                        {
                            float px = (x + (sx + 0.5f) * step) * unit;
                            if (Shape(kind, px, py)) coverage += sampleWeight;
                        }
                    }

                    pixels[y * _size + x] = new Color(color.r, color.g, color.b, color.a * coverage);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static bool Circle(float u, float v, float cx, float cy, float r)
        {
            float dx = u - cx;
            float dy = v - cy;
            return dx * dx + dy * dy <= r * r;
        }

        private static bool Ellipse(float u, float v, float cx, float cy, float rx, float ry)
        {
            float dx = (u - cx) / rx;
            float dy = (v - cy) / ry;
            return dx * dx + dy * dy <= 1f;
        }

        private static bool Annulus(float u, float v, float cx, float cy, float inner, float outer)
        {
            float dx = u - cx;
            float dy = v - cy;
            float d2 = dx * dx + dy * dy;
            return d2 >= inner * inner && d2 <= outer * outer;
        }

        private static bool Rect(float u, float v, float x0, float y0, float x1, float y1)
        {
            return u >= x0 && u <= x1 && v >= y0 && v <= y1;
        }

        private static bool Triangle(float px, float py, float x1, float y1, float x2, float y2, float x3, float y3)
        {
            float d1 = (px - x2) * (y1 - y2) - (x1 - x2) * (py - y2);
            float d2 = (px - x3) * (y2 - y3) - (x2 - x3) * (py - y3);
            float d3 = (px - x1) * (y3 - y1) - (x3 - x1) * (py - y1);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        private static bool Segment(float u, float v, float x1, float y1, float x2, float y2, float thickness)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float lengthSq = dx * dx + dy * dy;
            float t = 0f;
            if (lengthSq > 0.0001f)
            {
                t = ((u - x1) * dx + (v - y1) * dy) / lengthSq;
                t = t < 0f ? 0f : (t > 1f ? 1f : t);
            }
            float px = x1 + t * dx;
            float py = y1 + t * dy;
            float ex = u - px;
            float ey = v - py;
            return ex * ex + ey * ey <= thickness * thickness;
        }

        private static float AngleDeg(float u, float v, float cx, float cy)
        {
            return Mathf.Atan2(-(v - cy), u - cx) * Mathf.Rad2Deg;
        }

        private static bool AngleNear(float angle, float target, float tolerance)
        {
            float diff = Mathf.Abs(Mathf.DeltaAngle(angle, target));
            return diff <= tolerance;
        }

        private static bool Shape(string kind, float u, float v)
        {
            switch (kind)
            {
                case "heart":
                    if (Circle(u, v, 5.4f, 5.2f, 3.3f)) return true;
                    if (Circle(u, v, 10.6f, 5.2f, 3.3f)) return true;
                    return Triangle(u, v, 2.2f, 6.2f, 13.8f, 6.2f, 8f, 14.6f);

                case "drop":
                    if (Circle(u, v, 8f, 10.4f, 4.2f)) return true;
                    return Triangle(u, v, 4.7f, 9.2f, 11.3f, 9.2f, 8f, 1.2f);

                case "bolt":
                    if (Triangle(u, v, 9.6f, 1f, 4.2f, 9.4f, 9.6f, 9.4f)) return true;
                    return Triangle(u, v, 6.4f, 15f, 11.8f, 6.8f, 6.4f, 6.8f);

                case "moon":
                    return Circle(u, v, 8f, 8f, 6.6f) && !Circle(u, v, 11.6f, 5.4f, 6.1f);

                case "thermo":
                    if (Circle(u, v, 8f, 11.8f, 3.2f)) return true;
                    return Rect(u, v, 6.6f, 1.4f, 9.4f, 11.8f);

                case "cross":
                    return Rect(u, v, 5.8f, 1.4f, 10.2f, 14.6f) || Rect(u, v, 1.4f, 5.8f, 14.6f, 10.2f);

                case "close":
                    
                    if (Segment(u, v, 3.6f, 3.6f, 12.4f, 12.4f, 1.35f)) return true;
                    return Segment(u, v, 12.4f, 3.6f, 3.6f, 12.4f, 1.35f);

                case "brain":
                    if (!(Circle(u, v, 8f, 7.4f, 5.3f) || Rect(u, v, 6.8f, 10f, 9.2f, 14.6f))) return false;
                    
                    if (Rect(u, v, 7.5f, 3.4f, 8.5f, 12.4f)) return false;
                    if (Circle(u, v, 3.9f, 4.6f, 1.5f)) return false;
                    if (Circle(u, v, 12.1f, 4.6f, 1.5f)) return false;
                    return true;

                case "lungs":
                    if (Rect(u, v, 7.3f, 1.2f, 8.7f, 6.4f)) return true;
                    if (Ellipse(u, v, 5f, 9.4f, 3.1f, 4.9f)) return true;
                    return Ellipse(u, v, 11f, 9.4f, 3.1f, 4.9f);

                case "bone":
                    if (Rect(u, v, 4.4f, 6.7f, 11.6f, 9.3f)) return true;
                    if (Circle(u, v, 4.3f, 6.2f, 2.4f)) return true;
                    if (Circle(u, v, 4.3f, 9.8f, 2.4f)) return true;
                    if (Circle(u, v, 11.7f, 6.2f, 2.4f)) return true;
                    return Circle(u, v, 11.7f, 9.8f, 2.4f);

                case "pulse":
                    if (Segment(u, v, 1f, 9.4f, 4.6f, 9.4f, 1.05f)) return true;
                    if (Segment(u, v, 4.6f, 9.4f, 6.2f, 9.4f, 1.05f)) return true;
                    if (Segment(u, v, 6.2f, 9.4f, 7.4f, 3.4f, 1.05f)) return true;
                    if (Segment(u, v, 7.4f, 3.4f, 8.6f, 13.4f, 1.05f)) return true;
                    if (Segment(u, v, 8.6f, 13.4f, 9.8f, 7.2f, 1.05f)) return true;
                    if (Segment(u, v, 9.8f, 7.2f, 11.4f, 9.4f, 1.05f)) return true;
                    return Segment(u, v, 11.4f, 9.4f, 15f, 9.4f, 1.05f);

                case "shield":
                    if (v < 2.2f || v > 14.4f) return false;
                    {
                        float halfWidth = v < 6.6f ? 5.4f : 5.4f * (14.4f - v) / 7.8f;
                        return Mathf.Abs(u - 8f) <= halfWidth;
                    }

                case "gauge":
                    if (Annulus(u, v, 8f, 9.2f, 4.4f, 5.7f) && v <= 9.6f) return true;
                    return Segment(u, v, 8f, 9.2f, 11.6f, 5.6f, 1.15f);

                case "mood":
                    if (Annulus(u, v, 8f, 8f, 4.7f, 6.2f)) return true;
                    return Annulus(u, v, 8f, 7.2f, 3.2f, 4.3f) && v > 8.2f;

                case "food":
                    if (Rect(u, v, 8.5f, 3f, 9.9f, 14.4f)) return true;
                    if (Rect(u, v, 6.1f, 2.6f, 7.4f, 7.4f)) return true;
                    if (Rect(u, v, 11f, 2.6f, 12.3f, 7.4f)) return true;
                    return Rect(u, v, 6.1f, 2.6f, 12.3f, 4.2f);

                case "rad":
                    if (Circle(u, v, 8f, 8f, 1.7f)) return true;
                    {
                        float dx = u - 8f;
                        float dy = v - 8f;
                        float d2 = dx * dx + dy * dy;
                        if (d2 < 7.3f || d2 > 43f) return false;
                        float angle = AngleDeg(u, v, 8f, 8f);
                        return AngleNear(angle, 90f, 24f) || AngleNear(angle, 210f, 24f) || AngleNear(angle, 330f, 24f);
                    }

                case "skull":
                    if (Rect(u, v, 5.9f, 10.6f, 10.1f, 14.2f)) return true;
                    if (!Circle(u, v, 8f, 7.2f, 5.6f)) return false;
                    if (Circle(u, v, 6f, 6.8f, 1.7f)) return false;
                    if (Circle(u, v, 10f, 6.8f, 1.7f)) return false;
                    return !Triangle(u, v, 7.2f, 10.4f, 8.8f, 10.4f, 8f, 8.4f);

                case "weight":
                    if (Circle(u, v, 8f, 9.4f, 4.7f)) return true;
                    return Annulus(u, v, 8f, 5.4f, 2.2f, 3.4f) && v <= 6f;

                case "sparkle":
                    if (Mathf.Abs(u - 8f) + Mathf.Abs(v - 8f) <= 4.7f) return true;
                    if (Mathf.Abs(u - 8f) <= 0.95f && Mathf.Abs(v - 8f) <= 7.2f) return true;
                    return Mathf.Abs(v - 8f) <= 0.95f && Mathf.Abs(u - 8f) <= 7.2f;

                case "bandage":
                    {
                        float du = u - 8f;
                        float dv = v - 8f;
                        float p = (du + dv) * 0.7071f;
                        float q = (dv - du) * 0.7071f;
                        if (Mathf.Abs(p) <= 6.2f && Mathf.Abs(q) <= 2.3f) return true;
                        return Circle(u, v, 8f, 8f, 2.1f);
                    }

                case "walk":
                    if (Circle(u, v, 8.2f, 3.1f, 2f)) return true;
                    if (Segment(u, v, 8.2f, 5.2f, 7.3f, 9.6f, 1.25f)) return true;
                    if (Segment(u, v, 8f, 6.6f, 5f, 8.6f, 1.05f)) return true;
                    if (Segment(u, v, 8f, 6.6f, 11.4f, 8.2f, 1.05f)) return true;
                    if (Segment(u, v, 7.3f, 9.6f, 5.4f, 14.4f, 1.2f)) return true;
                    return Segment(u, v, 7.3f, 9.6f, 9.9f, 14f, 1.2f);

                case "circle":
                default:
                    return Circle(u, v, 8f, 8f, 6.4f);
            }
        }
    }
}
