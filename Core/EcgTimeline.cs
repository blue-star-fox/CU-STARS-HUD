using System;

namespace CuStatsHud.Core
{
    /// <summary>
    /// 心电时间轴换算 —— 纯逻辑，无 Unity 依赖，可单元测试。
    ///
    /// 游戏的 <c>Body.GetECGHeight(offset)</c> 等价于
    /// <c>heartCurve.Evaluate(heartProg - offset * heartRate / 60)</c>，
    /// 其中 <c>heartProg</c> 是以 1 为周期循环推进的相位。
    ///
    /// 由此得到两条关键性质：
    ///   1. 曲线本身是周期的 ⇒ 回退整数个心动周期不改变形状，可以丢弃；
    ///   2. 但 Evaluate 只在一个周期内有效，相位一旦超出 <c>[heartProg-1, heartProg]</c>，
    ///      AnimationCurve 会按端点值 clamp —— 画面就变成一条直线。
    ///
    /// 因此要把回退相位**折进**这个窗口，这正是「面板一打开就能铺满整屏历史波形」
    /// 能成立的前提（否则回填超过一个周期就只能得到直线）。
    /// </summary>
    public static class EcgTimeline
    {
        /// <summary>
        /// 把「想回退多少秒」换算成可以安全交给 <c>GetECGHeight</c> 的 offset。
        /// 其等效相位一定落在 <c>[heartProg-1, heartProg]</c> 内。
        /// </summary>
        public static float OffsetFor(float secondsBack, float heartRate, float heartProg)
        {
            if (secondsBack <= 0f) return 0f;

            float beatsPerSecond = BeatsPerSecond(heartRate);
            float cyclesBack = secondsBack * beatsPerSecond;   // 单位：心动周期数

            float wrapped = cyclesBack - (float)Math.Floor(cyclesBack);   // [0,1)

            // 注意：窗口判断必须用**原始** heartProg，不能先归一化。
            // 游戏内部是 Evaluate(heartProg - offset * bps)，用的就是原值；
            // 若这里把 heartProg=1 归一化成 0 再判断，就会为它挑出越界的相位
            // （1 + 0.73 = 1.73），Evaluate 只会按端点 clamp，那一列波形就成了直线。
            float prog = heartProg;
            if (prog < 0f || prog > 1f) prog -= (float)Math.Floor(prog);   // 异常输入的防御

            // 折进有效窗口：wrapped ∈ [prog-1, prog]
            if (wrapped > prog) wrapped -= 1f;

            return wrapped / beatsPerSecond;
        }

        /// <summary>该 offset 的等效相位（供测试与自查）。</summary>
        public static float PhaseFor(float offset, float heartRate, float heartProg)
        {
            return heartProg - offset * BeatsPerSecond(heartRate);
        }

        /// <summary>一个心动周期的秒数。</summary>
        public static float CycleSeconds(float heartRate)
        {
            return 1f / BeatsPerSecond(heartRate);
        }

        private static float BeatsPerSecond(float heartRate)
        {
            return heartRate > 1f ? heartRate / 60f : 1f / 60f;
        }
    }
}
