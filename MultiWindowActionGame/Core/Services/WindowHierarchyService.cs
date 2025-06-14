// Core/Services/WindowHierarchyService.cs
using MultiWindowActionGame.Core.Services.Interfaces;
using MultiWindowActionGame.Core.Interfaces;
using System.Collections.Concurrent;

namespace MultiWindowActionGame.Core.Services
{
    /// <summary>
    /// ウィンドウ階層管理サービスの実装
    /// </summary>
    public class WindowHierarchyService : IWindowHierarchyService, IDisposable
    {
        private readonly IWindowCollisionService _collisionService;
        private readonly IEventService _eventService;
        private readonly ConcurrentDictionary<IGameWindow, IGameWindow?> _parentMap = new();
        private readonly ConcurrentDictionary<IGameWindow, List<IGameWindow>> _childrenMap = new();
        private readonly object _lock = new();
        private bool _disposed = false;

        public event EventHandler<HierarchyChangedEventArgs<IGameWindow>>? HierarchyChanged;

        public WindowHierarchyService(IWindowCollisionService collisionService, IEventService eventService)
        {
            _collisionService = collisionService ?? throw new ArgumentNullException(nameof(collisionService));
            _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
        }

        public void UpdateHierarchy(IGameWindow window)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(window);

            lock (_lock)
            {
                // 現在の親を取得
                var currentParent = _parentMap.GetValueOrDefault(window);
                
                // 新しい親を探す
                var allWindows = GetAllRegisteredWindows();
                var newParent = FindParentWindow(window, allWindows);

                // 親が変更された場合のみ更新
                if (currentParent != newParent)
                {
                    // 古い親から削除
                    if (currentParent != null)
                    {
                        RemoveChildFromParent(window, currentParent);
                    }

                    // 新しい親に追加
                    if (newParent != null)
                    {
                        AddChildToParent(window, newParent);
                    }

                    // マップを更新
                    if (newParent != null)
                    {
                        _parentMap[window] = newParent;
                    }
                    else
                    {
                        _parentMap.TryRemove(window, out _);
                    }

                    // イベントを発行
                    OnHierarchyChanged(window, currentParent, newParent, HierarchyChangeType.ParentChanged);

                    System.Diagnostics.Debug.WriteLine($"Window hierarchy updated: {window} parent changed from {currentParent} to {newParent}");
                }

                // 子ウィンドウとの関係をチェック
                UpdateChildRelationships(window, allWindows);
            }
        }

        public void RemoveFromHierarchy(IGameWindow window)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(window);

            lock (_lock)
            {
                // 親から削除
                if (_parentMap.TryGetValue(window, out var parent) && parent != null)
                {
                    RemoveChildFromParent(window, parent);
                    OnHierarchyChanged(window, parent, null, HierarchyChangeType.ParentChanged);
                }

                // 子をすべて削除
                if (_childrenMap.TryGetValue(window, out var children))
                {
                    foreach (var child in children.ToList())
                    {
                        RemoveChildFromParent(child, window);
                        _parentMap.TryRemove(child, out _);
                        OnHierarchyChanged(child, window, null, HierarchyChangeType.ParentChanged);
                    }
                }

                // マップから削除
                _parentMap.TryRemove(window, out _);
                _childrenMap.TryRemove(window, out _);
            }

