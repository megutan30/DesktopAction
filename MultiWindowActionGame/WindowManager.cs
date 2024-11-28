﻿using MultiWindowActionGame;
using System.Numerics;
using static MultiWindowActionGame.GameWindow;

public class WindowManager : IWindowObserver
{
    private static readonly Lazy<WindowManager> lazy =
         new Lazy<WindowManager>(() => new WindowManager());

    public static WindowManager Instance { get { return lazy.Value; } }

    private List<GameWindow> windows = new List<GameWindow>();
    private object windowLock = new object();
    private Player? player;
    private bool isInitialized = false;

    private readonly Dictionary<GameWindow, HashSet<IEffectTarget>> containedTargetsCache = new();
    private Dictionary<IEffectTarget, GameWindow> parentChildRelations = new Dictionary<IEffectTarget, GameWindow>();
    private bool isCheckingParentChild = false;
    private bool needsUpdateCache = true;
    private Dictionary<GameWindow, GameWindow> childToParentRelations = new Dictionary<GameWindow, GameWindow>();
    private bool isCheckingRelations = false;
    private WindowManager()
    {
        
    }

    public void Initialize()
    {
        if (isInitialized) return;

        CreateInitialWindows();
        isInitialized = true;
    }
    public void InvalidateCache()
    {
        needsUpdateCache = true;
    }
    public void SetPlayer(Player player)
    {
        this.player = player;
    }
    // 親を取得するメソッド
    public GameWindow? GetParentWindow(IEffectTarget child)
    {
        return parentChildRelations.TryGetValue(child, out var parent) ? parent : null;
    }
    public void CheckPotentialParentWindow(GameWindow operatedWindow)
    {
        lock (windowLock)
        {
            // まず現在の親子関係を解除
            if (operatedWindow.Parent != null)
            {
                operatedWindow.Parent.RemoveChild(operatedWindow);
            }

            // 操作されたウィンドウより奥にあるウィンドウをチェック（親になれるか）
            var potentialParents = windows
                .Where(w => w != operatedWindow && windows.IndexOf(w) < windows.IndexOf(operatedWindow))
                .OrderByDescending(w => windows.IndexOf(w));

            foreach (var potentialParent in potentialParents)
            {
                if (potentialParent.AdjustedBounds.Contains(operatedWindow.AdjustedBounds))
                {
                    potentialParent.AddChild(operatedWindow);
                    Console.WriteLine($"Window {operatedWindow.Id} became child of {potentialParent.Id}");
                    return;
                }
            }

            // 操作されたウィンドウが親になれるかチェック
            var potentialChildren = windows
                .Where(w => w != operatedWindow && windows.IndexOf(w) > windows.IndexOf(operatedWindow))
                .OrderBy(w => windows.IndexOf(w));

            foreach (var potentialChild in potentialChildren)
            {
                if (operatedWindow.AdjustedBounds.Contains(potentialChild.AdjustedBounds))
                {
                    operatedWindow.AddChild(potentialChild);
                    Console.WriteLine($"Window {operatedWindow.Id} became parent of {potentialChild.Id}");
                }
            }
        }
    }

    public void CheckChildRelationBreak(IEffectTarget child)
    {
        if (!parentChildRelations.ContainsKey(child)) return;

        var parent = parentChildRelations[child];
        if (!parent.AdjustedBounds.Contains(child.Bounds))
        {
            parentChildRelations.Remove(child);
            Console.WriteLine($"Broke parent-child relation: Parent={parent.Id}, Child={child}");
        }
    }

