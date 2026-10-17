using System;
using System.Collections.Generic;
using UnityEngine;

namespace CuStatsHud.UI
{
    
    internal sealed class IconProvider : IDisposable
    {
        
        private static readonly Dictionary<string, string> KindToEmoji = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            
            { "heart", "1fac0" },        
            { "bp", "1fa7a" },           
            { "oxygen", "1f4a8" },       
            { "blood", "1fa78" },        
            { "viscosity", "1f9ea" },    
            { "fibrillation", "26a1" },  
            { "lungs", "1fac1" },        
            
            { "conscious", "1f440" },    
            { "brain", "1f9e0" },        
            { "pain", "1f62b" },         
            { "painshock", "1f915" },    
            { "shock", "1f4a5" },        
            { "mood", "1f60a" },         
            { "energy", "1f50b" },       
            { "bandage", "1fa79" },      
            
            { "food", "1f35e" },         
            { "water", "1f4a7" },        
            { "thermo", "1f321" },       
            { "sick", "1f912" },         
            { "sepsis", "1f9a0" },       
            { "rad", "2622" },           
            { "skull", "2620" },         
            { "dirty", "1f9fc" },        
            { "hearing", "1f9bb" },      
            { "immune", "1f489" },       
            
            { "strength", "1f4aa" },     
            { "shield", "1f6e1" },       
            { "intellect", "1f4a1" },    
            { "sparkle", "2728" },       
            { "weight", "2696" },        
            { "walk", "1f3c3" },         
            
            { "arm", "1f9be" },          
            { "leg", "1f9b5" },          
            { "bone", "1f9b4" },         
            
            { "vitals", "1f4ca" },       
            { "limbs", "1f4cb" },        
            { "mates", "1f465" },        
            { "settings", "2699" },      
            { "stats", "1f4ca" },        
            { "warning", "26a0" }        
        };

        private readonly EmojiIcons _emoji;
        private readonly IconFactory _procedural;

        private static EmojiIcons _sharedEmoji;

        public IconProvider(int proceduralIconSize)
        {
            if (_sharedEmoji == null)
            {
                _sharedEmoji = new EmojiIcons();
                _sharedEmoji.Preload(KindToEmoji.Values);
            }

            _emoji = _sharedEmoji;
            _procedural = new IconFactory(proceduralIconSize);
        }

        public static void DisposeShared()
        {
            if (_sharedEmoji == null) return;
            _sharedEmoji.Dispose();
            _sharedEmoji = null;
        }

        public static bool EmojiEnabled = true;

        public Texture2D GetEmoji(string kind)
        {
            if (!EmojiEnabled) return null;

            string code;
            if (string.IsNullOrEmpty(kind) || !KindToEmoji.TryGetValue(kind, out code)) return null;
            return _emoji.Get(code);
        }

        public Texture2D Get(string kind, Color proceduralTint)
        {
            if (!EmojiEnabled) return null;

            Texture2D emoji = GetEmoji(kind);
            if (emoji != null) return emoji;
            return _procedural.Get(kind, proceduralTint);
        }

        public Texture2D GetProcedural(string kind, Color tint)
        {
            return _procedural.Get(kind, tint);
        }

        public bool IsEmoji(string kind)
        {
            if (!EmojiEnabled) return false;
            return GetEmoji(kind) != null;
        }

        public void Dispose()
        {
            
            _procedural.Dispose();
        }
    }
}
