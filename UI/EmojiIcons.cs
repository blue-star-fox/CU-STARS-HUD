using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace CuStatsHud.UI
{
    
    internal sealed class EmojiIcons : IDisposable
    {
        private const string ResourcePrefix = "CUStatsHud.emoji.";

        private const float DarkLumaThreshold = 0.42f;

        private const float DarkChromaThreshold = 0.22f;

        private const float TargetLuma = 0.62f;

        private readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        public Texture2D Get(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;

            Texture2D cached;
            if (_cache.TryGetValue(code, out cached)) return cached;

            Texture2D texture = Load(code);
            _cache[code] = texture;
            return texture;
        }

        public void Preload(IEnumerable<string> codes)
        {
            if (codes == null) return;
            foreach (string code in codes) Get(code);
        }

        public void Dispose()
        {
            foreach (KeyValuePair<string, Texture2D> pair in _cache)
            {
                if (pair.Value != null) UnityEngine.Object.Destroy(pair.Value);
            }
            _cache.Clear();
        }

        private static Texture2D Load(string code)
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream(ResourcePrefix + code))
                {
                    if (stream == null) return null;

                    byte[] bytes = new byte[stream.Length];
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int read = stream.Read(bytes, offset, bytes.Length - offset);
                        if (read <= 0) break;
                        offset += read;
                    }

                    Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    texture.hideFlags = HideFlags.HideAndDontSave;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    texture.filterMode = FilterMode.Bilinear;

                    if (!texture.LoadImage(bytes))
                    {
                        UnityEngine.Object.Destroy(texture);
                        return null;
                    }

                    LiftIfTooDark(texture);
                    return texture;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void LiftIfTooDark(Texture2D texture)
        {
            try
            {
                Color32[] pixels = texture.GetPixels32();
                if (pixels == null || pixels.Length == 0) return;

                double lumaSum = 0d;
                double chromaSum = 0d;
                int count = 0;

                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 p = pixels[i];
                    if (p.a < 32) continue;

                    int max = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
                    int min = Mathf.Min(p.r, Mathf.Min(p.g, p.b));

                    lumaSum += (0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b) / 255d;
                    chromaSum += (max - min) / 255d;
                    count++;
                }
                if (count == 0) return;

                float luma = (float)(lumaSum / count);
                float chroma = (float)(chromaSum / count);
                if (luma >= DarkLumaThreshold || chroma >= DarkChromaThreshold) return;

                float k = Mathf.Clamp01((TargetLuma - luma) / Mathf.Max(0.05f, 1f - luma));

                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 c = pixels[i];
                    if (c.a == 0) continue;
                    c.r = (byte)(c.r + (255 - c.r) * k);
                    c.g = (byte)(c.g + (255 - c.g) * k);
                    c.b = (byte)(c.b + (255 - c.b) * k);
                    pixels[i] = c;
                }

                texture.SetPixels32(pixels);
                texture.Apply(false);
            }
            catch (Exception)
            {
                
            }
        }
    }
}