    public HashSet<IEffectTarget> GetContainedTargets(GameWindow window)
    {
        return new HashSet<IEffectTarget>(
            parentChildRelations
                .Where(kv => kv.Value == window)
                .Select(kv => kv.Key)
        );
    }
    public void CreateInitialWindows()
    {
        lock (windowLock)
        {
            if (windows.Count == 0)
            {
                var newWindows = new List<GameWindow>
                {
                    WindowFactory.CreateWindow(WindowType.Normal, new Point(100, 100), new Size(300, 200)),
                    WindowFactory.CreateWindow(WindowType.Resizable, new Point(450, 100), new Size(300, 200)),
                    WindowFactory.CreateWindow(WindowType.Movable, new Point(100, 350), new Size(300, 200)),
                    WindowFactory.CreateWindow(WindowType.Deletable, new Point(450, 350), new Size(300, 200))
                };

                foreach (var window in newWindows)
                {
                    window.AddObserver(this);
                    windows.Add(window);
                }
            }
        }
    }


    public IEnumerable<IEffectTarget> GetAllComponents()
    {
        lock (windowLock)
        {
            var components = new List<IEffectTarget>(windows);
            if (player != null)
            {
                components.Add(player);
            }
            return components;
        }
    }

    public List<GameWindow> GetIntersectingWindows(Rectangle bounds)
    {
        lock (windowLock)
        {
            return windows
                .Where(w => w.AdjustedBounds.IntersectsWith(bounds))
                .OrderByDescending(w => windows.IndexOf(w))
                .ToList();
        }
    }

    public async Task UpdateAsync(float deltaTime)
    {
        List<GameWindow> windowsCopy;
        lock (windowLock)
        {
            windowsCopy = new List<GameWindow>(windows);
        }

        foreach (var window in windowsCopy)
        {
            await window.UpdateAsync(deltaTime);
        }
    }

    public void Draw(Graphics g)
    {
        lock (windowLock)
        {
            foreach (var window in windows)
            {
                window.Draw(g);
            }

            if (MainGame.IsDebugMode)
            {
                DrawDebugInfo(g);
            }
        }
    }
    public void DrawDebugInfo(Graphics g)
    {
        foreach (var window in windows)
        {
            // ウィンドウの基本情報
            g.DrawRectangle(new Pen(Color.Blue, 1), window.AdjustedBounds);

            // Z-order情報
            g.DrawString($"Z: {windows.IndexOf(window)}",
                SystemFonts.DefaultFont, Brushes.White,
                window.Location.X + 5, window.Location.Y + 5);

            // 親子関係の表示
            if (window.Parent != null)
            {
                using (var pen = new Pen(Color.Yellow, 2))
                {
                    Point childCenter = new Point(
                        window.Bounds.X + window.Bounds.Width / 2,
                        window.Bounds.Y + window.Bounds.Height / 2
                    );
                    Point parentCenter = new Point(
                        window.Parent.Bounds.X + window.Parent.Bounds.Width / 2,
                        window.Parent.Bounds.Y + window.Parent.Bounds.Height / 2
                    );
                    g.DrawLine(pen, childCenter, parentCenter);
                }
            }

            // 子の数を表示
            if (window.Children.Any())
            {
                g.DrawString($"Children: {window.Children.Count}",
                    SystemFonts.DefaultFont, Brushes.Yellow,
                    window.Location.X + 5, window.Location.Y + 25);
            }
        }
    }
    public GameWindow? GetWindowAt(Rectangle bounds, GameWindow? currentWindow = null)
    {
        lock (windowLock)
        {
            GameWindow? bestMatch = null;
            int bestOverlap = 0;

            foreach (var window in windows)
            {
                if (window == currentWindow) continue; // 現在のウィンドウをスキップ

                Rectangle intersection = Rectangle.Intersect(window.AdjustedBounds, bounds);
                int overlap = intersection.Width * intersection.Height;

                if (overlap > 0 || IsAdjacentTo(window.AdjustedBounds, bounds))
                {
                    if (overlap > bestOverlap)
                    {
                        bestMatch = window;
                        bestOverlap = overlap;
                    }
                }
            }

            return bestMatch;
        }
    }
    private bool IsWindowInFrontOf(GameWindow window1, GameWindow window2)
    {
        lock (windowLock)
        {
            int index1 = windows.IndexOf(window1);
            int index2 = windows.IndexOf(window2);
            return index1 > index2;
        }
    }

