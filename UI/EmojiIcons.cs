using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace CuStatsHud.UI
{
    /// <summary>
    /// 内嵌 emoji 图标的加载与缓存。
    ///
    /// 图标以 <c>EmbeddedResource</c> 打进 DLL（逻辑名 <c>CUStatsHud.emoji.&lt;码点&gt;</c>），
    /// 因此插件是单文件分发、不需要额外的图片目录，也不会被游戏资源卸载影响。
    /// 图片为 128×128 PNG，绘制时由 GUI 双线性缩放到实际尺寸。
    /// </summary>
    internal sealed class EmojiIcons : IDisposable
    {
        private const string ResourcePrefix = "CUStatsHud.emoji.";

        /// <summary>整体过暗、且低饱和（深灰线条图标）在近黑面板上看不清，需要抬亮。</summary>
        private const float DarkLumaThreshold = 0.42f;

        /// <summary>饱和度上限：只有低饱和（灰阶）的暗图标才抬亮。</summary>
        private const float DarkChromaThreshold = 0.22f;

        /// <summary>抬亮后的目标平均亮度。</summary>
        private const float TargetLuma = 0.62f;

        /// <summary>码点 → 纹理；值为 null 表示该码点没有资源（同样缓存，避免反复查找）。</summary>
        private readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        /// <summary>取图标；没有对应资源时返回 null。</summary>
        public Texture2D Get(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;

            Texture2D cached;
            if (_cache.TryGetValue(code, out cached)) return cached;

            Texture2D texture = Load(code);
            _cache[code] = texture;
            return texture;
        }

        /// <summary>预载一组码点（首次解析时一次性解码，避免首次打开标签页时掉帧）。</summary>
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

        /// <summary>
        /// 面板底色接近纯黑，整体偏暗的灰阶 emoji（典型：🩺 深灰听诊器）几乎看不见，
        /// 因此载入时做一次亮度抬升：<c>rgb' = rgb + (1-rgb)*k</c>，保持色相。
        ///
        /// 判据是「暗 <b>且</b> 低饱和」：饱和的深色图标（🩸 血滴、🫀 心脏、💥 冲击）
        /// 在深色背景上对比度本来就够，抬亮只会让它们变粉、丢掉辨识度。
        /// </summary>
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
                // 抬亮失败不影响使用，保留原始贴图
            }
        }
    }
}
