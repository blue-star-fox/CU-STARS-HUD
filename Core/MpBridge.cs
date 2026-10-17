using System;
using System.Collections.Generic;
using System.Reflection;

namespace CuStatsHud.Core
{
    
    internal sealed class MpBridge
    {
        
        public struct Entry
        {
            public string Name;
            public UnityEngine.Object BodyObject;
            public bool IsLocal;
            public float Ping;
            
            public ulong SteamId;
            
            public object Avatar;
        }

        private const string ModAssemblyName = "KrokoshaCasualtiesMP";
        private const string NetPlayerTypeName = "KrokoshaCasualtiesMP.NetPlayer";

        private const float RescanInterval = 5f;

        private bool _scanned;
        private bool _available;
        private float _nextScanAt;

        private Type _netPlayerType;
        private FieldInfo _fAllLiving;
        private FieldInfo _fLocalPlayer;
        private FieldInfo _fName;
        private FieldInfo _fBody;
        private FieldInfo _fPing;
        private PropertyInfo _pIsLocal;
        private FieldInfo _fSteamId;
        private PropertyInfo _pAvatar;
        private FieldInfo _fAvatarLarge;
        private FieldInfo _fAvatarMedium;
        private FieldInfo _fAvatarSmall;

        public bool Available
        {
            get { return _available; }
        }

        public void EnsureInitialized()
        {
            if (_available) return;

            float now = UnityEngine.Time.unscaledTime;
            if (_scanned && now < _nextScanAt) return;

            Reset();
            _scanned = true;
            _nextScanAt = now + RescanInterval;
            try
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    Assembly asm = assemblies[i];
                    if (asm == null) continue;
                    if (asm.GetName().Name != ModAssemblyName) continue;
                    _netPlayerType = asm.GetType(NetPlayerTypeName, false);
                    break;
                }

                if (_netPlayerType == null) return;

                const BindingFlags Static = BindingFlags.Public | BindingFlags.Static;
                const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;

                _fAllLiving = _netPlayerType.GetField("AllLivingPlayers", Static);
                _fLocalPlayer = _netPlayerType.GetField("LOCAL_PLAYER", Static);
                _fName = _netPlayerType.GetField("playername", Instance);
                _fBody = _netPlayerType.GetField("body", Instance);
                _fPing = _netPlayerType.GetField("ping", Instance);
                _pIsLocal = _netPlayerType.GetProperty("is_local", Instance);

                _fSteamId = _netPlayerType.GetField("SteamId", Instance);
                _pAvatar = _netPlayerType.GetProperty("profilepic_any_largetosmall", Instance);
                _fAvatarLarge = _netPlayerType.GetField("profilepic_largeicon", Instance);
                _fAvatarMedium = _netPlayerType.GetField("profilepic_mediumicon", Instance);
                _fAvatarSmall = _netPlayerType.GetField("profilepic_smallicon", Instance);

                _available = _fBody != null && (_fAllLiving != null || _fLocalPlayer != null);
            }
            catch (Exception)
            {
                _netPlayerType = null;
                _fAllLiving = null;
                _fLocalPlayer = null;
                _fName = null;
                _fBody = null;
                _fPing = null;
                _pIsLocal = null;
                _fSteamId = null;
                _pAvatar = null;
                _fAvatarLarge = null;
                _fAvatarMedium = null;
                _fAvatarSmall = null;
                _available = false;
            }
        }

        public void Reset()
        {
            _scanned = false;
            _available = false;
            _netPlayerType = null;
            _fAllLiving = null;
            _fLocalPlayer = null;
            _fName = null;
            _fBody = null;
            _fPing = null;
            _pIsLocal = null;
            _fSteamId = null;
            _pAvatar = null;
            _fAvatarLarge = null;
            _fAvatarMedium = null;
            _fAvatarSmall = null;
        }

        public UnityEngine.Object GetLocalBodyObject()
        {
            if (!_available || _fLocalPlayer == null) return null;
            try
            {
                object local = _fLocalPlayer.GetValue(null);
                if (local == null) return null;
                return _fBody.GetValue(local) as UnityEngine.Object;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public string GetLocalPlayerName()
        {
            if (!_available || _fLocalPlayer == null || _fName == null) return null;
            try
            {
                object local = _fLocalPlayer.GetValue(null);
                if (local == null) return null;
                return _fName.GetValue(local) as string;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public bool CollectLivingPlayers(List<Entry> into)
        {
            into.Clear();
            if (!_available) return false;

            try
            {
                object raw = _fAllLiving != null ? _fAllLiving.GetValue(null) : null;
                System.Collections.IEnumerable list = raw as System.Collections.IEnumerable;
                if (list == null) return false;

                foreach (object player in list)
                {
                    if (player == null) continue;

                    Entry entry = new Entry();
                    entry.BodyObject = _fBody.GetValue(player) as UnityEngine.Object;

                    if (_fName != null)
                    {
                        entry.Name = _fName.GetValue(player) as string;
                    }

                    if (_pIsLocal != null)
                    {
                        try
                        {
                            object local = _pIsLocal.GetValue(player, null);
                            if (local is bool) entry.IsLocal = (bool)local;
                        }
                        catch (Exception)
                        {
                            
                        }
                    }

                    if (_fPing != null)
                    {
                        try
                        {
                            object ping = _fPing.GetValue(player);
                            if (ping is double) entry.Ping = (float)(double)ping;
                            else if (ping is float) entry.Ping = (float)ping;
                            else if (ping is int) entry.Ping = (int)ping;
                        }
                        catch (Exception)
                        {
                            entry.Ping = -1f;
                        }
                    }
                    else
                    {
                        entry.Ping = -1f;
                    }

                    if (_fSteamId != null)
                    {
                        try
                        {
                            object steamId = _fSteamId.GetValue(player);
                            if (steamId is ulong) entry.SteamId = (ulong)steamId;
                            else if (steamId is long) entry.SteamId = (ulong)(long)steamId;
                        }
                        catch (Exception)
                        {
                            entry.SteamId = 0UL;
                        }
                    }

                    entry.Avatar = ReadAvatar(player);

                    into.Add(entry);
                }

                return true;
            }
            catch (Exception)
            {
                into.Clear();
                return false;
            }
        }
        
        private object ReadAvatar(object player)
        {
            try
            {
                if (_pAvatar != null)
                {
                    object value = _pAvatar.GetValue(player, null);
                    if (value != null) return value;
                }
                if (_fAvatarLarge != null)
                {
                    object value = _fAvatarLarge.GetValue(player);
                    if (value != null) return value;
                }
                if (_fAvatarMedium != null)
                {
                    object value = _fAvatarMedium.GetValue(player);
                    if (value != null) return value;
                }
                if (_fAvatarSmall != null)
                {
                    object value = _fAvatarSmall.GetValue(player);
                    if (value != null) return value;
                }
            }
            catch (Exception)
            {
                
            }
            return null;
        }
    }
}
