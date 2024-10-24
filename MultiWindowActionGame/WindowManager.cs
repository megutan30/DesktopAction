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

    private WindowManager()
    {
        CreateInitialWindows();
    }

    public void SetPlayer(Player player)
    {
        this.player = player;
    }

    public Player? GetPlayerInWindow(GameWindow window)
    {
        if (player != null && player.GetCurrentWindow() == window)
        {
            return player;
        }
        return null;
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
                    if (window.IsResizable())
                    {
                        window.WindowResized += OnWindowResized;
                    }
                }
            }
        }
    }

    private void OnWindowResized(object? sender, SizeChangedEventArgs e)
    {
        if (sender is GameWindow window)
        {
            // ウィンドウ内のプレイヤーのサイズを更新
            Player? player = GetPlayerInWindow(window);
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

            // ウィンドウをZ-orderで降順ソート
            var sortedWindows = windowPoints
                .OrderByDescending(pair => windows.IndexOf(pair.Key))
                .ToList();

            if (sortedWindows.Count == 0)
            {
                return null;
            }

            // 現在のウィンドウの情報を取得
            var currentWindowPoints = currentWindow != null && windowPoints.ContainsKey(currentWindow)
                ? windowPoints[currentWindow]
                : new HashSet<Point>();

            // 最前面のウィンドウを取得
            var frontmostWindow = sortedWindows.FirstOrDefault();

            if (currentWindow == null)
            {
                // 現在のウィンドウがない場合、すべての点を含むウィンドウを探す
                var fullWindow = sortedWindows
                    .FirstOrDefault(w => w.Value.Count == checkPoints.Length);
                return fullWindow.Key ?? frontmostWindow.Key;
            }

            // 現在のウィンドウに点が残っている場合の処理
            if (currentWindowPoints.Count > 0)
            {
                // 前面のウィンドウへの移動条件をチェック
                var frontWindows = sortedWindows
                    .Where(w => IsWindowInFrontOf(w.Key, currentWindow))
                    .ToList();

                foreach (var frontWindow in frontWindows)
                {
                    bool hasBottomPoint = frontWindow.Value.Contains(checkPoints[0]) ||
                                        frontWindow.Value.Contains(checkPoints[1]);
                    if (hasBottomPoint)
                    {
                        return frontWindow.Key;
                    }
                }

                // 後面のウィンドウへの移動条件をチェック
                var backWindows = sortedWindows
                    .Where(w => !IsWindowInFrontOf(w.Key, currentWindow))
                    .ToList();

                foreach (var backWindow in backWindows)
                {
                    if (backWindow.Value.Count == checkPoints.Length)
                    {
                        return backWindow.Key;
                    }
                }

                // 条件を満たすウィンドウがない場合は現在のウィンドウを維持
                return currentWindow;
            }
            else
            {
                // 現在のウィンドウに点が残っていない場合
                // 最も多くの点を含む前面のウィンドウを探す
                var bestFrontWindow = sortedWindows
                    .Where(w => w.Value.Count > 0)
                    .OrderByDescending(w => w.Value.Count)
                    .FirstOrDefault();

                if (bestFrontWindow.Key != null)
                {
                    // 前面のウィンドウに下部の点が含まれているか、
                    // すべての点が含まれている場合に移動
                    bool hasBottomPoint = bestFrontWindow.Value.Contains(checkPoints[0]) ||
                                        bestFrontWindow.Value.Contains(checkPoints[1]);
                    bool hasAllPoints = bestFrontWindow.Value.Count == checkPoints.Length;

                    if (hasBottomPoint || hasAllPoints)
                    {
                        return bestFrontWindow.Key;
                    }
                }
            }
        }

        return currentWindow; // 適切な移動先が見つからない場合は現在のウィンドウを維持
    }

    public void BringWindowToFront(GameWindow window)
    {
        lock (windowLock)
        {
            windows.Remove(window);
            windows.Add(window);
        }
        UpdatePlayerWindow();
    }

    private void UpdatePlayerWindow()
    {
        if (player != null)
        {
            GameWindow? newWindow = GetTopWindowAt(player.Bounds,player.GetCurrentWindow());
            if (newWindow != player.GetCurrentWindow())
            {
                player.SetCurrentWindow(newWindow);
            }
        }
    }
    public bool IsAdjacentTo(Rectangle rect1, Rectangle rect2)
    {
        return (Math.Abs(rect1.Right - rect2.Left) <= 100 || Math.Abs(rect1.Left - rect2.Right) <= 100 ||
                Math.Abs(rect1.Bottom - rect2.Top) <= 100 || Math.Abs(rect1.Top - rect2.Bottom) <= 100) &&
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

    public void OnWindowChanged(GameWindow window, WindowChangeType changeType)
    {
        if (changeType == WindowChangeType.Deleted)
        {
            lock (windowLock)
            {
                windows.Remove(window);
            }
        }
        // すべてのウィンドウの変更で移動可能領域を更新
        UpdatePlayerMovableRegion();
    }
    private void UpdatePlayerMovableRegion()
    {
        if (player != null && player.GetCurrentWindow() != null)
        {
            Region newMovableRegion = CalculateMovableRegion(player.GetCurrentWindow());
            player.UpdateMovableRegion(newMovableRegion);
        }
    }
}