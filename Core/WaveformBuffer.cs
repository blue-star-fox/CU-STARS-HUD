using System;

namespace CuStatsHud.Core
{
    
    public sealed class WaveformBuffer
    {
        
        public const float DecayPerColumn = 0.985f;

        private float[] _level = new float[0];
        private int _writeX;
        private int _lastY;

        public int Width { get; private set; }

        public int Height { get; private set; }

        public float[] Levels
        {
            get { return _level; }
        }

        public static int Index(int width, int x, int y)
        {
            return y * width + x;
        }

        public const byte BaselineLevel = 64;

        public byte At(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return 0;

            float level = _level[Index(Width, x, y)];
            if (level < 1f) return 0;
            if (level > 255f) return 255;
            return (byte)level;
        }

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

        public void Push(float value)
        {
            if (_level.Length == 0) return;

            _lastY = WriteColumn(_writeX, value, _lastY);
            _writeX = (_writeX + 1) % Width;

            Decay();
        }

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

        private int WriteColumn(int x, float value, int lastY)
        {
            if (x < 0 || x >= Width) return lastY;

            float v = value < 0f ? 0f : (value > 1f ? 1f : value);
            int target = (int)Math.Round(v * (Height - 1), MidpointRounding.AwayFromZero);
            if (target < 0) target = 0;
            if (target > Height - 1) target = Height - 1;

            int lo = Math.Min(lastY, target);
            int hi = Math.Max(lastY, target);

            for (int y = lo; y <= hi; y++)
            {
                Set(x, y, 255f);
                Set(x, y + 1, 110f);
                Set(x, y - 1, 110f);
            }

            int neighbour = (x + 1) % Width;
            for (int y = lo; y <= hi; y++)
            {
                Set(neighbour, y, 60f);
            }

            return target;
        }

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
