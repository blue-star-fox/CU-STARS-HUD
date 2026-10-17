using System;
using System.Collections.Generic;
using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI
{
    /// <summary>
    /// Steam 头像缓存：把联机 MOD 已经加载好的头像原图，转成圆形贴图并缓存。
    ///
    /// 头像原图由 KrokoshaCasualtiesMP 挂在 NetPlayer 上（profilepic_*），
    /// 是懒加载的——刚进游戏时可能还是 null，因此这里对「暂时拿不到」只做限时退避重试，
    /// 而不是永久标记为失败。没有联机 MOD 时也不会有头像，界面直接不画头像。
    /// </summary>
    internal sealed class AvatarCache : IDisposable
    {
        /// <summary>拿不到头像后的重试间隔（秒）。</summary>
        private const float RetrySeconds = 3f;

        /// <summary>缓存上限（超出整体清空，头像数量本来就很少）。</summary>
        private const int MaxEntries = 24;

        private readonly Dictionary<ulong, Texture2D> _textures = new Dictionary<ulong, Texture2D>();
        private readonly Dictionary<ulong, int> _sizes = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, float> _retryAt = new Dictionary<ulong, float>();

        /// <summary>
        /// 取圆形头像贴图。拿不到时返回 null（调用方直接不画头像）。
        /// </summary>
        public Texture2D Get(ulong steamId, object source, int size)
        {
            if (steamId == 0UL || source == null || size < 12) return null;

            Texture2D sourceTexture = source as Texture2D;
            if (sourceTexture == null) return null;

            int sourceWidth;
            int sourceHeight;
            try
            {
                sourceWidth = sourceTexture.width;
                sourceHeight = sourceTexture.height;
            }
            catch (Exception)
            {
                return null;
            }
            if (sourceWidth <= 0 || sourceHeight <= 0) return null;

            Texture2D cached;
            int cachedSize;
            if (_textures.TryGetValue(steamId, out cached) && cached != null
                && _sizes.TryGetValue(steamId, out cachedSize) && cachedSize == size)
            {
                return cached;
            }

            float now = Time.unscaledTime;
            float retryAt;
            if (_retryAt.TryGetValue(steamId, out retryAt) && now < retryAt) return null;

            Texture2D built = Build(sourceTexture, sourceWidth, sourceHeight, size);
            if (built == null)
            {
                _retryAt[steamId] = now + RetrySeconds;
                return null;
            }

            if (cached != null) UnityEngine.Object.Destroy(cached);
            if (_textures.Count >= MaxEntries) Clear();

            _textures[steamId] = built;
            _sizes[steamId] = size;
            _retryAt.Remove(steamId);
            return built;
        }

        public void Dispose()
        {
            Clear();
        }

        private void Clear()
        {
            foreach (KeyValuePair<ulong, Texture2D> pair in _textures)
            {
                if (pair.Value != null) UnityEngine.Object.Destroy(pair.Value);
            }
            _textures.Clear();
            _sizes.Clear();
            _retryAt.Clear();
        }

        private Texture2D Build(Texture2D source, int sourceWidth, int sourceHeight, int size)
        {
            try
            {
                Color32[] sourcePixels = source.GetPixels32();
                if (sourcePixels == null || sourcePixels.Length < sourceWidth * sourceHeight) return null;

                // 转成与 Unity 无关的结构做像素运算（圆形遮罩 + 缩放 + 轮廓），便于单测
                Rgba32[] rgba = new Rgba32[sourcePixels.Length];
                for (int i = 0; i < rgba.Length; i++)
                {
                    Color32 c = sourcePixels[i];
                    rgba[i] = new Rgba32(c.r, c.g, c.b, c.a);
                }

                Rgba32[] circular = AvatarBuffer.MakeCircular(rgba, sourceWidth, sourceHeight, size);
                if (circular == null) return null;

                Color32[] destination = new Color32[circular.Length];
                for (int i = 0; i < circular.Length; i++)
                {
                    Rgba32 c = circular[i];
                    destination[i] = new Color32(c.R, c.G, c.B, c.A);
                }

                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.hideFlags = HideFlags.HideAndDontSave;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                texture.SetPixels32(destination);
                texture.Apply(false);
                return texture;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
