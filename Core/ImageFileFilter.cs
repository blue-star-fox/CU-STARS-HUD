using System;
using System.IO;

namespace CuStatsHud.Core
{
    
    public static class ImageFileFilter
    {
        
        public static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".bmp" };

        public static bool IsImage(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            try
            {
                string ext = Path.GetExtension(path);
                if (string.IsNullOrEmpty(ext)) return false;

                ext = ext.ToLowerInvariant();
                for (int i = 0; i < Extensions.Length; i++)
                {
                    if (ext == Extensions[i]) return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
