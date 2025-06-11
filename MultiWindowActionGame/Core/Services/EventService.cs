// Core/Services/EventService.cs
using MultiWindowActionGame.Core.Services.Interfaces;
using System.Collections.Concurrent;

namespace MultiWindowActionGame.Core.Services
{
    /// <summary>
    /// イベント管理サービスの実装
    /// </summary>
    public class EventService : IEventService, IDisposable
    {
        private readonly ConcurrentDictionary<Type, ConcurrentBag<Delegate>> _subscribers = new();
        private readonly object _lock = new();
        private bool _disposed = false;

        public void Subscribe<T>(Action<T> handler) where T : class
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(handler);

            var eventType = typeof(T);
            _subscribers.AddOrUpdate(
                eventType,
                new ConcurrentBag<Delegate> { handler },
                (key, existing) =>
                {
                    existing.Add(handler);
                    return existing;
                });

            System.Diagnostics.Debug.WriteLine($"Subscribed to event: {eventType.Name}");
        }

        public void Unsubscribe<T>(Action<T> handler) where T : class
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(handler);

            var eventType = typeof(T);
            if (!_subscribers.TryGetValue(eventType, out var handlers))
                return;

            // ConcurrentBagからの削除は効率的ではないため、フィルタリングして新しいバッグを作成
            var remainingHandlers = handlers.Where(h => !ReferenceEquals(h, handler)).ToList();
            
            if (remainingHandlers.Count == 0)
            {
                _subscribers.TryRemove(eventType, out _);
            }
            else
            {
                _subscribers[eventType] = new ConcurrentBag<Delegate>(remainingHandlers);
            }

