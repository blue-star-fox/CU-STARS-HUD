using System;

namespace CuStatsHud.Core
{
    /// <summary>
    /// 波形纸带的像素缓冲：与 UnityEngine 无关的纯逻辑，便于单元测试。
    ///
    /// <b>索引约定（关键）：</b>缓冲按行主序存放，索引为 <c>y * Width + x</c>，
    /// 与 <c>Texture2D.SetPixels32</c> 的期望顺序一致（自下而上、同一行内自左向右）。
    /// 若误写成列主序 <c>x * Height + y</c>，整幅波形会被转置 —— 竖直线段变成横线，
    /// 看上去就只剩零碎的点。
    ///
    /// <b>为什么内部存 float 而不是 byte：</b>余辉衰减若直接对 byte 做
    /// <c>(byte)(a * 0.985)</c>，每步都会向下截断、额外多损失约 0.5/步，
    /// 拖尾会从理论上的 ~410 列缩到 ~140 列（曲线只剩一小段）；
    /// 而改成四舍五入又有不动点问题（a ≤ 33 时 <c>round(a*0.985) == a</c>，永远淡不掉、末端硬切）。
    /// 内部用 0..255 的 float 累积，只在合成纹理时转成 byte，就能得到平滑且能真正消失的余辉。
    /// </summary>
    public sealed class WaveformBuffer
    {
        /// <summary>每写一列后的余辉系数（0.985 ⇒ 拖尾约 410 列）。</summary>
        public const float DecayPerColumn = 0.985f;

        private float[] _level = new float[0];
        private int _writeX;
        private int _lastY;

        public int Width { get; private set; }

        public int Height { get; private set; }

        /// <summary>强度缓冲（0..255，行主序，长度 = Width * Height）。</summary>
        public float[] Levels
        {
            get { return _level; }
        }

        /// <summary>行主序索引：与 SetPixels32 一致。</summary>
        public static int Index(int width, int x, int y)
        {
            return y * width + x;
        }

        /// <summary>
        /// 等电位基线的强度。真机的 ECG 纸带永远有一条水平基线，未扫到的区域也不是空洞，
        /// 所以合成纹理时在中间行叠加这一层。
        ///
        /// 它<b>不参与余辉衰减</b>——基线是常驻的，只有波形才淡出；
        /// 因此放在合成阶段（<see cref="WriteBytes"/>）而不是写进 level 缓冲，
        /// 否则每写一列都会被 <see cref="Decay"/> 削一次，几十列后就消失了。
        /// </summary>
        public const byte BaselineLevel = 64;

        /// <summary>某个像素的显示强度（越界返回 0）。注意：<b>不含</b>基线层，仅反映波形数据。</summary>
        public byte At(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return 0;

            float level = _level[Index(Width, x, y)];
            if (level < 1f) return 0;
            if (level > 255f) return 255;
            return (byte)level;
        }

        /// <summary>把强度缓冲转成 byte alpha（行主序），供纹理上传使用；此处叠加常驻基线。</summary>
        public void WriteBytes(byte[] destination, int count)
        {
            if (destination == null) return;

            int n = Math.Min(count, Math.Min(destination.Length, _level.Length));
            for (int i = 0; i < n; i++)
            {
                float level = _level[i];
                destination[i] = level < 1f ? (byte)0 : (level > 255f ? (byte)255 : (byte)level);
            }

            if (Width <= 0 || Height <= 0) return;

            int baselineY = Height / 2;
            for (int x = 0; x < Width; x++)
            {
                int index = Index(Width, x, baselineY);
                if (index >= n) break;
                if (destination[index] < BaselineLevel) destination[index] = BaselineLevel;
            }
        }

        public void Resize(int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            if (width == Width && height == Height && _level.Length == width * height) return;

            Width = width;
            Height = height;
            _level = new float[width * height];
            _writeX = 0;
            _lastY = height / 2;
        }

        public void Clear()
        {
            Array.Clear(_level, 0, _level.Length);
            _lastY = Height / 2;
        }

        /// <summary>
        /// 写入一列采样；<paramref name="value"/> 为 0..1（0 = 波形底部，1 = 波形顶部）。
        /// 竖直方向补齐上一列与本列之间的整段，保证折线连续不断。
        ///
        /// 方向说明：<c>SetPixels32</c> 的第 0 个像素是纹理<b>左下角</b>，
        /// 而 GUI 会把纹理顶行画在矩形顶部，所以 value=1（峰值）要映射到 <c>y = Height-1</c>。
        /// 若写成 <c>(1-value)*(Height-1)</c>，整条波形会上下颠倒（R 波朝下）。
        /// 游戏自带的 ECGVisualizer 同样是「+1 → height-1」。
        /// </summary>
        public void Push(float value)
        {
            if (_level.Length == 0) return;

            _lastY = WriteColumn(_writeX, value, _lastY);
            _writeX = (_writeX + 1) % Width;

            Decay();
        }

        /// <summary>
        /// 回填一整屏历史波形（一次性写满，而不是从空白开始慢慢长）。
        ///
        /// 与 <see cref="Push"/> 的区别是<b>不做余辉衰减</b>：回填要一次写 W 列，
        /// 若每列都衰减一次，先写的列会被后面的衰减吃掉、左边直接消失。
        /// 回填后各列等亮，之后正常写入时再逐列衰减，于是自然形成「越靠左越旧越暗」的余辉。
        /// </summary>
        /// <param name="values">按时间从旧到新排列的采样值（0..1）。</param>
        public void Backfill(float[] values)
        {
            if (_level.Length == 0 || values == null || values.Length == 0) return;

            Array.Clear(_level, 0, _level.Length);

            int count = Math.Min(values.Length, Width);
            int last = Height / 2;
            for (int x = 0; x < count; x++)
            {
                last = WriteColumn(x, values[x], last);
            }

            _lastY = last;
            _writeX = count % Width;
        }

        /// <summary>写一列（含竖直补线与相邻列预填），返回该列的像素行。不做衰减。</summary>
        private int WriteColumn(int x, float value, int lastY)
        {
            if (x < 0 || x >= Width) return lastY;

            float v = value < 0f ? 0f : (value > 1f ? 1f : value);
            int target = (int)Math.Round(v * (Height - 1), MidpointRounding.AwayFromZero);
            if (target < 0) target = 0;
            if (target > Height - 1) target = Height - 1;

            int lo = Math.Min(lastY, target);
            int hi = Math.Max(lastY, target);

            // 竖直补线：只写孤立采样点的话，点与点之间会看起来像“零碎小点”
            for (int y = lo; y <= hi; y++)
            {
                Set(x, y, 255f);
                Set(x, y + 1, 110f);
                Set(x, y - 1, 110f);
            }

            // 相邻列轻微加宽，避免快速变化时出现断点
            int neighbour = (x + 1) % Width;
            for (int y = lo; y <= hi; y++)
            {
                Set(neighbour, y, 60f);
            }

            return target;
        }

        /// <summary>整幅余辉衰减；低于半级直接归零，保证能真正淡干净。</summary>
        public void Decay()
        {
            for (int i = 0; i < _level.Length; i++)
            {
                float level = _level[i];
                if (level <= 0f) continue;

                level *= DecayPerColumn;
                _level[i] = level < 0.5f ? 0f : level;
            }
        }

        private void Set(int x, int y, float value)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;

            int index = Index(Width, x, y);
            if (_level[index] < value) _level[index] = value;
        }
    }
}
