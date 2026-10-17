using System;
using System.IO;
using UnityEngine;

namespace CuStatsHud.UI
{
    
    internal static class BackgroundImage
    {
        private static Texture2D _texture;

        public static Texture2D Current
        {
            get { return _texture; }
        }

        public static bool Ensure(string path, bool force)
        {
            if (!force && _texture != null) return true;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Release();
                return false;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes == null || bytes.Length == 0)
                {
                    Release();
                    return false;
                }

                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.hideFlags = HideFlags.HideAndDontSave;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;

                if (!tex.LoadImage(bytes))
                {
                    UnityEngine.Object.Destroy(tex);
                    Release();
                    return false;
                }

                Release();
                _texture = tex;
                return true;
            }
            catch (Exception)
            {
                
                Release();
                return false;
            }
        }

        public static void Release()
        {
            if (_texture == null) return;

            try
            {
                UnityEngine.Object.Destroy(_texture);
            }
            catch (Exception)
            {
                
            }

            _texture = null;
        }

        private static Texture2D _preview;
        private static string _previewPath;

        public static Texture2D Preview
        {
            get { return _preview; }
        }

        public static bool EnsurePreview(string path)
        {
            if (string.Equals(path, _previewPath, StringComparison.OrdinalIgnoreCase) && _preview != null)
            {
                return true;
            }

            ReleasePreview();
            _previewPath = path;

            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes == null || bytes.Length == 0) return false;

                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.hideFlags = HideFlags.HideAndDontSave;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;

                if (!tex.LoadImage(bytes))
                {
                    UnityEngine.Object.Destroy(tex);
                    return false;
                }

                _preview = tex;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void ReleasePreview()
        {
            if (_preview != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(_preview);
                }
                catch (Exception)
                {
                }
                _preview = null;
            }
            _previewPath = null;
        }
    }
}
