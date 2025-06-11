// Core/Services/PerformanceService.cs
using MultiWindowActionGame.Core.Services.Interfaces;
using MultiWindowActionGame.Core.Constants;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace MultiWindowActionGame.Core.Services
{
    /// <summary>
    /// パフォーマンス監視サービスの実装
    /// </summary>
    public class PerformanceService : IPerformanceService, IDisposable
    {
        private readonly Queue<float> _frameTimeHistory = new();
        private readonly ConcurrentDictionary<string, PerformanceMeasurement> _measurements = new();
        private readonly Stopwatch _totalRunTimeStopwatch = new();
        private readonly object _lock = new();
        
        private float _totalFrameTime = 0;
        private int _frameCount = 0;
        private bool _isEnabled = true;
        private bool _disposed = false;

        public float CurrentFPS { get; private set; }
        public float AverageFrameTime { get; private set; }
        public TimeSpan TotalRunTime => _totalRunTimeStopwatch.Elapsed;

        public event EventHandler<PerformanceReportEventArgs>? PerformanceReport;

        public PerformanceService()
        {
            _totalRunTimeStopwatch.Start();
        }

        public void UpdateFrameTime(float deltaTime)
        {
            if (!_isEnabled || _disposed) return;

            lock (_lock)
            {
                _frameTimeHistory.Enqueue(deltaTime);
                
                // 履歴サイズの制限
                while (_frameTimeHistory.Count > GameConstants.Gameplay.PERFORMANCE_HISTORY_SIZE)
                {
                    _frameTimeHistory.Dequeue();
                }

                _totalFrameTime += deltaTime;
                _frameCount++;

                // FPSとフレーム時間の計算
                if (_frameTimeHistory.Count > 0)
                {
                    AverageFrameTime = _frameTimeHistory.Average();
                    CurrentFPS = AverageFrameTime > 0 ? 1.0f / AverageFrameTime : 0;
                }

                // パフォーマンスレポートの発行（1秒ごと）
                if (_frameCount % GameConstants.Gameplay.DEFAULT_TARGET_FPS == 0)
                {
                    var reportArgs = new PerformanceReportEventArgs(
                        CurrentFPS,
                        TimeSpan.FromSeconds(AverageFrameTime),
                        GetMeasurements());
                    
                    PerformanceReport?.Invoke(this, reportArgs);
                }
            }
        }

        public IDisposable BeginMeasurement(string name)
        {
            ThrowIfDisposed();
            
            if (!_isEnabled)
                return new NullMeasurement();

            return new ScopedMeasurement(this, name);
        }

        public void RecordMeasurement(string name, TimeSpan duration)
        {
            ThrowIfDisposed();
            
            if (!_isEnabled) return;

            _measurements.AddOrUpdate(name,
                new PerformanceMeasurement
                {
                    Name = name,
                    Duration = duration,
                    AverageDuration = duration,
                    MinDuration = duration,
                    MaxDuration = duration,
                    CallCount = 1,
                    LastCalled = DateTime.Now
                },
                (key, existing) =>
                {
                    existing.CallCount++;
                    existing.Duration = duration;
                    existing.LastCalled = DateTime.Now;
                    
                    // 指数移動平均を使用
                    var alpha = GameConstants.Physics.FRAME_TIME_CONTRIBUTION;
                    existing.AverageDuration = TimeSpan.FromTicks(
                        (long)(existing.AverageDuration.Ticks * (1 - alpha) + duration.Ticks * alpha));
                    
                    if (duration < existing.MinDuration)
                        existing.MinDuration = duration;
                    
                    if (duration > existing.MaxDuration)
                        existing.MaxDuration = duration;
                    
                    return existing;
                });
        }

        public IReadOnlyDictionary<string, PerformanceMeasurement> GetMeasurements()
        {
            ThrowIfDisposed();
            return new Dictionary<string, PerformanceMeasurement>(_measurements);
        }

        public void Reset()
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                _frameTimeHistory.Clear();
                _measurements.Clear();
                _totalFrameTime = 0;
                _frameCount = 0;
                CurrentFPS = 0;
                AverageFrameTime = 0;
                _totalRunTimeStopwatch.Restart();
            }
        }

        public void SetEnabled(bool enabled)
        {
            ThrowIfDisposed();
            _isEnabled = enabled;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PerformanceService));
        }

        public void Dispose()
        {
            if (_disposed) return;

            _totalRunTimeStopwatch.Stop();
            _measurements.Clear();
            
            lock (_lock)
            {
                _frameTimeHistory.Clear();
            }
            
            _disposed = true;
        }

        /// <summary>
        /// スコープ付き測定の実装
        /// </summary>
        private class ScopedMeasurement : IDisposable
        {
            private readonly PerformanceService _service;
            private readonly string _name;
            private readonly Stopwatch _stopwatch;
            private bool _disposed = false;

            public ScopedMeasurement(PerformanceService service, string name)
            {
                _service = service;
                _name = name;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                if (_disposed) return;

                _stopwatch.Stop();
                _service.RecordMeasurement(_name, _stopwatch.Elapsed);
                _disposed = true;
            }
        }

        /// <summary>
        /// 無効な測定（何もしない）
        /// </summary>
        private class NullMeasurement : IDisposable
        {
            public void Dispose()
            {
                // 何もしない
            }
        }
    }

    /// <summary>
    /// パフォーマンス監視ユーティリティ
    /// </summary>
    public static class PerformanceHelper
    {
        /// <summary>
        /// 指定されたアクションの実行時間を測定する
        /// </summary>
        public static TimeSpan MeasureExecution(Action action)
        {
            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }

        /// <summary>
        /// 指定された非同期アクションの実行時間を測定する
        /// </summary>
        public static async Task<TimeSpan> MeasureExecutionAsync(Func<Task> action)
        {
            var stopwatch = Stopwatch.StartNew();
            await action();
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }

        /// <summary>
        /// 指定された関数の実行時間と結果を取得する
        /// </summary>
        public static (T Result, TimeSpan Duration) MeasureExecution<T>(Func<T> func)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = func();
            stopwatch.Stop();
            return (result, stopwatch.Elapsed);
        }

        /// <summary>
        /// 指定された非同期関数の実行時間と結果を取得する
        /// </summary>
        public static async Task<(T Result, TimeSpan Duration)> MeasureExecutionAsync<T>(Func<Task<T>> func)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await func();
            stopwatch.Stop();
            return (result, stopwatch.Elapsed);
        }

        /// <summary>
        /// メモリ使用量を取得する
        /// </summary>
        public static MemoryInfo GetMemoryInfo()
        {
            var process = Process.GetCurrentProcess();
            
            return new MemoryInfo
            {
                WorkingSet = process.WorkingSet64,
                PrivateMemorySize = process.PrivateMemorySize64,
                VirtualMemorySize = process.VirtualMemorySize64,
                PagedMemorySize = process.PagedMemorySize64,
                NonPagedSystemMemorySize = process.NonpagedSystemMemorySize64,
                PagedSystemMemorySize = process.PagedSystemMemorySize64,
                GCTotalMemory = GC.GetTotalMemory(false),
                GCGeneration0Collections = GC.CollectionCount(0),
                GCGeneration1Collections = GC.CollectionCount(1),
                GCGeneration2Collections = GC.CollectionCount(2)
            };
        }

        /// <summary>
        /// パフォーマンス警告をチェックする
        /// </summary>
        public static PerformanceWarning? CheckPerformanceWarnings(IPerformanceService performanceService)
        {
            var currentFPS = performanceService.CurrentFPS;
            var targetFPS = GameConstants.Gameplay.DEFAULT_TARGET_FPS;
            
            // FPS警告
            if (currentFPS < targetFPS * 0.5f) // 目標FPSの50%以下
            {
                return new PerformanceWarning
                {
                    Type = PerformanceWarningType.LowFPS,
                    Message = $"Low FPS detected: {currentFPS:F1} (target: {targetFPS})",
                    Severity = Warningseverity.High,
                    CurrentFPS = currentFPS,
                    FrameTime = TimeSpan.FromSeconds(performanceService.AverageFrameTime)
                };
            }
            else if (currentFPS < targetFPS * 0.8f) // 目標FPSの80%以下
            {
                return new PerformanceWarning
                {
                    Type = PerformanceWarningType.LowFPS,
                    Message = $"FPS below target: {currentFPS:F1} (target: {targetFPS})",
                    Severity = WarningLevel.Medium,
                    CurrentFPS = currentFPS,
                    FrameTime = TimeSpan.FromSeconds(performanceService.AverageFrameTime)
                };
            }

            // メモリ警告
            var memoryInfo = GetMemoryInfo();
            var memoryUsageMB = memoryInfo.WorkingSet / (1024 * 1024);
            
            if (memoryUsageMB > 1000) // 1GB以上
            {
                return new PerformanceWarning
                {
                    Type = PerformanceWarningType.HighMemoryUsage,
                    Message = $"High memory usage: {memoryUsageMB} MB",
                    Severity = WarningLevel.High,
                    MemoryUsage = memoryUsageMB
                };
            }

            return null;
        }

        /// <summary>
        /// 統計情報を計算する
        /// </summary>
        public static PerformanceStatistics CalculateStatistics(IReadOnlyDictionary<string, PerformanceMeasurement> measurements)
        {
            if (!measurements.Any())
            {
                return new PerformanceStatistics();
            }

            var totalDuration = measurements.Values.Sum(m => m.Duration.TotalMilliseconds);
            var averageDuration = measurements.Values.Average(m => m.AverageDuration.TotalMilliseconds);
            var minDuration = measurements.Values.Min(m => m.MinDuration.TotalMilliseconds);
            var maxDuration = measurements.Values.Max(m => m.MaxDuration.TotalMilliseconds);
            var totalCalls = measurements.Values.Sum(m => m.CallCount);

            // 最も時間のかかっている処理のTop5
            var slowestOperations = measurements.Values
                .OrderByDescending(m => m.AverageDuration.TotalMilliseconds)
                .Take(5)
                .ToList();

            // 最も頻繁に呼ばれている処理のTop5
            var mostFrequentOperations = measurements.Values
                .OrderByDescending(m => m.CallCount)
                .Take(5)
                .ToList();

            return new PerformanceStatistics
            {
                TotalMeasurements = measurements.Count,
                TotalDuration = TimeSpan.FromMilliseconds(totalDuration),
                AverageDuration = TimeSpan.FromMilliseconds(averageDuration),
                MinDuration = TimeSpan.FromMilliseconds(minDuration),
                MaxDuration = TimeSpan.FromMilliseconds(maxDuration),
                TotalCalls = totalCalls,
                SlowestOperations = slowestOperations,
                MostFrequentOperations = mostFrequentOperations
            };
        }
    }

    /// <summary>
    /// メモリ情報
    /// </summary>
    public class MemoryInfo
    {
        public long WorkingSet { get; set; }
        public long PrivateMemorySize { get; set; }
        public long VirtualMemorySize { get; set; }
        public long PagedMemorySize { get; set; }
        public long NonPagedSystemMemorySize { get; set; }
        public long PagedSystemMemorySize { get; set; }
        public long GCTotalMemory { get; set; }
        public int GCGeneration0Collections { get; set; }
        public int GCGeneration1Collections { get; set; }
        public int GCGeneration2Collections { get; set; }

        public double WorkingSetMB => WorkingSet / (1024.0 * 1024.0);
        public double GCTotalMemoryMB => GCTotalMemory / (1024.0 * 1024.0);
    }

    /// <summary>
    /// パフォーマンス警告
    /// </summary>
    public class PerformanceWarning
    {
        public PerformanceWarningType Type { get; set; }
        public string Message { get; set; } = "";
        public WarningLevel Severity { get; set; }
        public float CurrentFPS { get; set; }
        public TimeSpan FrameTime { get; set; }
        public long MemoryUsage { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// パフォーマンス統計
    /// </summary>
    public class PerformanceStatistics
    {
        public int TotalMeasurements { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public TimeSpan MinDuration { get; set; }
        public TimeSpan MaxDuration { get; set; }
        public int TotalCalls { get; set; }
        public IReadOnlyList<PerformanceMeasurement> SlowestOperations { get; set; } = new List<PerformanceMeasurement>();
        public IReadOnlyList<PerformanceMeasurement> MostFrequentOperations { get; set; } = new List<PerformanceMeasurement>();
    }

    /// <summary>
    /// パフォーマンス警告の種類
    /// </summary>
    public enum PerformanceWarningType
    {
        LowFPS,
        HighFrameTime,
        HighMemoryUsage,
        HighGCPressure,
        SlowOperation
    }

    /// <summary>
    /// 警告レベル
    /// </summary>
    public enum WarningLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// パフォーマンス分析器
    /// </summary>
    public class PerformanceAnalyzer : IDisposable
    {
        private readonly IPerformanceService _performanceService;
        private readonly IEventService _eventService;
        private readonly Timer _analysisTimer;
        private readonly List<PerformanceWarning> _warningHistory = new();
        private bool _disposed = false;

        public PerformanceAnalyzer(IPerformanceService performanceService, IEventService eventService)
        {
            _performanceService = performanceService ?? throw new ArgumentNullException(nameof(performanceService));
            _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));

            // 5秒ごとに分析を実行
            _analysisTimer = new Timer(AnalyzePerformance, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        private void AnalyzePerformance(object? state)
        {
            if (_disposed) return;

            try
            {
                var warning = PerformanceHelper.CheckPerformanceWarnings(_performanceService);
                if (warning != null)
                {
                    _warningHistory.Add(warning);
                    
                    // 警告履歴の制限（最新の100件まで）
                    while (_warningHistory.Count > 100)
                    {
                        _warningHistory.RemoveAt(0);
                    }

                    // イベントを発行
                    _eventService.Publish(new GameEvents.PerformanceWarningEvent
                    {
                        WarningType = warning.Type.ToString(),
                        CurrentFPS = warning.CurrentFPS,
                        FrameTime = warning.FrameTime
                    });
                }

                // 詳細な統計を計算
                var measurements = _performanceService.GetMeasurements();
                var statistics = PerformanceHelper.CalculateStatistics(measurements);

                // 統計情報をログ出力（デバッグ時のみ）
                #if DEBUG
                if (statistics.TotalMeasurements > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Performance Stats: {statistics.TotalMeasurements} measurements, " +
                        $"Avg: {statistics.AverageDuration.TotalMilliseconds:F2}ms, " +
                        $"FPS: {_performanceService.CurrentFPS:F1}");
                }
                #endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in performance analysis: {ex.Message}");
            }
        }

        public IReadOnlyList<PerformanceWarning> GetWarningHistory()
        {
            ThrowIfDisposed();
            return _warningHistory.ToList();
        }

        public PerformanceWarning? GetLatestWarning()
        {
            ThrowIfDisposed();
            return _warningHistory.LastOrDefault();
        }

        public void ClearWarningHistory()
        {
            ThrowIfDisposed();
            _warningHistory.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PerformanceAnalyzer));
        }

        public void Dispose()
        {
            if (_disposed) return;

            _analysisTimer?.Dispose();
            _warningHistory.Clear();
            _disposed = true;
        }
    }

    /// <summary>
    /// プロファイラー（より詳細な分析用）
    /// </summary>
    public class Profiler : IDisposable
    {
        private readonly Dictionary<string, ProfilerEntry> _entries = new();
        private readonly Stack<ProfilerEntry> _callStack = new();
        private readonly object _lock = new();
        private bool _disposed = false;

        public IDisposable BeginProfile(string name)
        {
            ThrowIfDisposed();
            return new ProfilerScope(this, name);
        }

        internal void StartProfile(string name)
        {
            lock (_lock)
            {
                var entry = new ProfilerEntry
                {
                    Name = name,
                    StartTime = DateTime.Now,
                    Stopwatch = Stopwatch.StartNew()
                };

                if (_callStack.Count > 0)
                {
                    entry.Parent = _callStack.Peek();
                    _callStack.Peek().Children.Add(entry);
                }

                _callStack.Push(entry);
            }
        }

        internal void EndProfile(string name)
        {
            lock (_lock)
            {
                if (_callStack.Count == 0) return;

                var entry = _callStack.Pop();
                if (entry.Name != name)
                {
                    System.Diagnostics.Debug.WriteLine($"Profile stack mismatch: expected {name}, got {entry.Name}");
                    return;
                }

                entry.Stopwatch.Stop();
                entry.EndTime = DateTime.Now;
                entry.Duration = entry.Stopwatch.Elapsed;

                if (!_entries.ContainsKey(name))
                {
                    _entries[name] = entry;
                }
                else
                {
                    // 統計情報を更新
                    var existing = _entries[name];
                    existing.CallCount++;
                    existing.TotalDuration += entry.Duration;
                    existing.AverageDuration = TimeSpan.FromTicks(existing.TotalDuration.Ticks / existing.CallCount);
                    
                    if (entry.Duration < existing.MinDuration || existing.MinDuration == TimeSpan.Zero)
                        existing.MinDuration = entry.Duration;
                    
                    if (entry.Duration > existing.MaxDuration)
                        existing.MaxDuration = entry.Duration;
                }
            }
        }

        public ProfilerReport GenerateReport()
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                return new ProfilerReport
                {
                    GeneratedAt = DateTime.Now,
                    Entries = _entries.Values.ToList(),
                    TotalProfiledTime = _entries.Values.Sum(e => e.TotalDuration.TotalMilliseconds),
                    TotalCalls = _entries.Values.Sum(e => e.CallCount)
                };
            }
        }

        public void Reset()
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                _entries.Clear();
                _callStack.Clear();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Profiler));
        }

        public void Dispose()
        {
            if (_disposed) return;

            Reset();
            _disposed = true;
        }

        private class ProfilerScope : IDisposable
        {
            private readonly Profiler _profiler;
            private readonly string _name;
            private bool _disposed = false;

            public ProfilerScope(Profiler profiler, string name)
            {
                _profiler = profiler;
                _name = name;
                _profiler.StartProfile(name);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _profiler.EndProfile(_name);
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// プロファイラーエントリ
    /// </summary>
    public class ProfilerEntry
    {
        public string Name { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public TimeSpan MinDuration { get; set; }
        public TimeSpan MaxDuration { get; set; }
        public int CallCount { get; set; } = 1;
        public Stopwatch Stopwatch { get; set; } = new();
        public ProfilerEntry? Parent { get; set; }
        public List<ProfilerEntry> Children { get; set; } = new();
    }

    /// <summary>
    /// プロファイラーレポート
    /// </summary>
    public class ProfilerReport
    {
        public DateTime GeneratedAt { get; set; }
        public IReadOnlyList<ProfilerEntry> Entries { get; set; } = new List<ProfilerEntry>();
        public double TotalProfiledTime { get; set; }
        public int TotalCalls { get; set; }

        public string GenerateTextReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Profiler Report - Generated at {GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Total Profiled Time: {TotalProfiledTime:F2}ms");
            sb.AppendLine($"Total Calls: {TotalCalls}");
            sb.AppendLine();

            sb.AppendLine("Top 10 Slowest Operations:");
            var slowest = Entries.OrderByDescending(e => e.AverageDuration.TotalMilliseconds).Take(10);
            foreach (var entry in slowest)
            {
                sb.AppendLine($"  {entry.Name}: {entry.AverageDuration.TotalMilliseconds:F2}ms avg ({entry.CallCount} calls)");
            }

            sb.AppendLine();
            sb.AppendLine("Top 10 Most Frequent Operations:");
            var frequent = Entries.OrderByDescending(e => e.CallCount).Take(10);
            foreach (var entry in frequent)
            {
                sb.AppendLine($"  {entry.Name}: {entry.CallCount} calls ({entry.AverageDuration.TotalMilliseconds:F2}ms avg)");
            }

            return sb.ToString();
        }
    }
}