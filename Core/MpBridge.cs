using System;
using System.Collections.Generic;
using System.Reflection;

namespace CuStatsHud.Core
{
    /// <summary>
    /// KrokoshaCasualtiesMP（联机 MOD）的纯反射桥。
    ///
    /// 全程反射、不引用该 MOD，因此插件在未安装联机 MOD 时同样可用，
    /// 且不会因 MOD 版本变动而加载失败。所有成员都在首次成功定位后缓存，
    /// 之后每轮刷新只做 GetValue，不再做类型查找。
    /// </summary>
    internal sealed class MpBridge
    {
        /// <summary>一名存活玩家的反射视图。</summary>
        public struct Entry
        {
            public string Name;
            public UnityEngine.Object BodyObject;
            public bool IsLocal;
            public float Ping;
            /// <summary>SteamID（0 = 未知）。</summary>
            public ulong SteamId;
            /// <summary>头像原图（联机 MOD 已加载好的 Texture2D）；为 null 表示暂无。</summary>
            public object Avatar;
        }

        private const string ModAssemblyName = "KrokoshaCasualtiesMP";
        private const string NetPlayerTypeName = "KrokoshaCasualtiesMP.NetPlayer";

        /// <summary>未找到 MOD 时的重扫间隔（秒）：MOD 可能在插件之后才加载完成。</summary>
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

        /// <summary>联机 MOD 是否已就绪（AllLivingPlayers 等成员均已解析）。</summary>
        public bool Available
        {
            get { return _available; }
        }

        /// <summary>
        /// 尝试解析 MOD 类型。MOD 可能在插件之后才加载，
        /// 因此解析失败时按 <see cref="RescanInterval"/> 周期重试，而不是一次定生死。
        /// </summary>
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

                // Steam 身份与头像：MOD 会把头像加载好挂在 NetPlayer 上，
                // 优先读候选属性（内部已做 large→medium→small 回退），再退回三个字段。
                _fSteamId = _netPlayerType.GetField("SteamId", Instance);
                _pAvatar = _netPlayerType.GetProperty("profilepic_any_largetosmall", Instance);
                _fAvatarLarge = _netPlayerType.GetField("profilepic_largeicon", Instance);
                _fAvatarMedium = _netPlayerType.GetField("profilepic_mediumicon", Instance);
                _fAvatarSmall = _netPlayerType.GetField("profilepic_smallicon", Instance);

                // body 是唯一必需的成员：没有它就无法读取任何玩家数据
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

        /// <summary>清理解析状态以便下次重新扫描。</summary>
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

        /// <summary>读取本地玩家：返回显示名与 Body；失败时 name 为 null、body 为 null。</summary>
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

        /// <summary>读取本地玩家名；失败返回 null。</summary>
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

        /// <summary>
        /// 收集所有存活玩家。结果写入调用方传入的列表（复用，避免每轮分配）。
        /// 返回是否走了联机路径。
        /// </summary>
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
                            // is_local 读取失败时退化为“按 body 比对本地玩家”
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
        /// <summary>
        /// 读取玩家头像原图。MOD 是懒加载的，刚进游戏时可能为 null，
        /// 因此这里只做「拿不到就返回 null」，由 UI 层限时重试。
        /// </summary>
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
                // 头像读取失败不影响其它数据
            }
            return null;
        }
    }
}
