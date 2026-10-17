using System;
using CuStatsHud.Core;
using UnityEngine;

namespace CuStatsHud.UI
{
    
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

        public int Width
        {
            get { return _buffer.Width; }
        }

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

        public void Push(float value)
        {
            _buffer.Push(value);
            _dirty = true;
        }

        public void Backfill(float[] values)
        {
            _buffer.Backfill(values);
            _dirty = true;
        }

        public void Flush()
        {
            if (_tex == null || _pixels == null || _alphaBytes == null || !_dirty) return;

            byte r = (byte)(Mathf.Clamp01(_color.r) * 255f);
            byte g = (byte)(Mathf.Clamp01(_color.g) * 255f);
            byte b = (byte)(Mathf.Clamp01(_color.b) * 255f);

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

    internal sealed class WaveformSet : IDisposable
    {
        
        public const float SecondsPerColumn = 0.010f;

        public readonly WaveformStrip Ecg;
        public readonly WaveformStrip Resp;

        private float _ecgAccum;
        private float _respAccum;
        private float _respPhase;
        private int _width = -1;
        private int _height = -1;
        
        private bool _hasSignal;
        
        private bool _needsBackfill = true;
        
        private float[] _backfill;

        public WaveformSet()
        {
            Ecg = new WaveformStrip(Theme.ChEcg);
            Resp = new WaveformStrip(Theme.ChResp);
        }

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

        public void Advance(float deltaTime, Body body, BodySnapshot snapshot)
        {
            if (deltaTime <= 0f) return;
            if (deltaTime > 0.25f) deltaTime = 0.25f; 

            bool valid = snapshot != null && snapshot.Valid;
            if (!valid)
            {
                
                if (_hasSignal)
                {
                    Clear();
                    _hasSignal = false;
                }
                return;
            }
            _hasSignal = true;

            if (_needsBackfill)
            {
                _needsBackfill = false;
                BackfillEcg(body, snapshot.HeartRate);
            }

            float respiration = snapshot.RespiratoryPerMin;
            bool breathing = snapshot.Breathing;

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

            bool breathingNow = breathing && respiration >= 0.5f;
            float breathsPerSecond = breathingNow ? respiration / 60f : 0f;

            _respAccum += deltaTime;
            guard = 0;
            while (_respAccum >= SecondsPerColumn && guard++ < 64)
            {
                if (breathingNow)
                {
                    _respPhase += SecondsPerColumn * breathsPerSecond;
                    if (_respPhase > 1f) _respPhase -= 1f;
                }
                else
                {
                    _respPhase = 0f;
                }

                float amplitude = breathingNow ? 0.42f : 0f;
                Resp.Push(0.5f + amplitude * Mathf.Sin(_respPhase * Mathf.PI * 2f));
                _respAccum -= SecondsPerColumn;
            }
        }

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
