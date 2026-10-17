using System;
using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI
{
    /// <summary>
    /// 波形条：一块“滚动纸带”。
    ///
    /// 采样与余辉逻辑全部在 <see cref="WaveformBuffer"/> 里（无 Unity 依赖、可单测），
    /// 本类只负责把它变成一张纹理并上传。只在真正写入新列的那一帧重建像素缓冲，
    /// 空闲时不产生任何开销。实现思路与游戏自带的 ECGVisualizer 一致。
    /// </summary>
    internal sealed class WaveformStrip : IDisposable
    {
        private readonly Color _color;
        private readonly WaveformBuffer _buffer = new WaveformBuffer();

        private Texture2D _tex;
        private Color32[] _pixels;
        private byte[] _alphaBytes;
        private bool _dirty;

        public WaveformStrip(Color color)
        {
            _color = color;
            _buffer.Resize(64, 32);
        }

        public Texture2D Texture
        {
            get { return _tex; }
        }

        /// <summary>当前纸带列数（= 纹理宽度）。</summary>
        public int Width
        {
            get { return _buffer.Width; }
        }

        /// <summary>尺寸变化时重建缓冲与纹理（只在缩放/分辨率变化时发生）。</summary>
        public void EnsureSize(int width, int height)
        {
            width = Mathf.Clamp(width, 16, 2048);
            height = Mathf.Clamp(height, 8, 256);
            if (_tex != null && width == _buffer.Width && height == _buffer.Height) return;

            DisposeTexture();

            _buffer.Resize(width, height);
            _pixels = new Color32[width * height];
            _alphaBytes = new byte[width * height];

            _tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            _tex.wrapMode = TextureWrapMode.Clamp;
            _tex.filterMode = FilterMode.Bilinear;
            _tex.hideFlags = HideFlags.HideAndDontSave;

            _dirty = true;
            Flush();
        }

        public void Clear()
        {
            _buffer.Clear();
            _dirty = true;
        }

        /// <summary>写入一列采样；<paramref name="value"/> 为 0..1（0 = 波形底部，1 = 波形顶部）。</summary>
        public void Push(float value)
        {
            _buffer.Push(value);
            _dirty = true;
        }

        /// <summary>一次性铺满整屏（见 <see cref="WaveformBuffer.Backfill"/>）。</summary>
        public void Backfill(float[] values)
        {
            _buffer.Backfill(values);
            _dirty = true;
        }

        /// <summary>把缓冲合成到颜色缓冲并上传；无新数据时直接返回。</summary>
        public void Flush()
        {
            if (_tex == null || _pixels == null || _alphaBytes == null || !_dirty) return;

            byte r = (byte)(Mathf.Clamp01(_color.r) * 255f);
            byte g = (byte)(Mathf.Clamp01(_color.g) * 255f);
            byte b = (byte)(Mathf.Clamp01(_color.b) * 255f);

            // 行主序：缓冲索引 (y * Width + x) 与 SetPixels32 的期望顺序一致；
            // WriteBytes 里会额外叠加常驻的等电位基线
            _buffer.WriteBytes(_alphaBytes, _pixels.Length);
            for (int i = 0; i < _pixels.Length; i++)
            {
                _pixels[i] = new Color32(r, g, b, _alphaBytes[i]);
            }

            _tex.SetPixels32(_pixels);
            _tex.Apply(false);
            _dirty = false;
        }

        public void Dispose()
        {
            DisposeTexture();
            _pixels = null;
            _alphaBytes = null;
        }

        private void DisposeTexture()
        {
            if (_tex == null) return;
            UnityEngine.Object.Destroy(_tex);
            _tex = null;
        }
    }

    /// <summary>
    /// ECG 与呼吸波两条纸带。
    ///
    /// <b>回填历史：</b>真机打开就是满屏连续波形，而不是从空白开始一点点长出来。
    /// 因此纸带建立/清空后会**一次性铺满整屏**：用 <see cref="EcgTimeline"/>
    /// 把每一列对应的时间点折进 <c>GetECGHeight</c> 的有效相位窗口，
    /// 采样出过去约 4.6 秒的真实曲线 —— 一打开就是连续好几个心跳。
    ///
    /// ECG 取自游戏真实曲线（<c>Body.GetECGHeight</c>）；呼吸波是依据呼吸频率推算的示意波形
    /// （游戏没有提供呼吸曲线），其数值标签是真实读数。
    /// </summary>
    internal sealed class WaveformSet : IDisposable
    {
        /// <summary>每列代表的秒数（约 4.6 秒铺满一屏，一屏 5~8 个心跳）。</summary>
        public const float SecondsPerColumn = 0.010f;

        public readonly WaveformStrip Ecg;
        public readonly WaveformStrip Resp;

        private float _ecgAccum;
        private float _respAccum;
        private float _respPhase;
        private int _width = -1;
        private int _height = -1;
        /// <summary>上一帧是否还有信号（失去本地玩家时把纸带清空，而不是继续画假平线）。</summary>
        private bool _hasSignal;
        /// <summary>纸带重建/清空后需要回填整屏历史。</summary>
        private bool _needsBackfill = true;
        /// <summary>回填用的采样缓冲（复用，避免每次分配）。</summary>
        private float[] _backfill;

        public WaveformSet()
        {
            Ecg = new WaveformStrip(Theme.ChEcg);
            Resp = new WaveformStrip(Theme.ChResp);
        }

        /// <summary>
        /// 尺寸变化时重建纸带并复位相位——否则缩放瞬间会看到
        /// 「纸带被清空但相位从原处继续」造成的波形撕裂。
        /// </summary>
        public void EnsureSize(int width, int height)
        {
            if (width == _width && height == _height) return;

            _width = width;
            _height = height;
            Ecg.EnsureSize(width, height);
            Resp.EnsureSize(width, height);
            Clear();
        }

        public void Clear()
        {
            Ecg.Clear();
            Resp.Clear();
            _ecgAccum = 0f;
            _respAccum = 0f;
            _respPhase = 0f;
            _needsBackfill = true;
        }

        /// <summary>
        /// 推进波形。<paramref name="body"/> 可为 null（此时 ECG 走平线）。
        /// </summary>
        public void Advance(float deltaTime, Body body, BodySnapshot snapshot)
        {
            if (deltaTime <= 0f) return;
            if (deltaTime > 0.25f) deltaTime = 0.25f; // 卡顿后不要一次补齐上百列

            bool valid = snapshot != null && snapshot.Valid;
            if (!valid)
            {
                // 没有本地玩家（主菜单/加载中）：清空纸带，避免留下一条假平线与过期数值
                if (_hasSignal)
                {
                    Clear();
                    _hasSignal = false;
                }
                return;
            }
            _hasSignal = true;

            // 纸带刚建立（或刚被清空）：一次性把整屏历史铺出来，
            // 否则要等好几秒才能看到完整波形，屏幕上会显得空落。
            if (_needsBackfill)
            {
                _needsBackfill = false;
                BackfillEcg(body, snapshot.HeartRate);
            }

            float respiration = snapshot.RespiratoryPerMin;
            bool breathing = snapshot.Breathing;

            // ---- ECG：采样游戏的心电曲线，逐列推进 ----
            _ecgAccum += deltaTime;
            int guard = 0;
            while (_ecgAccum >= SecondsPerColumn && guard++ < 64)
            {
                float height = 0f;
                if (body != null)
                {
                    try
                    {
                        height = body.GetECGHeight(_ecgAccum - SecondsPerColumn);
                    }
                    catch (Exception)
                    {
                        height = 0f;
                    }
                }
                Ecg.Push((height + 1f) * 0.5f);
                _ecgAccum -= SecondsPerColumn;
            }

            // ---- 呼吸波：按呼吸频率推进相位 ----
            float breathsPerSecond = Mathf.Max(0.05f, respiration) / 60f;
            _respAccum += deltaTime;
            guard = 0;
            while (_respAccum >= SecondsPerColumn && guard++ < 64)
            {
                _respPhase += SecondsPerColumn * breathsPerSecond;
                if (_respPhase > 1f) _respPhase -= 1f;

                float amplitude = breathing ? 0.42f : 0.05f;
                Resp.Push(0.5f + amplitude * Mathf.Sin(_respPhase * Mathf.PI * 2f));
                _respAccum -= SecondsPerColumn;
            }
        }

        /// <summary>
        /// 一次性铺满整屏 ECG 历史。每列的时间点用 <see cref="EcgTimeline"/> 折进
        /// 游戏心电曲线的有效相位窗口，因此拿到的都是**真实曲线**在过去时刻的取值。
        /// </summary>
        private void BackfillEcg(Body body, float heartRate)
        {
            int width = Ecg.Width;
            if (width <= 0) return;

            if (_backfill == null || _backfill.Length != width) _backfill = new float[width];

            float prog = 0f;
            if (body != null)
            {
                try
                {
                    prog = body.heartProg;
                }
                catch (Exception)
                {
                    prog = 0f;
                }
            }

            for (int x = 0; x < width; x++)
            {
                // x = 0 是最旧的一列，最右列对应当前时刻
                float secondsBack = (width - 1 - x) * SecondsPerColumn;

                float height = 0f;
                if (body != null)
                {
                    try
                    {
                        float offset = EcgTimeline.OffsetFor(secondsBack, heartRate, prog);
                        height = body.GetECGHeight(offset);
                    }
                    catch (Exception)
                    {
                        height = 0f;
                    }
                }

                _backfill[x] = (height + 1f) * 0.5f;
            }

            Ecg.Backfill(_backfill);
        }

        /// <summary>把波形的新数据推给 GPU。</summary>
        public void Flush()
        {
            Ecg.Flush();
            Resp.Flush();
        }

        public void Dispose()
        {
            Ecg.Dispose();
            Resp.Dispose();
        }
    }
}
