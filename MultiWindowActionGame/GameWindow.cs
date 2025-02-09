using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace MultiWindowActionGame
{
    public class GameWindow : BaseEffectTarget, IWindowSubject
    {
        public Rectangle ClientBounds { get; private set; }
        public Rectangle AdjustedBounds { get; private set; }
        public bool CanEnter { get; set; } = true;
        public bool CanExit { get; set; } = true;
        public Size OriginalSize { get; private set; }
        public IWindowStrategy Strategy { get; private set; }
        public override Rectangle Bounds => AdjustedBounds;
        public Rectangle FullBounds => new Rectangle(Location, Size);
        public bool IsInitializing { get; private set; } = true;
        private new const int Margin = 0;
        protected IWindowStrategy strategy;
        private List<IWindowObserver> observers = new List<IWindowObserver>();
        private readonly List<IWindowEffect> effects = new();
        public Guid Id { get; } = Guid.NewGuid();
        public event EventHandler<EventArgs> WindowMoved;
        public event EventHandler<SizeChangedEventArgs> WindowResized;
        public bool HasActiveEffects => effects.Any(e => e.IsActive);
        private TaskCompletionSource<bool> initializationTcs = new TaskCompletionSource<bool>();
        public Task InitializationTask => initializationTcs.Task;
        public Rectangle CollisionBounds => new(
            AdjustedBounds.X,
            Location.Y,
            AdjustedBounds.Size.Width,
            AdjustedBounds.Size.Height + (RectangleToScreen(ClientRectangle).Y - Location.Y)
        );

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Strategy.UpdateCursor(this, e.Location);
        }
        public GameWindow(Point location, Size size, IWindowStrategy strategy)
        {
            IsInitializing = true;
            this.strategy = strategy;
            this.Strategy = strategy;
            this.OriginalSize = size;
            this.MinimumSize = GameSettings.Instance.Window.MinimumSize;

            InitializeWindow(location, size);
            InitializeEvents();

            WindowManager.Instance.RegisterFormOrder(this, WindowManager.ZOrderPriority.Window);

            Debug.WriteLine($"Created window with ID: {Id}, Location: {Location}, Size: {Size}");
            this.Show();
            IsInitializing = false;
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
        }
        private void InitializeEvents()
        {
            this.Load += GameWindow_Load;
            this.Move += GameWindow_Move;
            this.Resize += GameWindow_Resize;
            this.Click += GameWindow_Click;
            UpdateBounds();
        }

        #region IEffectTarget Implementation
        public IEnumerable<IWindowEffect> GetActiveEffects()
        {
            return effects.Where(e => e.IsActive).ToList();
        }
        public override Size GetOriginalSize() => Size;
        public override void SetParent(GameWindow? newParent)
        {
            if (base.Parent != null)
            {
                base.Parent.RemoveChild(this);
            }
            base.Parent = newParent;
            newParent?.AddChild(this);
        }
        public override void OnMinimize()
        {
            IsMinimized = true;

            foreach (var child in Children.ToList())
            {
                child.OnMinimize();
                RemoveChild(child);
            }

            if (base.Parent != null)
            {
                base.Parent.RemoveChild(this);
            }

            WindowState = FormWindowState.Minimized;
        }
        public override void OnRestore()
        {
            IsMinimized = false;
            WindowState = FormWindowState.Normal;
            Show();

            WindowManager.Instance.CheckPotentialParentWindow(this);
            WindowManager.Instance.HandleWindowActivation(this);
            WindowManager.Instance.CheckPotentialParentWindow(this);
        }
        public override void AddChild(IEffectTarget child)
        {
            Children.Add(child);
            if (child is GameWindow window && window.Parent != this)
            {
                window.SetParent(this);
            }
        }
        public override void RemoveChild(IEffectTarget child)
        {
            base.RemoveChild(child);
            if (child is GameWindow window)
            {
                window.Parent = null;
            }
        }
        public override void UpdateTargetSize(Size newSize)
        {
            this.Size = newSize;
        }
        public override void UpdateTargetPosition(Point newPosition)
        {
            this.Location = newPosition;
        }
        public override bool CanReceiveEffect(IWindowEffect effect)
        {
            // 親からのエフェクトは常に受け入れる
            if (Parent != null)
            {
                return true;
            }

            // 自身が直接エフェクトを受け取る場合のチェック
            switch (effect.Type)
            {
                case EffectType.Movement:
                    return Strategy is MovableWindowStrategy;
                case EffectType.Resize:
                    return Strategy is ResizableWindowStrategy;
                case EffectType.Minimize:
                    return Strategy is MinimizableWindowStrategy;
                default:
                    return false;
            }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            initializationTcs.SetResult(true);
        }
        protected override void OnHandleDestroyed(EventArgs e)
        {
            WindowEffectManager.Instance.ClearEffects();
            base.OnHandleDestroyed(e);
        }
        public override void ApplyEffect(IWindowEffect effect)
        {
            if (!CanReceiveEffect(effect)) return;
            WindowEffectManager.Instance.AddEffect(effect);
            WindowEffectManager.Instance.ApplyEffects(this);
        }
        public bool IsChildOf(GameWindow potentialParent)
        {
            var current = this.Parent;
            while (current != null)
            {
                if (current == potentialParent) return true;
                current = current.Parent;
            }
            return false;
        }
        public IEnumerable<GameWindow> GetAllDescendants()
        {
            var descendants = new List<GameWindow>();
            foreach (var child in Children.OfType<GameWindow>())
            {
                descendants.Add(child);
                descendants.AddRange(child.GetAllDescendants());
            }
            return descendants;
        }
        #endregion

        #region IUpdatable and IDrawable Implementation
        public override async Task UpdateAsync(float deltaTime)
        {
            strategy.Update(this, deltaTime);
            strategy.HandleInput(this);
            UpdateBounds();
        }
        public override void Draw(Graphics g)
        {
        }
        #endregion
        
        #region Window Event Handlers
        private void GameWindow_Load(object sender, EventArgs e)
        {
            IntPtr hMenu = WindowMessages.GetSystemMenu(this.Handle, false);
            WindowMessages.EnableMenuItem(hMenu, WindowMessages.SC_CLOSE, WindowMessages.MF_BYCOMMAND | WindowMessages.MF_GRAYED);
        }
        private void GameWindow_Move(object? sender, EventArgs e)
        {
            UpdateBounds();
            WindowMoved?.Invoke(this, EventArgs.Empty);
            NotifyObservers(WindowChangeType.Moved);
        }
        private void GameWindow_Activated(object? sender, EventArgs e)
        {
            // 最小化状態からの復元ではない通常のアクティブ化の場合
            if (!IsMinimized && WindowState != FormWindowState.Minimized)
            {
                WindowManager.Instance.HandleWindowActivation(this);
                WindowManager.Instance.CheckPotentialParentWindow(this);
            }
        }
        private void GameWindow_Resize(object? sender, EventArgs e)
        {
            UpdateBounds();
            WindowResized?.Invoke(this, new SizeChangedEventArgs(this.Size));
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
        }
        public Point Center()
        {
            return new Point(
                Bounds.X + Bounds.Width / 2,
                Bounds.Y + Bounds.Height / 2
            );
        }
        private Rectangle GetClientRectangle()
        {
            WindowMessages.RECT rect;
            WindowMessages.GetClientRect(this.Handle, out rect);
            WindowMessages.POINT point = new WindowMessages.POINT { X = rect.Left, Y = rect.Top };
            WindowMessages.ClientToScreen(this.Handle, ref point);
            return new Rectangle(point.X, point.Y, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }
        #endregion

        #region Observer Pattern Implementation
        public void AddObserver(IWindowObserver observer) => observers.Add(observer);
        public void RemoveObserver(IWindowObserver observer) => observers.Remove(observer);
        public void NotifyObservers(WindowChangeType changeType)
        {
            foreach (var observer in observers)
            {
                observer.OnWindowChanged(this, changeType);
            }
        }
        #endregion

        #region Window Message Processing
        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WindowMessages.WM_NCHITTEST:
                    base.WndProc(ref m);
                    if (m.Result.ToInt32() == WindowMessages.HTCAPTION)
                    {
                        // マウスの位置を取得してカーソル更新とマークの処理を行う
                        Point screenPoint = new Point(
                            (int)(m.LParam.ToInt64() & 0xFFFF),
                            (int)((m.LParam.ToInt64() >> 16) & 0xFFFF)
                        );
                        Point clientPoint = this.PointToClient(screenPoint);
                        this.Invalidate(); // マークの再描画を要求

                        m.Result = (IntPtr)WindowMessages.HTCLIENT;
                    }
                    return;

                case WindowMessages.WM_NCLBUTTONDOWN:
                    if (m.WParam.ToInt32() == WindowMessages.HTCAPTION)
                    {
                        Point screenPoint = new Point(
                            (int)(m.LParam.ToInt64() & 0xFFFF),
                            (int)((m.LParam.ToInt64() >> 16) & 0xFFFF)
                        );
                        Point clientPoint = this.PointToClient(screenPoint);

                        // 親子関係の更新のためにWindowManagerに通知
                        WindowManager.Instance.BringWindowToFront(this);
                        WindowManager.Instance.CheckPotentialParentWindow(this);

                        // クリックイベントをシミュレート
                        Message newMsg = new Message
                        {
                            Msg = WindowMessages.WM_LBUTTONDOWN,
                            WParam = m.WParam,
                            LParam = (IntPtr)((clientPoint.Y << 16) | clientPoint.X)
                        };
                        this.Strategy.HandleWindowMessage(this, newMsg);
                        return;
                    }
                    break;

                case WindowMessages.WM_NCMOUSEMOVE:
                    // タイトルバー上でのマウス移動も処理
                    Point mousePoint = new Point(
                        (int)(m.LParam.ToInt64() & 0xFFFF),
                        (int)((m.LParam.ToInt64() >> 16) & 0xFFFF)
                    );
                    Point clientMousePoint = this.PointToClient(mousePoint);
                    Strategy.UpdateCursor(this, clientMousePoint);
                    this.Invalidate(); // マークの再描画を要求
                    break;
            }

            var result = WindowMessageHandler.HandleWindowMessage(this, m);
            if (!result.Handled)
            {
                base.WndProc(ref m);
            }
            else
            {
                m.Result = result.Result;
            }
        }
        private void UpdateMovableRegionForDescendants(GameWindow window)
        {
            // 現在のウィンドウの直接の子をチェック
            foreach (var child in window.Children)
            {
                // Playerの場合
                if (child is PlayerForm player)
                {
                    player.UpdateMovableRegion(WindowManager.Instance.CalculateMovableRegion(window));
                    return;
                }
                // 子ウィンドウの場合、その子孫も処理
                else if (child is GameWindow childWindow)
                {
                    UpdateMovableRegionForDescendants(childWindow);
                }
            }
        }
        public new void BringToFront()
        {
            WindowManager.Instance.BringWindowToFront(this);
        }
        #endregion
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                WindowManager.Instance.UnregisterFormOrder(this);
                foreach (var observer in observers.ToList())
                {
                    RemoveObserver(observer);
                }
            }
            base.Dispose(disposing);
        }
        public class SizeChangedEventArgs : EventArgs
        {
            public Size NewSize { get; }
            public SizeChangedEventArgs(Size newSize) => NewSize = newSize;
        }
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x8; // CS_DBLCLKS
                return cp;
            }
        }
        public bool IsResizable() => strategy is ResizableWindowStrategy;
    }
}