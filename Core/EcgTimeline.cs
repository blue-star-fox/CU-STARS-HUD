using System;

namespace CuStatsHud.Core
{
    
    public static class EcgTimeline
    {
        
        public static float OffsetFor(float secondsBack, float heartRate, float heartProg)
        {
            if (secondsBack <= 0f) return 0f;

            float beatsPerSecond = BeatsPerSecond(heartRate);
            float cyclesBack = secondsBack * beatsPerSecond;   

            float wrapped = cyclesBack - (float)Math.Floor(cyclesBack);   

            float prog = heartProg;
            if (prog < 0f || prog > 1f) prog -= (float)Math.Floor(prog);   

            if (wrapped > prog) wrapped -= 1f;

            return wrapped / beatsPerSecond;
        }

        public static float PhaseFor(float offset, float heartRate, float heartProg)
        {
            return heartProg - offset * BeatsPerSecond(heartRate);
        }

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
