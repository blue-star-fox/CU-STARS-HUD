using System;
using System.Collections.Generic;
using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI
{
    
    internal sealed class AvatarCache : IDisposable
    {
        
        private const float RetrySeconds = 3f;

        private const int MaxEntries = 24;

        private readonly Dictionary<ulong, Texture2D> _textures = new Dictionary<ulong, Texture2D>();
        private readonly Dictionary<ulong, int> _sizes = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, float> _retryAt = new Dictionary<ulong, float>();

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
