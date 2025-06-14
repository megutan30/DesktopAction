// Core/Services/ZOrderService.cs
using MultiWindowActionGame.Core.Services.Interfaces;
using MultiWindowActionGame.Core.Interfaces;
using MultiWindowActionGame.Core.Constants;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace MultiWindowActionGame.Core.Services
{
    /// <summary>
    /// Z-Order管理サービスの実装
    /// </summary>
    public class ZOrderService : IZOrderService, IDisposable
    {
        private readonly ConcurrentDictionary<IntPtr, IZOrderable> _registeredWindows = new();
        private readonly ConcurrentDictionary<ZOrderPriority, List<IZOrderable>> _windowsByPriority = new();
        private readonly IEventService _eventService;
        private readonly object _lock = new();
        private bool _disposed = false;

        public event EventHandler<ZOrderChangedEventArgs>? ZOrderChanged;

        public ZOrderService(IEventService eventService)
        {
            _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
            InitializePriorityGroups();
        }

        private void InitializePriorityGroups()
        {
            foreach (ZOrderPriority priority in Enum.GetValues<ZOrderPriority>())
            {
                _windowsByPriority[priority] = new List<IZOrderable>();
            }
        }

        public void RegisterWindow(IZOrderable window)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(window);

            lock (_lock)
            {
                // フォームハンドルの取得（Formの場合）
                IntPtr handle = IntPtr.Zero;
                if (window is Form form && !form.IsDisposed)
                {
                    handle = form.Handle;
                }

                if (handle != IntPtr.Zero)
                {
                    _registeredWindows[handle] = window;
                }

                var priority = window.Priority;
                if (!_windowsByPriority[priority].Contains(window))
                {
                    _windowsByPriority[priority].Add(window);
                    UpdateWindowZOrder(window);
                }
            }

            System.Diagnostics.Debug.WriteLine($"Registered window with priority: {window.Priority}");
        }

        public void UnregisterWindow(IZOrderable window)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(window);

            lock (_lock)
            {
                // ハンドルベースの登録解除
                var handleToRemove = _registeredWindows
                    .Where(kvp => ReferenceEquals(kvp.Value, window))
                    .Select(kvp => kvp.Key)
                    .FirstOrDefault();

                if (handleToRemove != IntPtr.Zero)
                {
                    _registeredWindows.TryRemove(handleToRemove, out _);
                }

                // 優先度ベースの登録解除
                var priority = window.Priority;
                if (_windowsByPriority.TryGetValue(priority, out var windows))
                {
                    windows.Remove(window);
                }
            }

            System.Diagnostics.Debug.WriteLine($"Unregistered window with priority: {window.Priority}");
        }

        public void BringToFront(IZOrderable window)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(window);

            lock (_lock)
            {
                var priority = window.Priority;
                if (_windowsByPriority.TryGetValue(priority, out var windows))
                {
                    // 同じ優先度内で最前面に移動
                    windows.Remove(window);
                    windows.Add(window);
                    
                    UpdateWindowZOrder(window);
                    OnZOrderChanged(window, window.ZOrder, window.ZOrder + 1);
                }
            }
        }

        public void SendToBack(IZOrderable window)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(window);

            lock (_lock)
            {
                var priority = window.Priority;
                if (_windowsByPriority.TryGetValue(priority, out var windows))
                {
                    // 同じ優先度内で最背面に移動
                    windows.Remove(window);
                    windows.Insert(0, window);
                    
                    UpdateWindowZOrder(window);
                    OnZOrderChanged(window, window.ZOrder, window.ZOrder - 1);
                }
            }
        }

        public void UpdateOrders(IReadOnlyList<IZOrderable> windows)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(windows);

            lock (_lock)
            {
                // 優先度の高い順に処理
                foreach (var priorityGroup in _windowsByPriority.OrderByDescending(kvp => (int)kvp.Key))
                {
                    for (int i = priorityGroup.Value.Count - 1; i >= 0; i--)
                    {
                        var window = priorityGroup.Value[i];
                        UpdateWindowZOrder(window);
                    }
                }
            }
        }

        public int GetZOrder(IZOrderable window)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(window);

            lock (_lock)
            {
                var priority = window.Priority;
                if (_windowsByPriority.TryGetValue(priority, out var windows))
                {
                    var index = windows.IndexOf(window);
                    if (index >= 0)
                    {
                        // 優先度ベース値 + 同一優先度内での順序
                        return (int)priority * 1000 + index;
                    }
                }
                return 0;
            }
        }

        public IReadOnlyList<IZOrderable> GetWindowsByPriority(ZOrderPriority priority)
        {
            ThrowIfDisposed();

            lock (_lock)
            {
                if (_windowsByPriority.TryGetValue(priority, out var windows))
                {
                    return windows.ToList();
                }
                return new List<IZOrderable>();
            }
        }

        private void UpdateWindowZOrder(IZOrderable window)
        {
            if (window is not Form form || form.IsDisposed || form.Handle == IntPtr.Zero)
                return;

            try
            {
                IntPtr insertAfter = GetInsertAfterHandle(window.Priority);
                bool success = SetWindowPos(
                    form.Handle,
                    insertAfter,
                    0, 0, 0, 0,
                    GameConstants.Win32.SWP_NOMOVE | GameConstants.Win32.SWP_NOSIZE | GameConstants.Win32.SWP_NOACTIVATE
                );

                if (!success)
                {
                    var error = Marshal.GetLastWin32Error();
                    System.Diagnostics.Debug.WriteLine($"SetWindowPos failed for window {form.Name}: Error {error}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating Z-order for window: {ex.Message}");
            }
        }

        private IntPtr GetInsertAfterHandle(ZOrderPriority priority)
        {
            return priority switch
            {
                ZOrderPriority.DebugLayer => new IntPtr(GameConstants.Win32.HWND_TOPMOST),
                ZOrderPriority.Bottom => new IntPtr(GameConstants.Win32.HWND_BOTTOM),
                _ => new IntPtr(GameConstants.Win32.HWND_TOP)
            };
        }

        private void OnZOrderChanged(IZOrderable window, int oldZOrder, int newZOrder)
        {
            var eventArgs = new ZOrderChangedEventArgs(oldZOrder, newZOrder, window.Priority);
            ZOrderChanged?.Invoke(this, eventArgs);

            // イベントサービス経由でも通知
            _eventService.Publish(new ZOrderChangedEvent
            {
                Window = window,
                OldZOrder = oldZOrder,
                NewZOrder = newZOrder,
                Priority = window.Priority
            });
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZOrderService));
        }

        public void Dispose()
        {
            if (_disposed) return;

            lock (_lock)
            {
                _registeredWindows.Clear();
                foreach (var windows in _windowsByPriority.Values)
                {
                    windows.Clear();
                }
                _windowsByPriority.Clear();
            }

            _disposed = true;
        }

        // Win32 API imports
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);
    }

    /// <summary>
    /// Z-Order変更イベント
    /// </summary>
    public class ZOrderChangedEvent
    {
        public IZOrderable Window { get; set; } = null!;
        public int OldZOrder { get; set; }
        public int NewZOrder { get; set; }
        public ZOrderPriority Priority { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Z-Order関連のユーティリティメソッド
    /// </summary>
    public static class ZOrderHelper
    {
        /// <summary>
        /// 優先度に基づいてソートされたウィンドウリストを作成
        /// </summary>
        public static IReadOnlyList<T> SortByZOrder<T>(IEnumerable<T> windows) where T : IZOrderable
        {
            return windows
                .OrderBy(w => (int)w.Priority)
                .ThenBy(w => w.ZOrder)
                .ToList();
        }

        /// <summary>
        /// 2つのウィンドウのZ-Order関係を比較
        /// </summary>
        public static int CompareZOrder(IZOrderable window1, IZOrderable window2)
        {
            // 優先度を先に比較
            var priorityComparison = ((int)window1.Priority).CompareTo((int)window2.Priority);
            if (priorityComparison != 0)
                return priorityComparison;

            // 同じ優先度の場合はZ-Orderを比較
            return window1.ZOrder.CompareTo(window2.ZOrder);
        }

        /// <summary>
        /// ウィンドウが他のウィンドウより前面にあるかチェック
        /// </summary>
        public static bool IsInFrontOf(IZOrderable window1, IZOrderable window2)
        {
            return CompareZOrder(window1, window2) > 0;
        }

        /// <summary>
        /// 優先度の文字列表現を取得
        /// </summary>
        public static string GetPriorityDescription(ZOrderPriority priority)
        {
            return priority switch
            {
                ZOrderPriority.Bottom => "Background Layer",
                ZOrderPriority.Window => "Window Layer",
                ZOrderPriority.WindowMark => "Window Mark Layer",
                ZOrderPriority.Button => "Button Layer",
                ZOrderPriority.Goal => "Goal Layer",
                ZOrderPriority.Player => "Player Layer",
                ZOrderPriority.DebugLayer => "Debug Overlay Layer",
                _ => "Unknown Layer"
            };
        }

        /// <summary>
        /// Z-Order統計情報を取得
        /// </summary>
        public static ZOrderStatistics GetStatistics(IZOrderService zOrderService)
        {
            var statistics = new ZOrderStatistics();
            
            foreach (ZOrderPriority priority in Enum.GetValues<ZOrderPriority>())
            {
                var windows = zOrderService.GetWindowsByPriority(priority);
                statistics.WindowCounts[priority] = windows.Count;
                statistics.TotalWindows += windows.Count;
            }

            return statistics;
        }
    }

    /// <summary>
    /// Z-Order統計情報
    /// </summary>
    public class ZOrderStatistics
    {
        public Dictionary<ZOrderPriority, int> WindowCounts { get; } = new();
        public int TotalWindows { get; set; }
        public ZOrderPriority? MostPopulatedPriority => 
            WindowCounts.Count > 0 ? WindowCounts.OrderByDescending(kvp => kvp.Value).First().Key : null;
        public int MaxWindowsInPriority => 
            WindowCounts.Count > 0 ? WindowCounts.Values.Max() : 0;

        public string GenerateReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("Z-Order Statistics:");
            report.AppendLine($"Total Windows: {TotalWindows}");
            report.AppendLine();

            foreach (var kvp in WindowCounts.OrderByDescending(x => (int)x.Key))
            {
                var description = ZOrderHelper.GetPriorityDescription(kvp.Key);
                report.AppendLine($"{description}: {kvp.Value} windows");
            }

            if (MostPopulatedPriority.HasValue)
            {
                var mostPopulated = ZOrderHelper.GetPriorityDescription(MostPopulatedPriority.Value);
                report.AppendLine();
                report.AppendLine($"Most populated layer: {mostPopulated} ({MaxWindowsInPriority} windows)");
            }

            return report.ToString();
        }
    }
}