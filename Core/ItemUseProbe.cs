using System;

namespace CuStatsHud.Core
{
    /// <summary>手持物品的探测结论（三态，避免用魔法布尔值表达「不知道」）。</summary>
    public enum HandItemState
    {
        /// <summary>探测失败，无法判断。调用方应保守放行。</summary>
        Unknown = 0,
        /// <summary>手上确实有物品。</summary>
        Present = 1,
        /// <summary>手上没有物品 —— 这次不该计入道具使用。</summary>
        Empty = 2
    }

    /// <summary>
    /// 判断「这次 <c>Body.UseItemInHand()</c> 到底有没有真正的道具」。
    ///
    /// <b>背景：</b>游戏在没有可用物品时会把这次调用变成一次空手挥击，
    /// 方法照样返回、后置补丁照样触发。若不加区分地计数，
    /// 「道具使用」总数就会夹杂大量空挥，与下方按 id 分类的明细之和永远对不上。
    ///
    /// <b>依据（已对 Assembly-CSharp.dll 实测核对）：</b>
    ///   <c>Body</c> 上并没有直接持有物品的字段，只有：
    ///     <c>InventorySlot[] slots</c> —— 槽位数组；
    ///     <c>int handSlot</c>        —— <b>当前手持的槽位索引</b>（不是物品对象！）。
    ///   游戏自己提供了读取入口：
    ///     <c>Item Body.GetItem(int slot)</c> —— 取某槽位的物品组件，槽位为空则返回 null。
    ///
    ///   所以「手上有东西吗」等价于 <c>body.GetItem(body.handSlot) != null</c>。
    ///   这也是早先注释里「需要读 Body 的私有 handSlot」一直没做成的原因 ——
    ///   handSlot 只是索引，读它本身拿不到物品，必须再配合 GetItem。
    ///
    /// <b>为什么仍然走反射：</b>直接引用游戏类型会在游戏改签名时让插件加载失败。
    /// 反射 + 失败降级能把影响限制在「统计不准」，而不是「插件起不来」。
    /// </summary>
    internal static class ItemUseProbe
    {
        /// <summary>槽位索引成员名（实测为 <c>int handSlot</c>）。</summary>
        private static readonly string[] HandSlotNames = { "handSlot" };

        /// <summary>按槽位取物品的方法名（实测为 <c>Item GetItem(int)</c>）。</summary>
        private const string GetItemName = "GetItem";

        /// <summary>物品池成员名：仅用于退到「前后指纹比对」时取元素序列。</summary>
        private static readonly string[] PoolNames = { "slots", "items", "inventory" };

        private static Type _cachedType;
        private static bool _resolved;

        private static System.Reflection.FieldInfo _handSlotField;
        private static System.Reflection.MethodInfo _getItemMethod;
        private static System.Reflection.FieldInfo _poolField;

        /// <summary>
        /// 第一级判定：读「当前槽位的物品」。
        ///
        /// <b>注意语义：</b>在 <c>UseItemInHand</c> 的**后置**补丁里调用时，
        /// 拿到的是动作完成后的状态。若该道具被用掉最后一个，这里会返回
        /// <see cref="HandItemState.Empty"/>，但那次**确实是真实使用**。
        /// 因此调用方应优先看前后指纹，本方法只作为次级依据。
        /// </summary>
        public static HandItemState ReadHand(object body)
        {
            if (body == null) return HandItemState.Unknown;

            EnsureResolved(body.GetType());
            if (_handSlotField == null || _getItemMethod == null) return HandItemState.Unknown;

            try
            {
                object slotIndex = _handSlotField.GetValue(body);
                if (slotIndex == null) return HandItemState.Unknown;

                object item = _getItemMethod.Invoke(body, new object[] { slotIndex });
                return item != null ? HandItemState.Present : HandItemState.Empty;
            }
            catch (Exception)
            {
                // 槽位越界 / 对象销毁：当作读不到
                return HandItemState.Unknown;
            }
        }