            System.Diagnostics.Debug.WriteLine($"Removed window from hierarchy: {window}");
        }

        public IGameWindow? FindParentWindow(IGameWindow window, IReadOnlyList<IGameWindow> candidates)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(window);
            ArgumentNullException.ThrowIfNull(candidates);

            // 現在のウィンドウの既存の階層グループを取得
            var existingGroup = GetWindowGroup(window);

            // 親候補を探す（Z-orderが低いものから優先）
            var potentialParents = candidates
                .Where(w => !existingGroup.Contains(w) && w != window)
                .Where(w => CanBeParent(w, window))
                .OrderBy(w => GetWindowZOrder(w));

            // 完全に含んでいる最も手前のウィンドウを探す
            IGameWindow? bestParent = null;
            int bestParentZOrder = -1;

            foreach (var candidate in potentialParents)
            {
                if (_collisionService.IsFullyContained(window.Bounds, candidate.Bounds))
                {
                    int candidateZOrder = GetWindowZOrder(candidate);
                    if (bestParentZOrder < candidateZOrder)
                    {
                        bestParent = candidate;
                        bestParentZOrder = candidateZOrder;
                    }
                }
            }

            return bestParent;
        }

        public IReadOnlyList<IGameWindow> GetChildren(IGameWindow parent)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(parent);

            lock (_lock)
            {
                return _childrenMap.GetValueOrDefault(parent, new List<IGameWindow>()).ToList();
            }
        }

        public IReadOnlyList<IGameWindow> GetDescendants(IGameWindow ancestor)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(ancestor);

            var descendants = new List<IGameWindow>();
            var directChildren = GetChildren(ancestor);

            foreach (var child in directChildren)
            {
                descendants.Add(child);
                descendants.AddRange(GetDescendants(child));
            }

            return descendants;
        }

        public IReadOnlyList<IGameWindow> GetRootWindows(IReadOnlyList<IGameWindow> allWindows)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(allWindows);

            lock (_lock)
            {
                return allWindows
                    .Where(w => !_parentMap.ContainsKey(w) || _parentMap[w] == null)
                    .ToList();
            }
        }

        private void UpdateChildRelationships(IGameWindow window, IReadOnlyList<IGameWindow> allWindows)
        {
            var windowGroup = GetWindowGroup(window);
            
            var potentialChildren = allWindows
                .Where(w => !windowGroup.Contains(w) && w != window)
                .Where(w => GetWindowZOrder(w) > GetWindowZOrder(window))
                .OrderBy(w => GetWindowZOrder(w));

            foreach (var potentialChild in potentialChildren)
            {
                if (_collisionService.IsFullyContained(potentialChild.Bounds, window.Bounds))
                {
                    // より適切な親となれる場合は関係を変更
                    var currentParent = _parentMap.GetValueOrDefault(potentialChild);
                    if (currentParent == null || GetWindowZOrder(window) > GetWindowZOrder(currentParent))
                    {
                        if (currentParent != null)
                        {
                            RemoveChildFromParent(potentialChild, currentParent);
                        }
                        
                        AddChildToParent(potentialChild, window);
                        _parentMap[potentialChild] = window;
                        
                        OnHierarchyChanged(potentialChild, currentParent, window, HierarchyChangeType.ParentChanged);
                    }
                }
            }
        }

        private void AddChildToParent(IGameWindow child, IGameWindow parent)
        {
            if (!_childrenMap.ContainsKey(parent))
            {
                _childrenMap[parent] = new List<IGameWindow>();
            }
            
            if (!_childrenMap[parent].Contains(child))
            {
                _childrenMap[parent].Add(child);
                OnHierarchyChanged(child, null, parent, HierarchyChangeType.ChildAdded);
            }
        }

        private void RemoveChildFromParent(IGameWindow child, IGameWindow parent)
        {
            if (_childrenMap.TryGetValue(parent, out var children))
            {
                if (children.Remove(child))
                {
                    OnHierarchyChanged(child, parent, null, HierarchyChangeType.ChildRemoved);
                }
            }
        }

        private HashSet<IGameWindow> GetWindowGroup(IGameWindow window)
        {
            var group = new HashSet<IGameWindow> { window };
            
            // 祖先を追加
            var current = window;
            while (_parentMap.TryGetValue(current, out var parent) && parent != null)
            {
                group.Add(parent);
                current = parent;
            }
            
            // 子孫を追加
            group.UnionWith(GetDescendants(window));
            
            return group;
        }

        private bool CanBeParent(IGameWindow potentialParent, IGameWindow child)
        {
            // 循環参照をチェック
            var current = potentialParent;
            while (_parentMap.TryGetValue(current, out var parent) && parent != null)
            {
                if (parent == child)
                    return false;
                current = parent;
            }
            
            return true;
        }

        private int GetWindowZOrder(IGameWindow window)
        {
            if (window is IZOrderable zOrderable)
            {
                return (int)zOrderable.Priority * 1000 + zOrderable.ZOrder;
            }
            return 0;
        }

        private IReadOnlyList<IGameWindow> GetAllRegisteredWindows()
        {
            // すべての登録されたウィンドウを取得
            var allWindows = new HashSet<IGameWindow>();
            
            foreach (var parent in _parentMap.Keys)
            {
                allWindows.Add(parent);
            }
            
            foreach (var parent in _parentMap.Values)
            {
                if (parent != null)
                {
                    allWindows.Add(parent);
                }
            }
            
            foreach (var parent in _childrenMap.Keys)
            {
                allWindows.Add(parent);
            }
            
            foreach (var children in _childrenMap.Values)
            {
                foreach (var child in children)
                {
                    allWindows.Add(child);
                }
            }
            
            return allWindows.ToList();
        }

        private void OnHierarchyChanged(IGameWindow child, IGameWindow? oldParent, IGameWindow? newParent, HierarchyChangeType changeType)
        {
            var eventArgs = new HierarchyChangedEventArgs<IGameWindow>(child, oldParent, newParent, changeType);
            HierarchyChanged?.Invoke(this, eventArgs);

            // イベントサービス経由でも通知
            _eventService.Publish(new WindowHierarchyChangedEvent
            {
                Child = child,
                OldParent = oldParent,
                NewParent = newParent,
                ChangeType = changeType,
                Timestamp = DateTime.Now
            });
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WindowHierarchyService));
        }

        public void Dispose()
        {
            if (_disposed) return;

            lock (_lock)
            {
                _parentMap.Clear();
                _childrenMap.Clear();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// ウィンドウ階層変更イベント
    /// </summary>
    public class WindowHierarchyChangedEvent
    {
        public IGameWindow Child { get; set; } = null!;
        public IGameWindow? OldParent { get; set; }
        public IGameWindow? NewParent { get; set; }
        public HierarchyChangeType ChangeType { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 階層関連のユーティリティメソッド
    /// </summary>
    public static class HierarchyHelper
    {
        /// <summary>
        /// ウィンドウの階層の深さを計算
        /// </summary>
        public static int GetDepth(IGameWindow window, IWindowHierarchyService hierarchyService)
        {
            int depth = 0;
            var allWindows = hierarchyService.GetRootWindows(new List<IGameWindow> { window });
            
            // 簡易実装：実際の親を辿る場合は追加のメソッドが必要
            return depth;
        }

        /// <summary>
        /// 階層ツリーの文字列表現を生成
        /// </summary>
        public static string GenerateHierarchyTree(IGameWindow root, IWindowHierarchyService hierarchyService, int indent = 0)
        {
            var result = new string(' ', indent * 2) + $"- {GetWindowDisplayName(root)}\n";
            
            var children = hierarchyService.GetChildren(root);
            foreach (var child in children)
            {
                result += GenerateHierarchyTree(child, hierarchyService, indent + 1);
            }
            
            return result;
        }

        /// <summary>
        /// ウィンドウの表示名を取得
        /// </summary>
        public static string GetWindowDisplayName(IGameWindow window)
        {
            if (window is IIdentifiable identifiable)
            {
                return $"{identifiable.Name} ({identifiable.Id})";
            }
            
            return window.GetType().Name;
        }

        /// <summary>
        /// 指定されたウィンドウの祖先をすべて取得
        /// </summary>
        public static IReadOnlyList<IGameWindow> GetAncestors(IGameWindow window, IWindowHierarchyService hierarchyService)
        {
            var ancestors = new List<IGameWindow>();
            var allWindows = hierarchyService.GetRootWindows(new List<IGameWindow> { window });
            
            // 簡易実装：実際の親を辿る場合は追加のロジックが必要
            return ancestors;
        }

        /// <summary>
        /// 2つのウィンドウの共通祖先を検索
        /// </summary>
        public static IGameWindow? FindCommonAncestor(IGameWindow window1, IGameWindow window2, IWindowHierarchyService hierarchyService)
        {
            var ancestors1 = GetAncestors(window1, hierarchyService).ToHashSet();
            var ancestors2 = GetAncestors(window2, hierarchyService);
            
            return ancestors2.FirstOrDefault(ancestors1.Contains);
        }

        /// <summary>
        /// 階層の整合性をチェック
        /// </summary>
        public static HierarchyValidationResult ValidateHierarchy(IReadOnlyList<IGameWindow> allWindows, IWindowHierarchyService hierarchyService)
        {
            var result = new HierarchyValidationResult();
            var visited = new HashSet<IGameWindow>();
            
            foreach (var window in allWindows)
            {
                if (!visited.Contains(window))
                {
                    var cycleCheck = DetectCycle(window, hierarchyService, new HashSet<IGameWindow>());
                    if (cycleCheck.HasCycle)
                    {
                        result.HasCycles = true;
                        result.CyclicWindows.AddRange(cycleCheck.CycleNodes);
                    }
                    
                    visited.UnionWith(cycleCheck.VisitedNodes);
                }
            }
            
            return result;
        }

        /// <summary>
        /// 循環参照を検出
        /// </summary>
        private static CycleDetectionResult DetectCycle(IGameWindow startWindow, IWindowHierarchyService hierarchyService, HashSet<IGameWindow> visited)
        {
            var result = new CycleDetectionResult();
            var currentPath = new HashSet<IGameWindow>();
            var stack = new Stack<IGameWindow>();
            
            stack.Push(startWindow);
            
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                
                if (currentPath.Contains(current))
                {
                    result.HasCycle = true;
                    result.CycleNodes.Add(current);
                    continue;
                }
                
                if (visited.Contains(current))
                    continue;
                
                visited.Add(current);
                currentPath.Add(current);
                result.VisitedNodes.Add(current);
                
                var children = hierarchyService.GetChildren(current);
                foreach (var child in children)
                {
                    stack.Push(child);
                }
            }
            
            return result;
        }

        /// <summary>
        /// 階層統計情報を計算
        /// </summary>
        public static HierarchyStatistics CalculateStatistics(IReadOnlyList<IGameWindow> allWindows, IWindowHierarchyService hierarchyService)
        {
            var statistics = new HierarchyStatistics
            {
                TotalWindows = allWindows.Count
            };
            
            var rootWindows = hierarchyService.GetRootWindows(allWindows);
            statistics.RootWindows = rootWindows.Count;
            
            foreach (var root in rootWindows)
            {
                var descendants = hierarchyService.GetDescendants(root);
                var depth = CalculateMaxDepth(root, hierarchyService);
                
                statistics.MaxDepth = Math.Max(statistics.MaxDepth, depth);
                statistics.TotalRelationships += descendants.Count;
            }
            
            statistics.AverageChildrenPerWindow = statistics.TotalWindows > 0 ? 
                (double)statistics.TotalRelationships / statistics.TotalWindows : 0;
            
            return statistics;
        }

        private static int CalculateMaxDepth(IGameWindow window, IWindowHierarchyService hierarchyService)
        {
            var children = hierarchyService.GetChildren(window);
            if (!children.Any())
                return 1;
            
            return 1 + children.Max(child => CalculateMaxDepth(child, hierarchyService));
        }
    }

    /// <summary>
    /// 階層検証結果
    /// </summary>
    public class HierarchyValidationResult
    {
        public bool HasCycles { get; set; }
        public List<IGameWindow> CyclicWindows { get; set; } = new();
        public bool IsValid => !HasCycles;
        
        public string GetErrorMessage()
        {
            if (!HasCycles)
                return "Hierarchy is valid";
            
            return $"Hierarchy has cycles involving {CyclicWindows.Count} windows";
        }
    }

    /// <summary>
    /// 循環検出結果
    /// </summary>
    private class CycleDetectionResult
    {
        public bool HasCycle { get; set; }
        public List<IGameWindow> CycleNodes { get; set; } = new();
        public HashSet<IGameWindow> VisitedNodes { get; set; } = new();
    }

    /// <summary>
    /// 階層統計情報
    /// </summary>
    public class HierarchyStatistics
    {
        public int TotalWindows { get; set; }
        public int RootWindows { get; set; }
        public int TotalRelationships { get; set; }
        public int MaxDepth { get; set; }
        public double AverageChildrenPerWindow { get; set; }

        public string GenerateReport()
        {
            return $@"Hierarchy Statistics:
Total Windows: {TotalWindows}
Root Windows: {RootWindows}
Total Parent-Child Relationships: {TotalRelationships}
Maximum Depth: {MaxDepth}
Average Children per Window: {AverageChildrenPerWindow:F2}";
        }
    }
        