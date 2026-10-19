using System.Diagnostics;
using UnityEngine.Profiling;

namespace VHOLTAT.Dev
{
    /// <summary>
    /// Dev only (self-test): times VHOLTAT's per-frame movement work and counts managed heap growth inside it
    /// (Mono's used size before vs after, frames where a GC ran in between are skipped).
    /// </summary>
    internal static class PerfProbe
    {
        public static bool On;
        private static readonly Stopwatch Watch = new Stopwatch();
        private static long before;
        public static int Frames, AllocFrames;
        public static long AllocBytes;
        public static double TotalMs, MaxMs;

        public static void Begin()
        {
            before = Profiler.GetMonoUsedSizeLong();
            Watch.Restart();
        }

        public static void End()
        {
            Watch.Stop();
            long grew = Profiler.GetMonoUsedSizeLong() - before;
            double ms = Watch.Elapsed.TotalMilliseconds;
            Frames++;
            TotalMs += ms;
            if (ms > MaxMs) MaxMs = ms;
            if (grew > 0) { AllocFrames++; AllocBytes += grew; }
        }

        private static long moveBefore;
        public static long MoveAllocBytes;

        /// <summary>Allocation inside CharacterController.Move (vanilla's OnControllerColliderHit runs there).</summary>
        public static void MoveBegin() { if (On) moveBefore = Profiler.GetMonoUsedSizeLong(); }
        public static void MoveEnd()
        {
            if (!On) return;
            long grew = Profiler.GetMonoUsedSizeLong() - moveBefore;
            if (grew > 0) MoveAllocBytes += grew;
        }

        public static string Report() =>
            $"[perf] frames={Frames} avg={(Frames > 0 ? TotalMs / Frames : 0):0.000}ms max={MaxMs:0.000}ms allocFrames={AllocFrames} allocBytes={AllocBytes} (of which inside CharacterController.Move: {MoveAllocBytes})";

        public static void Reset()
        {
            Frames = AllocFrames = 0;
            AllocBytes = MoveAllocBytes = 0;
            TotalMs = MaxMs = 0;
        }
    }
}
