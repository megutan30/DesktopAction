using MultiWindowActionGame;
using System.Numerics;
using static MultiWindowActionGame.GameWindow;

public class WindowManager : IWindowObserver
{
    private static readonly Lazy<WindowManager> lazy =
         new Lazy<WindowManager>(() => new WindowManager());

    public static WindowManager Instance { get { return lazy.Value; } }
    private List<GameWindow> windows = new List<GameWindow>();
    private object windowLock = new object();
    private PlayerForm? player;
    private bool isInitialized = false;

    private readonly Dictionary<GameWindow, HashSet<IEffectTarget>> containedTargetsCache = new();
    private Dictionary<IEffectTarget, GameWindow> parentChildRelations = new Dictionary<IEffectTarget, GameWindow>();
    private bool isCheckingParentChild = false;
    private bool needsUpdateCache = true;
    private Dictionary<GameWindow, GameWindow> childToParentRelations = new Dictionary<GameWindow, GameWindow>();
    private bool isCheckingRelations = false;

    private WindowManager() { }

    public void Initialize()
    {
        if (isInitialized) return;
        isInitialized = true;
    }
    public void InvalidateCache()
    {
        needsUpdateCache = true;
    }
    public void SetPlayer(PlayerForm player)
    {
        this.player = player;
    }
    public GameWindow? GetParentWindow(IEffectTarget child)
    {
        return parentChildRelations.TryGetValue(child, out var parent) ? parent : null;
    }
    public void CheckPotentialParentWindow(GameWindow operatedWindow)
    {
        lock (windowLock)
        {
            // 現在の親子関係が有効かチェック（変更なし）
            if (operatedWindow.Parent != null)
            {
                if (!operatedWindow.Parent.AdjustedBounds.Contains(operatedWindow.AdjustedBounds))
                {
                    var oldParent = operatedWindow.Parent;
                    oldParent.RemoveChild(operatedWindow);
                    Console.WriteLine($"Window {operatedWindow.Id} detached from parent {oldParent.Id}");
                }
            }

            // 子ウィンドウとの関係をチェック（変更なし）
            foreach (var child in operatedWindow.Children.OfType<GameWindow>().ToList())
            {
                if (!operatedWindow.AdjustedBounds.Contains(child.AdjustedBounds))
                {
                    operatedWindow.RemoveChild(child);
                    Console.WriteLine($"Child {child.Id} detached from parent {operatedWindow.Id}");
                }
            }

            // 既存の親子グループを取得（ただし、親子関係の変更を許可するため、制限を緩和）
            var existingGroup = new HashSet<GameWindow>();
            if (operatedWindow.Children.Any())
            {
                existingGroup.Add(operatedWindow);
                existingGroup.UnionWith(operatedWindow.GetAllDescendants());
            }

            // 親候補を探す
            var allPotentialParents = windows
                .Where(w => !existingGroup.Contains(w) && w != operatedWindow &&
                       windows.IndexOf(w) < windows.IndexOf(operatedWindow))
                .OrderByDescending(w => windows.IndexOf(w));

            // 完全に含んでいる最も手前のウィンドウを探す
            GameWindow? bestParent = null;
            int bestParentIndex = -1;

            foreach (var potentialParent in allPotentialParents)
            {
                if (potentialParent.AdjustedBounds.Contains(operatedWindow.AdjustedBounds))
                {
                    int currentIndex = windows.IndexOf(potentialParent);
                    if (bestParentIndex < currentIndex)
                    {
                        // 現在の親より良い候補が見つかった場合
                        if (operatedWindow.Parent == null ||
                            currentIndex > windows.IndexOf(operatedWindow.Parent))
                        {
                            bestParent = potentialParent;
                            bestParentIndex = currentIndex;
                        }
                    }
                }
            }

            // より適切な親が見つかった場合は親子関係を変更
            if (bestParent != null && bestParent != operatedWindow.Parent)
            {
                // 既存の親子関係を解除
                if (operatedWindow.Parent != null)
                {
                    var oldParent = operatedWindow.Parent;
                    oldParent.RemoveChild(operatedWindow);
                    Console.WriteLine($"Window {operatedWindow.Id} detached from old parent {oldParent.Id}");
                }

                // 新しい親子関係を設定
                bestParent.AddChild(operatedWindow);
                Console.WriteLine($"Window {operatedWindow.Id} became child of new parent {bestParent.Id}");
                return;
            }

            // 子ウィンドウを探す処理（より前面のウィンドウも対象に）
            var potentialChildren = windows
                .Where(w => !existingGroup.Contains(w) && w != operatedWindow &&
                       windows.IndexOf(w) > windows.IndexOf(operatedWindow))
                .OrderBy(w => windows.IndexOf(w));

            foreach (var potentialChild in potentialChildren)
            {
                if (operatedWindow.AdjustedBounds.Contains(potentialChild.AdjustedBounds))
                {
                    // 既存の親子関係があっても、より適切な親となれる場合は変更
                    if (potentialChild.Parent == null ||
                        windows.IndexOf(operatedWindow) > windows.IndexOf(potentialChild.Parent))
                    {
                        if (potentialChild.Parent != null)
                        {
                            potentialChild.Parent.RemoveChild(potentialChild);
                        }
                        operatedWindow.AddChild(potentialChild);
                        Console.WriteLine($"Window {operatedWindow.Id} became parent of {potentialChild.Id}");
                    }
                }
            }
        }
    }
    public void RegisterWindow(GameWindow window)
    {
        lock (windowLock)
        {
            window.AddObserver(this);
            windows.Add(window);

            // 親子関係のチェックと更新
            CheckPotentialParentWindow(window);
        }
    }

