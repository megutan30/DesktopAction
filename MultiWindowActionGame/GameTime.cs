using System.Diagnostics;

namespace MultiWindowActionGame
{
    public static class GameTime
    {
        private static Stopwatch stopwatch = new Stopwatch();
        private static long lastTime = 0;

        public static float DeltaTime { get; private set; }
        public static float TotalTime { get; private set; }

        public static void Start()
        {
            stopwatch.Start();
        }

        public static void Update()
        {
            long currentTime = stopwatch.ElapsedMilliseconds;
            DeltaTime = (currentTime - lastTime) / 1000f;
            TotalTime = currentTime / 1000f;
            lastTime = currentTime;
        }
    }
}