    public GameWindow? GetTopWindowAt(Rectangle playerBounds, GameWindow? currentWindow)
    {
        Point[] checkPoints = new Point[]
        {
            new Point(playerBounds.Left, playerBounds.Bottom), // 左下
            new Point(playerBounds.Right, playerBounds.Bottom), // 右下
            new Point(playerBounds.Left, playerBounds.Top), // 左上
            new Point(playerBounds.Right, playerBounds.Top), // 右上
            new Point(playerBounds.X + playerBounds.Width / 2, playerBounds.Y + playerBounds.Height / 2) // 中心
        };

        Dictionary<GameWindow, HashSet<Point>> windowPoints = new Dictionary<GameWindow, HashSet<Point>>();

        lock (windowLock)
        {
            // 各チェックポイントについて、どのウィンドウに含まれているかをチェック
            foreach (var point in checkPoints)
            {
                for (int i = windows.Count - 1; i >= 0; i--)
                {
                    var window = windows[i];
                    if (window.AdjustedBounds.Contains(point))
                    {
                        if (!windowPoints.ContainsKey(window))
                        {
                            windowPoints[window] = new HashSet<Point>();
                        }
                        windowPoints[window].Add(point);
                        break;
                    }
                }
            }

            if (windowPoints.Count == 0)
            {
                return null;
            }

            // 現在のウィンドウの情報を取得
            var currentWindowPoints = currentWindow != null && windowPoints.ContainsKey(currentWindow)
                ? windowPoints[currentWindow]
                : new HashSet<Point>();

            if (currentWindow == null)
            {
                // 現在のウィンドウがない場合、すべての点を含むウィンドウを探す
                var fullWindow = windowPoints
                    .Where(w => w.Value.Count == checkPoints.Length)
                    .Select(w => new { Window = w.Key, BottomEdge = w.Key.AdjustedBounds.Bottom })
                    .OrderBy(w => w.BottomEdge)
                    .ThenByDescending(w => windows.IndexOf(w.Window))
                    .FirstOrDefault();

                return fullWindow?.Window ?? windowPoints.First().Key;
            }

            // 現在のウィンドウに点が残っている場合の処理
            if (currentWindowPoints.Count > 0)
            {
                // 下部の点を含むウィンドウを探し、下端の位置で優先順位付け
                var candidateWindows = windowPoints
                    .Where(w => w.Key != currentWindow &&
                               (w.Value.Contains(checkPoints[0]) || w.Value.Contains(checkPoints[1])))
                    .Select(w => new
                    {
                        Window = w.Key,
                        Points = w.Value,
                        BottomEdge = w.Key.AdjustedBounds.Bottom
                    })
                    .OrderBy(w => w.BottomEdge)
                    .ThenByDescending(w => windows.IndexOf(w.Window))
                    .ToList();

                if (candidateWindows.Any())
                {
                    var bestWindow = candidateWindows.First();
                    // 現在のウィンドウより下端が低い場合は移動しない
                    if (bestWindow.BottomEdge <= currentWindow.AdjustedBounds.Bottom)
                    {
                        return bestWindow.Window;
                    }
                }

                // 後面のウィンドウへの移動条件をチェック
                var backWindows = windowPoints
                    .Where(w => w.Key != currentWindow && w.Value.Count == checkPoints.Length)
                    .Select(w => w.Key)
                    .FirstOrDefault();

                if (backWindows != null)
                {
                    return backWindows;
                }

                return currentWindow;
            }
            else
            {
                // 下部の点を含むウィンドウの中から最適なものを選択
                var candidateWindows = windowPoints
                    .Where(w => w.Value.Contains(checkPoints[0]) || w.Value.Contains(checkPoints[1]))
                    .Select(w => new
                    {
                        Window = w.Key,
                        Points = w.Value,
                        BottomEdge = w.Key.AdjustedBounds.Bottom
                    })
                    .OrderBy(w => w.BottomEdge) // 下端が高いものを優先
                    .ThenByDescending(w => windows.IndexOf(w.Window)) // 同じ高さならZ-orderが高いものを優先
                    .ToList();

                if (candidateWindows.Any())
                {
                    return candidateWindows.First().Window;
                }

                // 下部の点を含むウィンドウがない場合は、すべての点を含むウィンドウを探す
                var fullWindow = windowPoints
                    .Where(w => w.Value.Count == checkPoints.Length)
                    .Select(w => new
                    {
                        Window = w.Key,
                        BottomEdge = w.Key.AdjustedBounds.Bottom
                    })
                    .OrderBy(w => w.BottomEdge)
                    .ThenByDescending(w => windows.IndexOf(w.Window))
                    .FirstOrDefault();

                if (fullWindow != null)
                {
                    return fullWindow.Window;
                }
            }

            return currentWindow;
        }
    }

