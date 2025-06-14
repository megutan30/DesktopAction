// Core/Services/WindowManagerService.cs
using MultiWindowActionGame.Core.Services.Interfaces;
using MultiWindowActionGame.Core.Interfaces;
using System.Collections.Concurrent;

namespace MultiWindowActionGame.Core.Services
{
    /// <summary>
    /// ウィンドウ管理サービスの統合実装
    /// </summary>
    public class WindowManagerService : IWindowManagerService, IDisposable
    {
        private readonly IZOrderService _zOrderService;
        private readonly IWindowHierarchyService _hierarchyService;
        private readonly IWindowCollisionService _collisionService;
        private readonly INoEntryZoneService _noEntryZoneService;
        private readonly IEventService _eventService;
        
        private readonly ConcurrentDictionary<Guid, IGameWindow> _windows = new();
        private readonly List<IGameWindow> _windowOrder = new();
        private readonly object _lock = new();
        private bool _disposed = false;

        public event EventHandler<WindowRegisteredEventArgs>? WindowRegistered;
        public event EventHandler<WindowUnregisteredEventArgs>? WindowUnregistered;
        public event EventHandler<WindowOrderChangedEventArgs>? WindowOrderChanged;

        public WindowManagerService(
            IZOrderService zOrderService,
            IWindowHierarchyService hierarchyService,
            IWindowCollisionService collisionService,
            INoEntryZoneService noEntryZoneService,
            IEventService eventService)
        {
            _zOrderService = zOrderService ?? throw new ArgumentNullException(nameof(zOrderService));
            _hierarchyService = hierarchyService ?? throw new ArgumentNullException(nameof(hierarchyService));
            _collisionService = collisionService ?? throw new ArgumentNullException(nameof(collisionService));
            _noEntryZoneService = noEntryZoneService ?? throw new ArgumentNullException(nameof(noEntryZoneService));
            _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
        }

