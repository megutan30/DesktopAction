using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MultiWindowActionGame
{
    public class GameWindow : Form, IWindowComponent, IWindowSubject,IEffectTarget
    {
        public Rectangle ClientBounds { get; private set; }
        public Rectangle AdjustedBounds { get; private set; }
        public bool CanEnter { get; set; } = true;
        public bool CanExit { get; set; } = true;
        public Size OriginalSize { get; private set; }
        public IWindowStrategy Strategy { get; private set; }
        public Rectangle Bounds => AdjustedBounds;
        private new const int Margin = 0;
        protected IWindowStrategy strategy;
        private List<IWindowObserver> observers = new List<IWindowObserver>();
        private readonly List<IWindowEffect> effects = new();
        private readonly HashSet<IEffectTarget> containedTargets = new();
        public Guid Id { get; } = Guid.NewGuid();
        public event EventHandler<EventArgs> WindowMoved;
        public event EventHandler<SizeChangedEventArgs> WindowResized;
        public event EventHandler? MoveStarted;
        public event EventHandler? MoveEnded;
        public event EventHandler? ResizeStarted;
        public event EventHandler? ResizeEnded;

        private bool isMoving = false;
        private bool isResizing = false;

        private const int WM_SYSCOMMAND = 0x0112;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int SC_CLOSE = 0xF060;
        private const int SC_MINIMIZE = 0xF020;
        private const int SC_MAXIMIZE = 0xF030;
        private const int SC_RESTORE = 0xF120;

        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;

        private const int WM_NCHITTEST = 0x0084;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int SC_MOVE = 0xF010;
        private const int HTCAPTION = 2;

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private Rectangle GetClientRectangle()
        {
            RECT rect;
            GetClientRect(this.Handle, out rect);
            POINT point = new POINT { X = rect.Left, Y = rect.Top };
            ClientToScreen(this.Handle, ref point);

            return new Rectangle(point.X, point.Y, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        public virtual void OnWindowMoved()
        {
            WindowMoved?.Invoke(this, EventArgs.Empty);
        }

        public void OnWindowResized()
        {
            WindowResized?.Invoke(this, new SizeChangedEventArgs(this.Size));
        }

        public GameWindow(Point location, Size size, IWindowStrategy strategy)
        {
            this.strategy = strategy;
            this.Strategy = strategy;
            this.OriginalSize = size;

            this.MinimumSize = new Size(100, 100);

            this.Load += GameWindow_Load;

            InitializeWindow(location, size);
            InitializeEvents();

            Console.WriteLine($"Created window with ID: {Id}, Location: {Location}, Size: {Size}");
            this.Show();
        }
        private void InitializeWindow(Point location, Size size)
        {
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = location;
            this.Size = size;
            this.TopMost = true;
            this.ControlBox = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new Size(100, 100);
        }

        private void InitializeEvents()
        {
            this.Load += GameWindow_Load;
            this.Move += GameWindow_Move;
            this.Resize += GameWindow_Resize;
            this.Click += GameWindow_Click;
            UpdateBounds();
        }

        public void AddEffect(IWindowEffect effect)
        {
            effects.Add(effect);
        }
        public void RemoveEffect(IWindowEffect effect)
        {
            effects.Remove(effect);
        }

        public bool CanReceiveEffect(IWindowEffect effect)
        {
            if (isMoving && effect.Type == EffectType.Resize) return false;
            if (isResizing && effect.Type == EffectType.Movement) return false;
            return true;
        }

        public void ApplyEffect(IWindowEffect effect)
        {
            if (!CanReceiveEffect(effect)) return;

            effect.Apply(this);

            // 含まれているターゲットに効果を適用
            foreach (var target in containedTargets)
            {
                if (target.CanReceiveEffect(effect))
                {
                    target.ApplyEffect(effect);
                }
            }
        }

        public bool IsCompletelyContained(GameWindow container)
        {
            // ウィンドウが別のウィンドウに完全に含まれているかチェック
            return container.AdjustedBounds.Contains(this.AdjustedBounds);
        }

        private void UpdateContainedTargets()
        {
            var oldTargets = new HashSet<IEffectTarget>(containedTargets);
            containedTargets.Clear();

            foreach (var component in WindowManager.Instance.GetAllComponents())
            {
                if (component is IEffectTarget target &&
                    component != this &&
                    target.IsCompletelyContained(this))
                {
                    containedTargets.Add(target);
                }
            }

            // 新しく含まれるようになったターゲットと離れたターゲットを処理
            foreach (var target in containedTargets.Except(oldTargets))
            {
                OnTargetEntered(target);
            }
            foreach (var target in oldTargets.Except(containedTargets))
            {
                OnTargetExited(target);
            }
        }
        private void OnTargetEntered(IEffectTarget target)
        {
            // ターゲットが入ってきたときの処理
            foreach (var effect in effects.Where(e => e.IsActive))
            {
                if (target.CanReceiveEffect(effect))
                {
                    target.ApplyEffect(effect);
                }
            }
        }

        private void OnTargetExited(IEffectTarget target)
        {
            // ターゲットが出て行ったときの処理
            // 必要に応じて効果をクリアするなど
        }
        public void AddChild(IWindowComponent child)
        {
            throw new NotSupportedException("GameWindow cannot have children");
        }

        public void RemoveChild(IWindowComponent child)
        {
            throw new NotSupportedException("GameWindow cannot have children");
        }

        public IWindowComponent? GetChild(int index)
        {
            return null;
        }

        public int ChildCount => 0;

        public async Task UpdateAsync(float deltaTime)
        {
            strategy.Update(this, deltaTime);
            strategy.HandleInput(this);
            UpdateBounds();
            UpdateContainedTargets();
        }
        //public async Task UpdateAsync(float deltaTime)
        //{
        //    // 既存の更新処理
        //    await base.UpdateAsync(deltaTime);

        //    // 含まれているターゲットの更新
        //    UpdateContainedTargets();

        //    // アクティブな効果の適用
        //    foreach (var effect in effects.Where(e => e.IsActive))
        //    {
        //        foreach (var target in containedTargets)
        //        {
        //            if (target.CanReceiveEffect(effect))
        //            {
        //                target.ApplyEffect(effect);
        //            }
        //        }
        //    }
        //}
        public void Draw(Graphics g)
        {
            g.DrawString($"Window ID: {Id}", this.Font, Brushes.Black, 10, 10);
            g.DrawString($"Type: {strategy.GetType().Name}", this.Font, Brushes.Black, 10, 30);


            if (MainGame.IsDebugMode)
            {
                DrawDebugInfo(g);
            }
        }

        private void DrawDebugInfo(Graphics g)
        {
            // 含まれているターゲットの数を表示
            g.DrawString($"Contained Targets: {containedTargets.Count}",
                this.Font, Brushes.Red, 10, 50);

            // 効果の状態を表示
            int y = 70;
            foreach (var effect in effects)
            {
                g.DrawString($"Effect: {effect.Type} Active: {effect.IsActive}",
                    this.Font, Brushes.Blue, 10, y);
                y += 20;
            }
        }

        protected override void OnMove(EventArgs e)
        {
            base.OnMove(e);
            WindowManager.Instance.InvalidateCache();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            WindowManager.Instance.InvalidateCache();
        }

        private void GameWindow_Load(object sender, EventArgs e)
        {
            // システムメニューを取得し、閉じるボタンを無効化
            IntPtr hMenu = (IntPtr)GetSystemMenu(this.Handle, false);
            EnableMenuItem(hMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
        }

        private void GameWindow_Move(object? sender, EventArgs e)
        {
            UpdateBounds();
            NotifyObservers(WindowChangeType.Moved);
        }

        private void GameWindow_Resize(object? sender, EventArgs e)
        {
            UpdateBounds();
            NotifyObservers(WindowChangeType.Resized);
            strategy.HandleResize(this);
        }
        private void GameWindow_Click(object? sender, EventArgs e)
        {
            WindowManager.Instance.BringWindowToFront(this);
        }
        private void UpdateBounds()
        {
            Rectangle clientRect = GetClientRectangle();
            ClientBounds = clientRect;
            AdjustedBounds = new Rectangle(
                clientRect.X + Margin,
                clientRect.Y + Margin,
                clientRect.Width - (2 * Margin),
                clientRect.Height - (2 * Margin)
            );
            Console.WriteLine($"Updated bounds for window {Id}: Location = {Location}, Size = {Size}, AdjustedBounds = {AdjustedBounds}");
        }

        public void AddObserver(IWindowObserver observer)
        {
            observers.Add(observer);
        }

        public void RemoveObserver(IWindowObserver observer)
        {
            observers.Remove(observer);
        }

        public void NotifyObservers(WindowChangeType changeType)
        {
            foreach (var observer in observers)
            {
                observer.OnWindowChanged(this, changeType);
            }
        }

        public new void BringToFront()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(BringToFront));
            }
            else
            {
                base.BringToFront();
            }
        }

        public bool IsResizable()
        {
            return strategy is ResizableWindowStrategy;
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_NCHITTEST:
                    // タイトルバーのヒットテストを処理
                    base.WndProc(ref m);
                    if (m.Result.ToInt32() == HTCAPTION)
                    {
                        m.Result = IntPtr.Zero;
                    }
                    return;

                case WM_NCLBUTTONDOWN:
                    // タイトルバーでのマウス左ボタンクリックを処理
                    if (m.WParam.ToInt32() == HTCAPTION)
                    {
                        return;  // タイトルバーでのクリックを無視
                    }
                    break;

                case 0x0214: // WM_SIZING
                    ResizeStarted?.Invoke(this, EventArgs.Empty);
                    break;
                case 0x0231: // WM_ENTERSIZEMOVE
                    if (Strategy is MovableWindowStrategy)
                    {
                        isMoving = true;
                        MoveStarted?.Invoke(this, EventArgs.Empty);
                    }
                    else if (Strategy is ResizableWindowStrategy)
                    {
                        isResizing = true;
                        ResizeStarted?.Invoke(this, EventArgs.Empty);
                    }
                    break;
                case 0x0232: // WM_EXITSIZEMOVE
                    if (isMoving)
                    {
                        isMoving = false;
                        MoveEnded?.Invoke(this, EventArgs.Empty);
                    }
                    else if (isResizing)
                    {
                        isResizing = false;
                        ResizeEnded?.Invoke(this, EventArgs.Empty);
                    }
                    break;

                case WM_SYSCOMMAND:
                    int command = m.WParam.ToInt32() & 0xFFF0;
                    if (command == SC_CLOSE) return;
                    if (command == SC_MINIMIZE)
                        (Strategy as DeletableWindowStrategy)?.HandleMinimize(this);
                    else if (command == SC_RESTORE)
                        (Strategy as DeletableWindowStrategy)?.HandleRestore(this);
                    break;
                case WM_MOUSEMOVE:
                    Strategy.UpdateCursor(this, PointToClient(Cursor.Position));
                    break;

            }

            base.WndProc(ref m);

            if (strategy is ResizableWindowStrategy resizableStrategy)
            {
                resizableStrategy.HandleWindowMessage(this, m);
            }
            if (strategy is MovableWindowStrategy movableStrategy)
            {
                movableStrategy.HandleWindowMessage(this, m);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Draw(e.Graphics);
        }

        public class SizeChangedEventArgs : EventArgs
        {
            public Size NewSize { get; }

            public SizeChangedEventArgs(Size newSize)
            {
                NewSize = newSize;
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int CS_DBLCLKS = 0x8;
                var cp = base.CreateParams;
                cp.ClassStyle |= CS_DBLCLKS;
                return cp;
            }
        }
    }
}