    public void BringWindowToFront(GameWindow window)
    {

    }
    public void UpdateWindowOrders()
    {
        lock (windowLock)
        {
            // 親子関係に基づいてZ-orderを調整
            var orderedWindows = new List<GameWindow>();

            // 親を持たないウィンドウを先に追加
            foreach (var window in windows.Where(w => !parentChildRelations.ContainsKey(w)))
            {
                AddWindowAndChildren(window, orderedWindows);
            }

            // ウィンドウリストを更新
            windows = orderedWindows;
        }
    }

    private void AddWindowAndChildren(GameWindow window, List<GameWindow> orderedWindows)
    {
        orderedWindows.Add(window);

        // このウィンドウを親とする子ウィンドウを追加
        var children = parentChildRelations
            .Where(kv => kv.Value == window)
            .Select(kv => kv.Key)
            .OfType<GameWindow>();

        foreach (var child in children)
        {
            AddWindowAndChildren(child, orderedWindows);
        }
    }
    public Region CalculateMovableRegion(GameWindow currentWindow)
    {
        Region movableRegion = new Region(currentWindow.AdjustedBounds);

        lock (windowLock)
        {
            foreach (var window in windows)
            {
                if (window == currentWindow) continue;

                if (window.AdjustedBounds.IntersectsWith(currentWindow.AdjustedBounds) ||
                    IsAdjacentTo(window.AdjustedBounds, currentWindow.AdjustedBounds))
                {
                    movableRegion.Union(window.AdjustedBounds);
                }
            }
        }

        return movableRegion;
    }

    private bool IsAdjacentTo(Rectangle rect1, Rectangle rect2)
    {
        return (Math.Abs(rect1.Right - rect2.Left) <= 100 ||
                Math.Abs(rect1.Left - rect2.Right) <= 100 ||
                Math.Abs(rect1.Bottom - rect2.Top) <= 100 ||
                Math.Abs(rect1.Top - rect2.Bottom) <= 100) &&
               (rect1.Left <= rect2.Right && rect2.Left <= rect1.Right &&
                rect1.Top <= rect2.Bottom && rect2.Top <= rect1.Bottom);
    }