        /// <summary>
        /// 取一份「道具状况」指纹，供调用方在动作前后比对。
        /// 优先用当前手持槽的物品身份，探不到时退到整个槽位数组的元素序列。
        /// 两级都不可用、或读取出错时返回 null（表示无法比对）。
        /// </summary>
        public static string Snapshot(object body)
        {
            if (body == null) return null;

            EnsureResolved(body.GetType());

            // 优先：当前手持槽的物品实例
            if (_handSlotField != null && _getItemMethod != null)
            {
                try
                {
                    object slotIndex = _handSlotField.GetValue(body);
                    object item = slotIndex != null ? _getItemMethod.Invoke(body, new object[] { slotIndex }) : null;
                    return item == null ? "hand:-" : "hand:" + Identity(item);
                }
                catch (Exception)
                {
                    // 落到下面的槽位数组方案
                }
            }

            if (_poolField == null) return null;

            object pool = ReadField(_poolField, body);
            if (pool == null) return "pool:-";

            System.Collections.IEnumerable sequence = pool as System.Collections.IEnumerable;
            if (sequence == null) return "pool:" + Identity(pool);

            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder("pool:");
                int count = 0;
                foreach (object element in sequence)
                {
                    // 只取前若干个引用身份：足够判断「有没有变化」，
                    // 又不会在 hook 里对长数组做大量字符串拼接。
                    if (count >= 32) break;
                    sb.Append(Identity(element)).Append('|');
                    count++;
                }
                return sb.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>比对前后指纹。任一为 null（两级都不可用）时返回 null = 无法判断。</summary>
        public static bool? Changed(string before, string after)
        {
            if (before == null || after == null) return null;
            return !string.Equals(before, after, StringComparison.Ordinal);
        }

        /// <summary>插件销毁/重挂补丁时清空缓存，下次调用重新探测。</summary>
        public static void Reset()
        {
            _cachedType = null;
            _resolved = false;
            _handSlotField = null;
            _getItemMethod = null;
            _poolField = null;
        }

        // ---------------------------------------------------------------

        private static void EnsureResolved(Type bodyType)
        {
            if (bodyType == null) return;
            if (_resolved && _cachedType == bodyType) return;

            _cachedType = bodyType;
            _resolved = true;
            _handSlotField = null;
            _getItemMethod = null;
            _poolField = null;

            const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance;

            // 手持槽位索引：必须是 int 字段
            for (int i = 0; i < HandSlotNames.Length && _handSlotField == null; i++)
            {
                try
                {
                    System.Reflection.FieldInfo f = bodyType.GetField(HandSlotNames[i], Flags);
                    if (f != null && f.FieldType == typeof(int)) _handSlotField = f;
                }
                catch (Exception)
                {
                }
            }

            // 按槽位取物品：一个 int 参数、返回引用类型的实例方法
            try
            {
                System.Reflection.MethodInfo[] candidates = bodyType.GetMethods(Flags);
                for (int i = 0; i < candidates.Length; i++)
                {
                    System.Reflection.MethodInfo m = candidates[i];
                    if (m.Name != GetItemName) continue;
                    if (m.IsStatic || m.ReturnType.IsValueType) continue;
                    if (m.GetParameters().Length != 1) continue;

                    _getItemMethod = m;
                    break;
                }
            }
            catch (Exception)
            {
            }

            // 槽位数组（仅用于指纹兜底）
            for (int i = 0; i < PoolNames.Length && _poolField == null; i++)
            {
                try
                {
                    System.Reflection.FieldInfo f = bodyType.GetField(PoolNames[i], Flags);
                    if (f != null) _poolField = f;
                }
                catch (Exception)
                {
                }
            }
        }

        private static object ReadField(System.Reflection.FieldInfo field, object instance)
        {
            try
            {
                return field.GetValue(instance);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 元素身份。UnityEngine.Object 用实例 ID，其它对象用运行时哈希。
        /// <b>刻意不用 ToString()</b>：物品的 ToString 可能返回本地化名字，
        /// 同名的不同实例会被误判成「没变化」。
        /// </summary>
        private static string Identity(object value)
        {
            if (value == null) return "null";

            try
            {
                UnityEngine.Object unityObject = value as UnityEngine.Object;
                if (unityObject != null)
                {
                    // Unity 的 == 重载能识别「已销毁」，那种情况下 GetInstanceID 会抛异常
                    return unityObject == null ? "destroyed" : "u" + unityObject.GetInstanceID();
                }
            }
            catch (Exception)
            {
                return "unity-err";
            }

            try
            {
                return "h" + value.GetHashCode();
            }
            catch (Exception)
            {
                return "obj";
            }
        }
    }
}
