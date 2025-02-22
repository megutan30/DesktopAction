using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiWindowActionGame
{
    public class PerformanceMonitor
    {
        private static readonly Lazy<PerformanceMonitor> lazy =
            new Lazy<PerformanceMonitor>(() => new PerformanceMonitor());
        public static PerformanceMonitor Instance => lazy.Value;

        private readonly Dictionary<string, Stopwatch> timers = new();
        private readonly Queue<float> frameTimeHistory = new(60);
        private readonly Dictionary<string, float> averageTimes = new();
        private float totalFrameTime = 0;
        private int frameCount = 0;

        public class ScopedTimer : IDisposable
        {
            private readonly string name;
            private readonly Stopwatch stopwatch;

            public ScopedTimer(string name)
            {
                this.name = name;
                this.stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                stopwatch.Stop();
                Instance.RecordTime(name, stopwatch.ElapsedMilliseconds);
            }
        }

        public IDisposable BeginScope(string name)
        {
            return new ScopedTimer(name);
        }

        private void RecordTime(string name, long milliseconds)
        {
            if (!averageTimes.ContainsKey(name))
            {
                averageTimes[name] = milliseconds;
            }
            else
            {
                // 指数移動平均を使用
                averageTimes[name] = averageTimes[name] * 0.95f + milliseconds * 0.05f;
            }
        }

        public void UpdateFrameTime(float deltaTime)
        {
            frameTimeHistory.Enqueue(deltaTime);
            if (frameTimeHistory.Count > 60)
            {
                frameTimeHistory.Dequeue();
            }

            totalFrameTime += deltaTime;
            frameCount++;
        }

        public float GetAverageFrameTime()
        {
            return frameTimeHistory.Any() ? frameTimeHistory.Average() : 0;
        }

        public float GetCurrentFPS()
        {
            var avgFrameTime = GetAverageFrameTime();
            return avgFrameTime > 0 ? 1.0f / avgFrameTime : 0;
        }

        public IReadOnlyDictionary<string, float> GetTimings()
        {
            return averageTimes;
        }

        public void Reset()
        {
            frameTimeHistory.Clear();
            averageTimes.Clear();
            totalFrameTime = 0;
            frameCount = 0;
        }
    }
}