    public void DrawDebugInfo(Graphics g, Rectangle playerBounds)
    {
        lock (windowLock)
        {
            foreach (var window in windows)
            {
                // ウィンドウの枠を描画
                g.DrawRectangle(Pens.Blue, window.AdjustedBounds);

                // プレイヤーとの交差を確認
                Rectangle intersection = Rectangle.Intersect(window.AdjustedBounds, playerBounds);
                if (!intersection.IsEmpty)
                {
                    // 交差している場合、交差部分を赤で塗りつぶす
                    g.FillRectangle(new SolidBrush(Color.FromArgb(100, Color.Red)), intersection);
                }

                // 隣接しているかチェック
                if (IsAdjacentTo(window.AdjustedBounds, playerBounds))
                {
                    // 隣接している場合、ウィンドウの枠を緑で描画
                    g.DrawRectangle(new Pen(Color.Green, 2), window.AdjustedBounds);
                }

                foreach (var relation in parentChildRelations)
                {
                    var child = relation.Key;
                    var parent = relation.Value;

                    // 親子関係を線で表示
                    using (Pen pen = new Pen(Color.Yellow, 2))
                    {
                        Point childCenter = new Point(
                            child.Bounds.X + child.Bounds.Width / 2,
                            child.Bounds.Y + child.Bounds.Height / 2
                        );
                        Point parentCenter = new Point(
                            parent.Bounds.X + parent.Bounds.Width / 2,
                            parent.Bounds.Y + parent.Bounds.Height / 2
                        );
                        g.DrawLine(pen, childCenter, parentCenter);
                    }

                    // 関係情報を表示
                    string info = $"Parent: {parent.Id}";
                    g.DrawString(info, SystemFonts.DefaultFont, Brushes.Yellow,
                        child.Bounds.X, child.Bounds.Y - 20);
                }
            }
        }
    }

    public GameWindow? GetNearestWindow(Rectangle bounds)
    {
        GameWindow? nearestWindow = null;
        float minDistance = float.MaxValue;

        lock (windowLock)
        {
            foreach (var window in windows)
            {
                float distance = DistanceToWindow(bounds, window.AdjustedBounds);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestWindow = window;
                }
            }
        }

        return nearestWindow;
    }

    private float DistanceToWindow(Rectangle bounds, Rectangle windowBounds)
    {
        // ウィンドウとの最短距離を計算
        float dx = Math.Max(0, Math.Max(windowBounds.Left - bounds.Right, bounds.Left - windowBounds.Right));
        float dy = Math.Max(0, Math.Max(windowBounds.Top - bounds.Bottom, bounds.Top - windowBounds.Bottom));
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    public async Task BringWindowToFrontAsync(GameWindow window)
    {
        lock (windowLock)
        {
            windows.Remove(window);
            windows.Add(window);
        }
        await Task.Run(() => window.BringToFront());
    }
    public void HandleWindowActivation(GameWindow window)
    {
        lock (windowLock)
        {
            // すでに最前面の場合は何もしない
            if (windows.IndexOf(window) == windows.Count - 1) return;

            // 親がある場合は何もしない（親の操作に従う）
            if (window.Parent != null) return;

            // 子孫ウィンドウを含むすべての関連ウィンドウを収集
            var relatedWindows = CollectRelatedWindows(window);

            // 関連ウィンドウを現在のリストから削除
            foreach (var relatedWindow in relatedWindows)
            {
                windows.Remove(relatedWindow);
            }

            // 関連ウィンドウを順番を保ったまま最後に追加
            windows.AddRange(relatedWindows);

            // 物理的なウィンドウの重なり順も更新
            foreach (var relatedWindow in relatedWindows)
            {
                relatedWindow.BringToFront();
            }

            Console.WriteLine($"Window {window.Id} and its related windows brought to front");
        }
    }
    private List<GameWindow> CollectRelatedWindows(GameWindow root)
    {
        var result = new List<GameWindow>();
        CollectRelatedWindowsRecursive(root, result);
        return result;
    }

    private void CollectRelatedWindowsRecursive(GameWindow window, List<GameWindow> collection)
    {
        // 自身を追加
        collection.Add(window);

        // 子ウィンドウを追加（Z-orderを維持するため、現在の順序で追加）
        var childWindows = window.Children.OfType<GameWindow>()
            .OrderBy(w => windows.IndexOf(w));

        foreach (var child in childWindows)
        {
            CollectRelatedWindowsRecursive(child, collection);
        }
    }

    // イベントハンドラでキャッシュを無効化
    void IWindowObserver.OnWindowChanged(GameWindow window, WindowChangeType changeType)
    {
        if (changeType == WindowChangeType.Deleted)
        {
            lock (windowLock)
            {
                windows.Remove(window);
            }
        }
    }
}