    public void ClearWindows()
    {
        lock (windowLock)
        {
            // すべてのウィンドウを閉じる
            foreach (var window in windows.ToList())
            {
                window.RemoveObserver(this);  // オブザーバーの解除を追加
                window.Close();
            }
            windows.Clear();
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
    public int GetWindowZIndex(GameWindow window)
    {
        lock (windowLock)
        {
            return windows.IndexOf(window);
        }
    }
    public List<GameWindow> GetIntersectingWindows(Rectangle bounds)
    {
        lock (windowLock)
        {
            return windows
                .Where(w => w.CollisionBounds.IntersectsWith(bounds))
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
                DrawWindowBase(g, window);
            }

            foreach (var window in windows)
            {
                DrawWindowMark(g, window);
            }
        }
    }
    private void DrawWindowBase(Graphics g, GameWindow window)
    {
        // 親子関係のアウトラインなど、ウィンドウの基本的な描画
        if (window.Parent != null)
        {
            Color parentColor = window.Parent.BackColor;
            Color outlineColor = Color.FromArgb(
                Math.Max(0, parentColor.R - 50),
                Math.Max(0, parentColor.G - 50),
                Math.Max(0, parentColor.B - 50)
            );

            DrawParentChildConnection(g, window);
            using (Pen outlinePen = new Pen(outlineColor, 3))
            {
                g.DrawRectangle(outlinePen, window.CollisionBounds);
            }
        }

        // デバッグ情報など他の描画
        if (MainGame.IsDebugMode)
        {
            //window.DrawDebugInfo(g);
        }
        else
        {
            g.DrawString($"Window ID: {window.Id}", window.Font, Brushes.Black,
                window.AdjustedBounds.X + 10, window.AdjustedBounds.Y + 10);
            g.DrawString($"Type: {window.Strategy.GetType().Name}", window.Font, Brushes.Black,
                window.AdjustedBounds.X + 10, window.AdjustedBounds.Y + 30);
        }
    }

    private void DrawWindowMark(Graphics g, GameWindow window)
    {
        Rectangle markBounds = window.CollisionBounds;

        // この領域と交差する、より前面にあるウィンドウを取得
        var coveringWindows = windows
            .Where(w => w != window &&
                       windows.IndexOf(w) > windows.IndexOf(window) &&
                       w.AdjustedBounds.IntersectsWith(markBounds))
            .ToList();

        // マウスの現在位置を取得
        Point mousePos = Cursor.Position;
        bool isHovered = window.ClientRectangle.Contains(window.PointToClient(mousePos));

        // 前面のウィンドウがマウス位置と重なっているかチェック
        if (isHovered)
        {
            foreach (var coveringWindow in coveringWindows)
            {
                if (coveringWindow.AdjustedBounds.Contains(mousePos))
                {
                    isHovered = false;
                    break;
                }
            }
        }

        if (coveringWindows.Any())
        {
            using (Region clipRegion = new Region(markBounds))
            {
                foreach (var coveringWindow in coveringWindows)
                {
                    clipRegion.Exclude(coveringWindow.AdjustedBounds);
                }

                Region originalClip = g.Clip;
                g.Clip = clipRegion;

                // isHoveredの結果に基づいてマークを描画
                window.Strategy.DrawStrategyMark(g, window.AdjustedBounds, isHovered);

                g.Clip = originalClip;
            }
        }
        else
        {
            window.Strategy.DrawStrategyMark(g, window.AdjustedBounds, isHovered);
        }
    }
    public void DrawDebugInfo(Graphics g, Rectangle playerBounds)
    {
        if (!MainGame.IsDebugMode) return;

        lock (windowLock)
        {
            foreach (var window in windows)
            {
                g.DrawRectangle(new Pen(Color.Blue, 1), window.AdjustedBounds);

                if (window.Parent != null)
                {
                    DrawParentChildConnection(g, window);
                }

                Rectangle intersection = Rectangle.Intersect(window.AdjustedBounds, playerBounds);
                if (!intersection.IsEmpty)
                {
                    g.FillRectangle(new SolidBrush(Color.FromArgb(100, Color.Red)), intersection);
                }
            }
        }
    }
    private void DrawParentChildConnection(Graphics g, GameWindow childWindow)
    {
        using (var pen = new Pen(Color.Yellow, 2))
        {
            Point childCenter = new Point(
                childWindow.Bounds.X + childWindow.Bounds.Width / 2,
                childWindow.Bounds.Y + childWindow.Bounds.Height / 2
            );
            Point parentCenter = new Point(
                childWindow.Parent.Bounds.X + childWindow.Parent.Bounds.Width / 2,
                childWindow.Parent.Bounds.Y + childWindow.Parent.Bounds.Height / 2
            );
            g.DrawLine(pen, childCenter, parentCenter);
        }
    }
    private void DrawWindowDebugInfo(Graphics g, GameWindow window, bool isChild = false)
    {
        // デバッグ情報の描画処理
        Color borderColor = isChild ? Color.Yellow : Color.Blue;
        g.DrawRectangle(new Pen(borderColor, 1), window.AdjustedBounds);

        string info = $"ID: {window.Id} Z: {windows.IndexOf(window)}";
        if (isChild)
        {
            info += $" Parent: {window.Parent?.Id}";
        }
        g.DrawString(info, SystemFonts.DefaultFont, Brushes.Red,
            window.Location.X + 5, window.Location.Y + 5);
    }
    public GameWindow? GetWindowAt(Rectangle bounds, GameWindow? currentWindow = null)
    {
        lock (windowLock)
        {
            // 親ウィンドウのみを検索対象とする
            var topLevelWindows = windows.Where(w => w.Parent == null);
            foreach (var window in topLevelWindows.Reverse())
            {
                if (window == currentWindow) continue;
                if (window.AdjustedBounds.Contains(bounds))
                {
                    return window;
                }
            }
            return null;
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

    public GameWindow? GetTopWindowAt(Rectangle bounds, GameWindow? currentWindow)
    {
        Point[] checkPoints = new Point[]
        {
                new Point(bounds.Left, bounds.Bottom),
                new Point(bounds.Right, bounds.Bottom),
                new Point(bounds.Left, bounds.Top),
                new Point(bounds.Right, bounds.Top),
                new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2)
        };

        lock (windowLock)
        {
            if (currentWindow == null)
            {
                return windows
                    .Where(w => w.AdjustedBounds.Contains(bounds))
                    .OrderByDescending(w => windows.IndexOf(w))
                    .FirstOrDefault();
            }

            Dictionary<GameWindow, HashSet<Point>> windowPoints = new Dictionary<GameWindow, HashSet<Point>>();

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

            if (!windowPoints.Any()) return null;

            var currentWindowPoints = windowPoints.GetValueOrDefault(currentWindow, new HashSet<Point>());

            if (currentWindowPoints.Any())
            {
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
                    if (bestWindow.BottomEdge <= currentWindow.AdjustedBounds.Bottom)
                    {
                        return bestWindow.Window;
                    }
                }

                return currentWindow;
            }
            else
            {
                var candidateWindows = windowPoints
                    .Where(w => w.Value.Contains(checkPoints[0]) || w.Value.Contains(checkPoints[1]))
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
                    return candidateWindows.First().Window;
                }
            }
        }

        return currentWindow;
    }
    public GameWindow? GetWindowFullyContaining(Rectangle bounds)
    {
        lock (windowLock)
        {
            return windows
                .Where(w => w.Bounds.Contains(bounds))
                .OrderByDescending(w => windows.IndexOf(w))
                .FirstOrDefault();
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
            var topLevelWindows = windows.Where(w => w.Parent == null && w != currentWindow);
            foreach (var window in topLevelWindows)
            {
                if (window.AdjustedBounds.IntersectsWith(currentWindow.AdjustedBounds) ||
                    IsAdjacentTo(window.AdjustedBounds, currentWindow.AdjustedBounds))
                {
                    movableRegion.Union(window.AdjustedBounds);
                    foreach (var child in window.GetAllDescendants())
                    {
                        movableRegion.Union(child.AdjustedBounds);
                    }
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
            // プレイヤーが関連するウィンドウの場合は特別処理
            if (player != null && (window == player.Parent ||
                window.GetAllDescendants().Contains(player.Parent)))
            {
                player.BringToFront();
            }

            if (windows.IndexOf(window) == windows.Count - 1) return;

            if (window.Parent != null)
            {
                ReorderSiblingWindows(window);
                return;
            }

            var currentOrder = CollectRelatedWindows(window)
                .OrderBy(w => windows.IndexOf(w))
                .ToList();

            var orderDiffs = new Dictionary<GameWindow, int>();
            for (int i = 0; i < currentOrder.Count; i++)
            {
                orderDiffs[currentOrder[i]] = i;
            }

            foreach (var relatedWindow in currentOrder)
            {
                windows.Remove(relatedWindow);
            }

            int baseIndex = windows.Count;
            foreach (var relatedWindow in currentOrder)
            {
                int newIndex = baseIndex + orderDiffs[relatedWindow];
                windows.Insert(Math.Min(newIndex, windows.Count), relatedWindow);
                relatedWindow.BringToFront();
            }
        }
    }
    private void ReorderSiblingWindows(GameWindow clickedWindow)
    {
        if (clickedWindow.Parent == null) return;

        lock (windowLock)
        {
            // クリックされたウィンドウとその子孫をすべて取得
            var clickedWindowGroup = new List<GameWindow> { clickedWindow };
            clickedWindowGroup.AddRange(clickedWindow.GetAllDescendants());

            // 現在のウィンドウグループを一時的にリストから削除
            foreach (var window in clickedWindowGroup)
            {
                windows.Remove(window);
            }

            // 最前面のウィンドウのインデックスを取得
            int maxZIndex = windows.Count;

            // クリックされたウィンドウグループを最前面に配置
            foreach (var window in clickedWindowGroup.OrderBy(w => windows.IndexOf(w)))
            {
                windows.Insert(maxZIndex, window);
                window.BringToFront();
            }

            Console.WriteLine($"Reordered window {clickedWindow.Id} within siblings. New Z-index: {windows.IndexOf(clickedWindow)}");
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