            System.Diagnostics.Debug.WriteLine($"Unsubscribed from event: {eventType.Name}");
        }

        public void Publish<T>(T eventArgs) where T : class
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(eventArgs);

            var eventType = typeof(T);
            if (!_subscribers.TryGetValue(eventType, out var handlers))
                return;

            var handlersList = handlers.ToList();
            var successCount = 0;
            var errorCount = 0;

            foreach (var handler in handlersList)
            {
                try
                {
                    if (handler is Action<T> typedHandler)
                    {
                        typedHandler(eventArgs);
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    System.Diagnostics.Debug.WriteLine($"Error in event handler for {eventType.Name}: {ex.Message}");
                    
                    // 重要: 一つのハンドラーでエラーが発生しても他のハンドラーは実行を続ける
                }
            }

            System.Diagnostics.Debug.WriteLine($"Published event {eventType.Name}: {successCount} successful, {errorCount} errors");
        }

        public async Task PublishAsync<T>(T eventArgs) where T : class
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(eventArgs);

            var eventType = typeof(T);
            if (!_subscribers.TryGetValue(eventType, out var handlers))
                return;

            var handlersList = handlers.ToList();
            var tasks = new List<Task>();

            foreach (var handler in handlersList)
            {
                if (handler is Action<T> typedHandler)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            typedHandler(eventArgs);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error in async event handler for {eventType.Name}: {ex.Message}");
                        }
                    }));
                }
            }

            await Task.WhenAll(tasks);
            System.Diagnostics.Debug.WriteLine($"Published async event {eventType.Name}: {tasks.Count} handlers");
        }

        public void Clear()
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                var eventTypes = _subscribers.Keys.ToList();
                _subscribers.Clear();
                
                System.Diagnostics.Debug.WriteLine($"Cleared all event subscriptions: {eventTypes.Count} event types");
            }
        }

        public int GetSubscriberCount<T>() where T : class
        {
            ThrowIfDisposed();
            
            var eventType = typeof(T);
            return _subscribers.TryGetValue(eventType, out var handlers) ? handlers.Count : 0;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EventService));
        }

        public void Dispose()
        {
            if (_disposed) return;

            Clear();
            _disposed = true;
        }
    }

    /// <summary>
    /// イベント関連のユーティリティクラス
    /// </summary>
    public static class EventHelper
    {
        /// <summary>
        /// 弱参照でイベントハンドラーを保持するラッパー
        /// </summary>
        public class WeakEventHandler<T> where T : class
        {
            private readonly WeakReference _targetRef;
            private readonly string _methodName;

            public WeakEventHandler(object target, Action<T> handler)
            {
                _targetRef = new WeakReference(target);
                _methodName = handler.Method.Name;
            }

            public bool TryHandle(T eventArgs)
            {
                var target = _targetRef.Target;
                if (target == null)
                    return false; // ターゲットがGCされた

                try
                {
                    var method = target.GetType().GetMethod(_methodName);
                    method?.Invoke(target, new object[] { eventArgs });
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in weak event handler: {ex.Message}");
                    return false;
                }
            }

            public bool IsAlive => _targetRef.IsAlive;
        }

        /// <summary>
        /// イベントバスパターンの実装
        /// </summary>
        public class EventBus : IDisposable
        {
            private readonly IEventService _eventService;
            private readonly Dictionary<string, List<Delegate>> _namedHandlers = new();
            private bool _disposed = false;

            public EventBus(IEventService eventService)
            {
                _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
            }

            public void Subscribe<T>(string eventName, Action<T> handler) where T : class
            {
                ThrowIfDisposed();
                
                if (!_namedHandlers.ContainsKey(eventName))
                {
                    _namedHandlers[eventName] = new List<Delegate>();
                }
                
                _namedHandlers[eventName].Add(handler);
                _eventService.Subscribe<T>(handler);
            }

            public void Unsubscribe<T>(string eventName, Action<T> handler) where T : class
            {
                ThrowIfDisposed();
                
                if (_namedHandlers.TryGetValue(eventName, out var handlers))
                {
                    handlers.Remove(handler);
                    if (handlers.Count == 0)
                    {
                        _namedHandlers.Remove(eventName);
                    }
                }
                
                _eventService.Unsubscribe<T>(handler);
            }

            public void Publish<T>(string eventName, T eventArgs) where T : class
            {
                ThrowIfDisposed();
                _eventService.Publish(eventArgs);
            }

            public async Task PublishAsync<T>(string eventName, T eventArgs) where T : class
            {
                ThrowIfDisposed();
                await _eventService.PublishAsync(eventArgs);
            }

            public IReadOnlyList<string> GetRegisteredEvents()
            {
                ThrowIfDisposed();
                return _namedHandlers.Keys.ToList();
            }

            private void ThrowIfDisposed()
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(EventBus));
            }

            public void Dispose()
            {
                if (_disposed) return;

                foreach (var handlers in _namedHandlers.Values)
                {
                    handlers.Clear();
                }
                _namedHandlers.Clear();
                
                _disposed = true;
            }
        }

        /// <summary>
        /// 条件付きイベントハンドラー
        /// </summary>
        public static Action<T> CreateConditionalHandler<T>(Predicate<T> condition, Action<T> handler) where T : class
        {
            return eventArgs =>
            {
                if (condition(eventArgs))
                {
                    handler(eventArgs);
                }
            };
        }

        /// <summary>
        /// 一度だけ実行されるイベントハンドラー
        /// </summary>
        public static Action<T> CreateOnceHandler<T>(Action<T> handler, IEventService eventService) where T : class
        {
            Action<T>? onceHandler = null;
            onceHandler = eventArgs =>
            {
                try
                {
                    handler(eventArgs);
                }
                finally
                {
                    if (onceHandler != null)
                    {
                        eventService.Unsubscribe<T>(onceHandler);
                    }
                }
            };
            return onceHandler;
        }

        /// <summary>
        /// 遅延実行イベントハンドラー
        /// </summary>
        public static Action<T> CreateDelayedHandler<T>(Action<T> handler, TimeSpan delay) where T : class
        {
            return eventArgs =>
            {
                Task.Delay(delay).ContinueWith(_ => handler(eventArgs));
            };
        }

        /// <summary>
        /// デバウンス機能付きイベントハンドラー
        /// </summary>
        public static Action<T> CreateDebouncedHandler<T>(Action<T> handler, TimeSpan debounceTime) where T : class
        {
            Timer? debounceTimer = null;
            T? lastEventArgs = null;

            return eventArgs =>
            {
                lastEventArgs = eventArgs;
                
                debounceTimer?.Dispose();
                debounceTimer = new Timer(_ =>
                {
                    if (lastEventArgs != null)
                    {
                        handler(lastEventArgs);
                        lastEventArgs = null;
                    }
                    debounceTimer?.Dispose();
                }, null, debounceTime, Timeout.InfiniteTimeSpan);
            };
        }

        /// <summary>
        /// スロットル機能付きイベントハンドラー
        /// </summary>
        public static Action<T> CreateThrottledHandler<T>(Action<T> handler, TimeSpan throttleTime) where T : class
        {
            var lastExecution = DateTime.MinValue;
            var lockObject = new object();

            return eventArgs =>
            {
                lock (lockObject)
                {
                    var now = DateTime.Now;
                    if (now - lastExecution >= throttleTime)
                    {
                        handler(eventArgs);
                        lastExecution = now;
                    }
                }
            };
        }
    }

    /// <summary>
    /// ゲーム固有のイベント定義
    /// </summary>
    public static class GameEvents
    {
        // ゲーム状態イベント
        public class GameStartedEvent
        {
            public DateTime StartTime { get; set; } = DateTime.Now;
        }

        public class GamePausedEvent
        {
            public DateTime PauseTime { get; set; } = DateTime.Now;
        }

        public class GameResumedEvent
        {
            public DateTime ResumeTime { get; set; } = DateTime.Now;
        }

        public class GameEndedEvent
        {
            public DateTime EndTime { get; set; } = DateTime.Now;
            public TimeSpan TotalPlayTime { get; set; }
        }

        // プレイヤーイベント
        public class PlayerCreatedEvent
        {
            public IPlayer Player { get; set; } = null!;
            public Point StartPosition { get; set; }
        }

        public class PlayerMovedEvent
        {
            public IPlayer Player { get; set; } = null!;
            public Point OldPosition { get; set; }
            public Point NewPosition { get; set; }
        }

        public class PlayerJumpedEvent
        {
            public IPlayer Player { get; set; } = null!;
            public float JumpForce { get; set; }
        }

        public class PlayerLandedEvent
        {
            public IPlayer Player { get; set; } = null!;
            public IGameWindow? LandedWindow { get; set; }
        }

        public class PlayerDeathEvent
        {
            public IPlayer Player { get; set; } = null!;
            public string DeathReason { get; set; } = "";
        }

        // ウィンドウイベント
        public class WindowCreatedEvent
        {
            public IGameWindow Window { get; set; } = null!;
            public WindowStrategyType StrategyType { get; set; }
        }

        public class WindowMovedEvent
        {
            public IGameWindow Window { get; set; } = null!;
            public Point OldPosition { get; set; }
            public Point NewPosition { get; set; }
        }

        public class WindowResizedEvent
        {
            public IGameWindow Window { get; set; } = null!;
            public Size OldSize { get; set; }
            public Size NewSize { get; set; }
        }

        public class WindowMinimizedEvent
        {
            public IGameWindow Window { get; set; } = null!;
            public DateTime MinimizeTime { get; set; } = DateTime.Now;
        }

        public class WindowRestoredEvent
        {
            public IGameWindow Window { get; set; } = null!;
            public DateTime RestoreTime { get; set; } = DateTime.Now;
        }

        // UI イベント
        public class ButtonClickedEvent
        {
            public IGameButton Button { get; set; } = null!;
            public ButtonType ButtonType { get; set; }
            public Point ClickPosition { get; set; }
        }

        public class SettingsChangedEvent
        {
            public string SettingName { get; set; } = "";
            public object? OldValue { get; set; }
            public object? NewValue { get; set; }
        }

        // ステージイベント
        public class StageStartedEvent
        {
            public int StageNumber { get; set; }
            public string StageName { get; set; } = "";
            public DateTime StartTime { get; set; } = DateTime.Now;
        }

        public class StageCompletedEvent
        {
            public int StageNumber { get; set; }
            public TimeSpan CompletionTime { get; set; }
            public bool IsLastStage { get; set; }
        }

        public class GoalReachedEvent
        {
            public IPlayer Player { get; set; } = null!;
            public IGoal Goal { get; set; } = null!;
            public int StageNumber { get; set; }
            public DateTime ReachedTime { get; set; } = DateTime.Now;
        }

        // デバッグイベント
        public class DebugModeChangedEvent
        {
            public bool IsDebugMode { get; set; }
        }

        public class PerformanceWarningEvent
        {
            public string WarningType { get; set; } = "";
            public float CurrentFPS { get; set; }
            public TimeSpan FrameTime { get; set; }
        }
    }
}