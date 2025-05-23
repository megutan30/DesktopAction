﻿using MultiWindowActionGame;
using System.Drawing.Drawing2D;
using System.Numerics;
using System.Runtime.InteropServices;
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

    private OverlayForm? overlayForm;

    // Z-order の優先順位を定義
    public enum ZOrderPriority
    {
        DebugLayer = 1,
        Player = 2,
        Bottom = 3,
        Goal = 4,
        Button = 5,
        WindowMark = 6,
        Window = 7,
    }
    private readonly Dictionary<IntPtr, ZOrderPriority> handlePriorities = new();
    private readonly SortedDictionary<ZOrderPriority, List<Form>> formsByPriority = new();

    private WindowManager() { }

    public void Initialize()
    {
        if (isInitialized) return;

        overlayForm = new OverlayForm(this);
        RegisterFormOrder(overlayForm, ZOrderPriority.DebugLayer);
        overlayForm.Show();

        isInitialized = true;
    }
    public async Task InitializeWindowsAsync(IEnumerable<GameWindow> windows)
    {
        var tasks = windows.Select(w => w.InitializationTask);
        await Task.WhenAll(tasks);
        UpdateWindowGroupZOrder();
    }
    public void SetPlayer(PlayerForm player)
    {
        this.player = player;
    }
    public GameWindow? GetParentWindow(IEffectTarget child)
    {
        return parentChildRelations.TryGetValue(child, out var parent) ? parent : null;
    }

    public IReadOnlyList<GameWindow> GetAllWindows()
    {
        lock (windowLock)
        {
            return windows.ToList();
        }
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
    public void RegisterFormOrder(Form form, ZOrderPriority priority)
    {
        lock (windowLock)
        {
            handlePriorities[form.Handle] = priority;
            if (!formsByPriority.ContainsKey(priority))
            {
                formsByPriority[priority] = new List<Form>();
            }
            formsByPriority[priority].Add(form);
            UpdateFormZOrder(form, priority);  // 登録時に即座にZ-orderを設定
        }
    }
    public void UnregisterFormOrder(Form form)
    {
        lock (windowLock)
        {
            // フォームがまだ破棄されていない場合のみ処理を行う
            if (!form.IsDisposed && handlePriorities.TryGetValue(form.Handle, out var priority))
            {
                handlePriorities.Remove(form.Handle);
                if (formsByPriority.ContainsKey(priority))
                {
                    formsByPriority[priority].Remove(form);
                }
            }
        }
    }

    private IntPtr GetInsertAfterHandle(ZOrderPriority priority)
    {
        return WindowMessages.HWND_TOPMOST;
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
            var components = new List<IEffectTarget>();

            // ZOrderPriorityの順序に従ってコンポーネントを追加
            foreach (var priority in Enum.GetValues<ZOrderPriority>().OrderByDescending(p => (int)p))
            {
                switch (priority)
                {
                    case ZOrderPriority.Player:
                        if (player != null)
                        {
                            components.Add(player);
                        }
                        break;

                    case ZOrderPriority.Window:
                        components.AddRange(windows);
                        break;

                    case ZOrderPriority.Button:
                        components.AddRange(
                            formsByPriority
                                .Where(kv => kv.Key == ZOrderPriority.Button)
                                .SelectMany(kv => kv.Value)
                                .OfType<IEffectTarget>()
                        );
                        break;

                    case ZOrderPriority.Goal:
                        // ゴールの追加（もし必要な場合）
                        components.AddRange(
                            formsByPriority
                                .Where(kv => kv.Key == ZOrderPriority.Goal)
                                .SelectMany(kv => kv.Value)
                                .OfType<IEffectTarget>()
                        );
                        break;
                }
            }

            return components;
        }
    }

    public IReadOnlyList<GameButton> GetAllButtons()
    {
        lock (windowLock)
        {
            // formsByPriorityからZOrderPriority.Buttonのものを取得
            if (formsByPriority.TryGetValue(ZOrderPriority.Button, out var buttonForms))
            {
                return buttonForms.OfType<GameButton>().ToList();
            }
            return new List<GameButton>();
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
            var allTargets = GetAllComponents();
            foreach (var target in allTargets)
            {
                if (target.Parent != null && !(target is Goal) && !(target is PlayerForm))
                {
                    int currentIndex = allTargets.ToList().IndexOf(target);
                    var coveringTargets = allTargets
                        .Skip(currentIndex + 1)
                        .Where(t => t.Bounds.IntersectsWith(target.Bounds));

                    // GameWindowの場合はCollisionBoundsを使用
                    if (target is GameWindow window)
                    {
                        OutlineRenderer.DrawClippedOutline(g, target, coveringTargets, window.CollisionBounds);
                    }
                    else
                    {
                        OutlineRenderer.DrawClippedOutline(g, target, coveringTargets, target.Bounds);
                    }
                }

                target.Draw(g);
            }
        }
    }
    public void DrawMarks(Graphics g)
    {
        lock (windowLock)
        {
            foreach (var window in windows)
            {
                DrawWindowMark(g, window);
                if (window.Parent != null)
                {
                    //DrawParentChildConnection(g, window);
                }
            }
        }
    }
    private void DrawWindowMark(Graphics g, GameWindow window)
    {
        Rectangle markBounds = window.CollisionBounds;

        // この領域と交差する、より前面にあるウィンドウを取得
        var coveringWindows = windows
            .Where(w => w != window &&
                       windows.IndexOf(w) > windows.IndexOf(window) &&
                       w.CollisionBounds.IntersectsWith(markBounds))
            .ToList();

        // マウスの現在位置を取得
        Point mousePos = Cursor.Position;
        bool isHovered = window.RectangleToScreen(window.ClientRectangle).Contains(mousePos) ||
                         new Rectangle(window.Location, new Size(window.Width, window.RectangleToScreen(window.ClientRectangle).Y - window.Location.Y)).Contains(mousePos);

        // 前面のウィンドウがマウス位置と重なっているかチェック
        if (isHovered)
        {
            foreach (var coveringWindow in coveringWindows)
            {
                if (coveringWindow.CollisionBounds.Contains(mousePos))
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
                    clipRegion.Exclude(coveringWindow.CollisionBounds);
                }

                Region originalClip = g.Clip;
                g.Clip = clipRegion;

                // isHoveredの結果に基づいてマークを描画
                window.Strategy.DrawStrategyMark(g, window.CollisionBounds, isHovered);

                g.Clip = originalClip;
            }
        }
        else
        {
            window.Strategy.DrawStrategyMark(g, window.CollisionBounds, isHovered);
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
        lock (windowLock)
        {
            // 親がある場合は、同じ親を持つウィンドウ間でのみ順序を変更
            if (window.Parent != null)
            {
                // 同じ親を持つウィンドウとその子孫を含むグループを作成
                var siblings = windows.Where(w => w.Parent == window.Parent).ToList();
                var nonSiblings = windows.Where(w => w.Parent != window.Parent).ToList();

                // 各兄弟ウィンドウとその子孫を一時的に除外
                var siblingGroups = new List<List<GameWindow>>();
                foreach (var sibling in siblings)
                {
                    var group = new List<GameWindow> { sibling };
                    group.AddRange(sibling.GetAllDescendants());
                    siblingGroups.Add(group);

                    foreach (var groupWindow in group)
                    {
                        windows.Remove(groupWindow);
                    }
                }

                // クリックされたウィンドウのグループを最後に移動
                var clickedGroup = siblingGroups.Find(g => g[0] == window);
                siblingGroups.Remove(clickedGroup);
                siblingGroups.Add(clickedGroup);

                // 適切な位置に全てのグループを戻す
                var insertIndex = nonSiblings.FindIndex(w => w == window.Parent) + 1;
                foreach (var group in siblingGroups)
                {
                    windows.InsertRange(insertIndex, group);
                    insertIndex += group.Count;
                }
            }
            else
            {
                // 親がない場合は、ルートレベルのウィンドウ間で順序を変更
                var windowGroup = new List<GameWindow> { window };
                windowGroup.AddRange(window.GetAllDescendants());  // 子孫も含める

                // グループ全体を一度削除
                foreach (var groupWindow in windowGroup)
                {
                    windows.Remove(groupWindow);
                }

                // グループ全体を最前面に移動
                windows.AddRange(windowGroup);
            }

            // 同じ優先度内での順序も更新
            if (handlePriorities.TryGetValue(window.Handle, out var priority))
            {
                if (formsByPriority.ContainsKey(priority))
                {
                    var forms = formsByPriority[priority];
                    foreach (var groupWindow in CollectRelatedWindows(window))
                    {
                        if (forms.Contains(groupWindow))
                        {
                            forms.Remove(groupWindow);
                            forms.Add(groupWindow);
                        }
                    }
                }
            }

            UpdateWindowGroupZOrder();
        }
    }
    public void UpdateDisplay()
    {
        overlayForm?.UpdateOverlay();
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
    public Region CalculateMovableRegion(GameWindow? currentWindow)
    {
        // currentWindowがnullの場合（プレイヤーがウィンドウの外にいる場合）、
        // メインフォームの領域を返す
        if (currentWindow == null && Program.mainForm != null)
        {
            return new Region(new Rectangle(0, 0,
                Program.mainForm.ClientSize.Width,
                Program.mainForm.ClientSize.Height));
        }

        // currentWindowが有効な場合は既存の処理を実行
        if (currentWindow != null)
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

        // どちらの条件も満たさない場合は空のリージョンを返す
        return new Region();
    }
    private bool IsAdjacentTo(Rectangle rect1, Rectangle rect2)
    {
        var settings = GameSettings.Instance.Gameplay;
        return (Math.Abs(rect1.Right - rect2.Left) <= settings.WindowSnapDistance ||
                Math.Abs(rect1.Left - rect2.Right) <= settings.WindowSnapDistance ||
                Math.Abs(rect1.Bottom - rect2.Top) <= settings.WindowSnapDistance ||
                Math.Abs(rect1.Top - rect2.Bottom) <= settings.WindowSnapDistance) &&
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
    public void UpdateWindowGroupZOrder()
    {
        // 優先度ごとに処理
        foreach (var priorityGroup in formsByPriority.Reverse())
        {
            // 同じ優先度内では、後ろのものから順に配置（新しく追加されたものが前面に来る）
            for (int i = priorityGroup.Value.Count - 1; i >= 0; i--)
            {
                var form = priorityGroup.Value[i];
                if (!form.IsDisposed && form.Handle != IntPtr.Zero)
                {
                    IntPtr insertAfter = i == priorityGroup.Value.Count - 1
                        ? GetInsertAfterHandle(priorityGroup.Key)  // 優先度グループの最前面
                        : priorityGroup.Value[i + 1].Handle;       // 同じグループの前のウィンドウの後ろ

                    WindowMessages.SetWindowPos(
                        form.Handle,
                        insertAfter,
                        0, 0, 0, 0,
                        WindowMessages.SWP_NOMOVE | WindowMessages.SWP_NOSIZE | WindowMessages.SWP_NOACTIVATE
                    );
                }
            }
        }
    }
    public void UpdateFormZOrder(Form form, ZOrderPriority priority)
    {
        if (!form.IsDisposed && form.Handle != IntPtr.Zero)
        {
            IntPtr insertAfter = GetInsertAfterHandle(priority);
            WindowMessages.SetWindowPos(
                form.Handle,
                insertAfter,
                0, 0, 0, 0,
                WindowMessages.SWP_NOMOVE | WindowMessages.SWP_NOSIZE | WindowMessages.SWP_NOACTIVATE
            );
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