        public IReadOnlyList<IGameWindow> GetAllWindows()
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                return _windowOrder.ToList();
            }
        }

        public IReadOnlyList<IGameWindow> GetActiveWindows()
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                return _windowOrder.Where(w => !w.IsMinimized).ToList();
            }
        }

        public void RegisterWindow(IGameWindow window)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(window);

            lock (_lock)
            {
                if (_windows.TryAdd(window.Id, window))
                {
                    _windowOrder.Add(window);
                    
                    // 各サービスに登録
                    if (window is IZOrderable zOrderable)
                    {
                        _zOrderService.RegisterWindow(zOrderable);
                    }
                    
                    // 階層関係を更新
                    _hierarchyService.UpdateHierarchy(window);
                    
                    // イベントを発行
                    var eventArgs = new WindowRegisteredEventArgs(window);
                    WindowRegistered?.Invoke(this, eventArgs);
                    
                    _eventService.Publish(new WindowRegisteredEvent
                    {
                        Window = window,
                        TotalWindows = _windows.Count,
                        Timestamp = DateTime.Now
                    });

                    System.Diagnostics.Debug.WriteLine($"Registered window: {window.Id}");
                }
            }
        }

        public void UnregisterWindow(IGameWindow window)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(window);

            lock (_lock)
            {
                if (_windows.TryRemove(window.Id, out var removedWindow))
                {
                    _windowOrder.Remove(removedWindow);
                    
                    // 各サービスから登録解除
                    if (removedWindow is IZOrderable zOrderable)
                    {
                        _zOrderService.UnregisterWindow(zOrderable);
                    }
                    
                    _hierarchyService.RemoveFromHierarchy(removedWindow);
                    
                    // イベントを発行
                    var eventArgs = new WindowUnregisteredEventArgs(removedWindow);
                    WindowUnregistered?.Invoke(this, eventArgs);
                    
                    _eventService.Publish(new WindowUnregisteredEvent
                    {
                        Window = removedWindow,
                        TotalWindows = _windows.Count,
                        Timestamp = DateTime.Now
                    });

                    System.Diagnostics.Debug.WriteLine($"Unregistered window: {removedWindow.Id}");
                }
            }
        }

        public IGameWindow? GetWindowAt(Rectangle bounds)
        {
            ThrowIfDisposed();
            
            var activeWindows = GetActiveWindows();
            return _collisionService.GetTopWindowAt(bounds, activeWindows);
        }

        public IGameWindow? GetTopWindowAt(Rectangle bounds, IGameWindow? exclude = null)
        {
            ThrowIfDisposed();
            
            var activeWindows = GetActiveWindows();
            if (exclude != null)
            {
                activeWindows = activeWindows.Where(w => w.Id != exclude.Id).ToList();
            }
            
            return _collisionService.GetTopWindowAt(bounds, activeWindows);
        }

        public IGameWindow? GetWindowById(Guid id)
        {
            ThrowIfDisposed();
            
            _windows.TryGetValue(id, out var window);
            return window;
        }

        public void BringWindowToFront(IGameWindow window)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(window);

            lock (_lock)
            {
                var oldOrder = GetWindowOrder(window);
                
                // Z-Order サービスで最前面に移動
                if (window is IZOrderable zOrderable)
                {
                    _zOrderService.BringToFront(zOrderable);
                }
                
                // ウィンドウリストでも最前面に移動
                _windowOrder.Remove(window);
                _windowOrder.Add(window);
                
                // 階層関係を更新
                _hierarchyService.UpdateHierarchy(window);
                
                var newOrder = GetWindowOrder(window);
                
                // イベントを発行
                var eventArgs = new WindowOrderChangedEventArgs(window, oldOrder, newOrder);
                WindowOrderChanged?.Invoke(this, eventArgs);
                
                _eventService.Publish(new WindowOrderChangedEvent
                {
                    Window = window,
                    OldOrder = oldOrder,
                    NewOrder = newOrder,
                    Timestamp = DateTime.Now
                });
            }
        }

        public void SendWindowToBack(IGameWindow window)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(window);

            lock (_lock)
            {
                var oldOrder = GetWindowOrder(window);
                
                // Z-Order サービスで最背面に移動
                if (window is IZOrderable zOrderable)
                {
                    _zOrderService.SendToBack(zOrderable);
                }
                
                // ウィンドウリストでも最背面に移動
                _windowOrder.Remove(window);
                _windowOrder.Insert(0, window);
                
                var newOrder = GetWindowOrder(window);
                
                // イベントを発行
                var eventArgs = new WindowOrderChangedEventArgs(window, oldOrder, newOrder);
                WindowOrderChanged?.Invoke(this, eventArgs);
            }
        }

        public void UpdateWindowOrders()
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                var allWindows = _windowOrder.ToList();
                var zOrderableWindows = allWindows.OfType<IZOrderable>().ToList();
                
                _zOrderService.UpdateOrders(zOrderableWindows);
                
                // すべてのウィンドウの階層関係を更新
                foreach (var window in allWindows)
                {
                    _hierarchyService.UpdateHierarchy(window);
                }
            }
        }

        public async Task UpdateAsync(float deltaTime)
        {
            ThrowIfDisposed();
            
            var windows = GetAllWindows();
            var updateTasks = windows.Select(w => w.UpdateAsync(deltaTime));
            
            await Task.WhenAll(updateTasks);
        }

        public void ClearAllWindows()
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                var windowsToRemove = _windows.Values.ToList();
                
                foreach (var window in windowsToRemove)
                {
                    UnregisterWindow(window);
                }
                
                _windows.Clear();
                _windowOrder.Clear();
            }
            
            System.Diagnostics.Debug.WriteLine("Cleared all windows");
        }

        private int GetWindowOrder(IGameWindow window)
        {
            lock (_lock)
            {
                return _windowOrder.IndexOf(window);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WindowManagerService));
        }

        public void Dispose()
        {
            if (_disposed) return;

            ClearAllWindows();
            _disposed = true;
        }
    }

    /// <summary>
    /// ウィンドウ管理関連のイベント
    /// </summary>
    public class WindowRegisteredEvent
    {
        public IGameWindow Window { get; set; } = null!;
        public int TotalWindows { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class WindowUnregisteredEvent
    {
        public IGameWindow Window { get; set; } = null!;
        public int TotalWindows { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class WindowOrderChangedEvent
    {
        public IGameWindow Window { get; set; } = null!;
        public int OldOrder { get; set; }
        public int NewOrder { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// ウィンドウ管理関連のユーティリティメソッド
    /// </summary>
    public static class WindowManagerHelper
    {
        /// <summary>
        /// ウィンドウの配置を最適化
        /// </summary>
        public static void OptimizeWindowLayout(IWindowManagerService windowManager, Rectangle availableArea)
        {
            var windows = windowManager.GetActiveWindows();
            if (!windows.Any()) return;

            // 簡単なタイル配置アルゴリズム
            var cols = (int)Math.Ceiling(Math.Sqrt(windows.Count));
            var rows = (int)Math.Ceiling((double)windows.Count / cols);
            
            var windowWidth = availableArea.Width / cols;
            var windowHeight = availableArea.Height / rows;

            for (int i = 0; i < windows.Count; i++)
            {
                var col = i % cols;
                var row = i / cols;
                
                var x = availableArea.X + col * windowWidth;
                var y = availableArea.Y + row * windowHeight;
                
                if (windows[i] is ITransformable transformable)
                {
                    transformable.SetPosition(new Point(x, y));
                    transformable.SetSize(new Size(windowWidth, windowHeight));
                }
            }
        }

        /// <summary>
        /// 重複するウィンドウを検出
        /// </summary>
        public static IReadOnlyList<(IGameWindow, IGameWindow)> FindOverlappingWindows(IWindowManagerService windowManager)
        {
            var windows = windowManager.GetActiveWindows();
            var overlapping = new List<(IGameWindow, IGameWindow)>();

            for (int i = 0; i < windows.Count; i++)
            {
                for (int j = i + 1; j < windows.Count; j++)
                {
                    if (windows[i].Bounds.IntersectsWith(windows[j].Bounds))
                    {
                        overlapping.Add((windows[i], windows[j]));
                    }
                }
            }

            return overlapping;
        }

        /// <summary>
        /// ウィンドウの統計情報を計算
        /// </summary>
        public static WindowManagerStatistics CalculateStatistics(IWindowManagerService windowManager)
        {
            var allWindows = windowManager.GetAllWindows();
            var activeWindows = windowManager.GetActiveWindows();
            var overlapping = FindOverlappingWindows(windowManager);

            var totalArea = allWindows.Sum(w => w.Bounds.Width * w.Bounds.Height);
            var averageArea = allWindows.Count > 0 ? totalArea / allWindows.Count : 0;

            return new WindowManagerStatistics
            {
                TotalWindows = allWindows.Count,
                ActiveWindows = activeWindows.Count,
                MinimizedWindows = allWindows.Count - activeWindows.Count,
                OverlappingPairs = overlapping.Count,
                TotalWindowArea = totalArea,
                AverageWindowArea = averageArea,
                LargestWindow = allWindows.MaxBy(w => w.Bounds.Width * w.Bounds.Height),
                SmallestWindow = allWindows.MinBy(w => w.Bounds.Width * w.Bounds.Height)
            };
        }

        /// <summary>
        /// ウィンドウを指定された領域内に制限
        /// </summary>
        public static void ConstrainWindowsToBounds(IWindowManagerService windowManager, Rectangle bounds)
        {
            var windows = windowManager.GetActiveWindows();

            foreach (var window in windows)
            {
                if (window is ITransformable transformable)
                {
                    var windowBounds = window.Bounds;
                    var newBounds = windowBounds;

                    // 位置を境界内に調整
                    if (newBounds.Right > bounds.Right)
                        newBounds.X = bounds.Right - newBounds.Width;
                    if (newBounds.Bottom > bounds.Bottom)
                        newBounds.Y = bounds.Bottom - newBounds.Height;
                    if (newBounds.X < bounds.X)
                        newBounds.X = bounds.X;
                    if (newBounds.Y < bounds.Y)
                        newBounds.Y = bounds.Y;

                    // サイズを境界内に調整
                    if (newBounds.Width > bounds.Width)
                        newBounds.Width = bounds.Width;
                    if (newBounds.Height > bounds.Height)
                        newBounds.Height = bounds.Height;

                    if (newBounds != windowBounds)
                    {
                        transformable.SetPosition(newBounds.Location);
                        transformable.SetSize(newBounds.Size);
                    }
                }
            }
        }

        /// <summary>
        /// ウィンドウの重なりを解決
        /// </summary>
        public static void ResolveOverlaps(IWindowManagerService windowManager, Rectangle availableArea)
        {
            var overlapping = FindOverlappingWindows(windowManager);
            var processedWindows = new HashSet<IGameWindow>();

            foreach (var (window1, window2) in overlapping)
            {
                if (processedWindows.Contains(window1) || processedWindows.Contains(window2))
                    continue;

                // 簡単な解決策：一方を右にずらす
                if (window2 is ITransformable transformable)
                {
                    var newX = window1.Bounds.Right + 10;
                    if (newX + window2.Bounds.Width <= availableArea.Right)
                    {
                        transformable.SetPosition(new Point(newX, window2.Bounds.Y));
                        processedWindows.Add(window2);
                    }
                }
            }
        }

        /// <summary>
        /// ウィンドウの配置パターンを分析
        /// </summary>
        public static WindowLayoutAnalysis AnalyzeLayout(IWindowManagerService windowManager)
        {
            var windows = windowManager.GetActiveWindows();
            var analysis = new WindowLayoutAnalysis();

            if (!windows.Any())
                return analysis;

            // 配置パターンの分析
            var leftAligned = windows.Count(w => w.Bounds.X < 100);
            var rightAligned = windows.Count(w => w.Bounds.Right > 1400);
            var topAligned = windows.Count(w => w.Bounds.Y < 100);
            var bottomAligned = windows.Count(w => w.Bounds.Bottom > 700);

            analysis.LeftEdgeAlignment = (double)leftAligned / windows.Count;
            analysis.RightEdgeAlignment = (double)rightAligned / windows.Count;
            analysis.TopEdgeAlignment = (double)topAligned / windows.Count;
            analysis.BottomEdgeAlignment = (double)bottomAligned / windows.Count;

            // 密度分析
            var boundingBox = windows.Aggregate(windows.First().Bounds, 
                (current, window) => Rectangle.Union(current, window.Bounds));
            var totalWindowArea = windows.Sum(w => w.Bounds.Width * w.Bounds.Height);
            analysis.Density = (double)totalWindowArea / (boundingBox.Width * boundingBox.Height);

            return analysis;
        }
    }

    /// <summary>
    /// ウィンドウマネージャー統計情報
    /// </summary>
    public class WindowManagerStatistics
    {
        public int TotalWindows { get; set; }
        public int ActiveWindows { get; set; }
        public int MinimizedWindows { get; set; }
        public int OverlappingPairs { get; set; }
        public int TotalWindowArea { get; set; }
        public double AverageWindowArea { get; set; }
        public IGameWindow? LargestWindow { get; set; }
        public IGameWindow? SmallestWindow { get; set; }

        public string GenerateReport()
        {
            return $@"Window Manager Statistics:
Total Windows: {TotalWindows}
Active Windows: {ActiveWindows}
Minimized Windows: {MinimizedWindows}
Overlapping Pairs: {OverlappingPairs}
Total Window Area: {TotalWindowArea} pixels
Average Window Area: {AverageWindowArea:F1} pixels
Largest Window: {LargestWindow?.GetType().Name ?? "None"}
Smallest Window: {SmallestWindow?.GetType().Name ?? "None"}";
        }
    }

    /// <summary>
    /// ウィンドウレイアウト分析結果
    /// </summary>
    public class WindowLayoutAnalysis
    {
        public double LeftEdgeAlignment { get; set; }
        public double RightEdgeAlignment { get; set; }
        public double TopEdgeAlignment { get; set; }
        public double BottomEdgeAlignment { get; set; }
        public double Density { get; set; }

        public string GetLayoutDescription()
        {
            if (LeftEdgeAlignment > 0.7) return "Left-aligned layout";
            if (RightEdgeAlignment > 0.7) return "Right-aligned layout";
            if (TopEdgeAlignment > 0.7) return "Top-aligned layout";
            if (BottomEdgeAlignment > 0.7) return "Bottom-aligned layout";
            if (Density > 0.8) return "Dense layout";
            if (Density < 0.3) return "Sparse layout";
            return "Mixed layout";
        }
    }