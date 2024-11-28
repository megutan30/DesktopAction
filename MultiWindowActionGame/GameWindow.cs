﻿using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MultiWindowActionGame
{
    public class GameWindow : Form, IWindowSubject, IEffectTarget
    {
        public Rectangle ClientBounds { get; private set; }
        public Rectangle AdjustedBounds { get; private set; }
        public bool CanEnter { get; set; } = true;
        public bool CanExit { get; set; } = true;
        public Size OriginalSize { get; private set; }
        public IWindowStrategy Strategy { get; private set; }
        public Rectangle Bounds => AdjustedBounds;
        public GameWindow? Parent { get; private set; }
        public ICollection<IEffectTarget> Children { get; } = new HashSet<IEffectTarget>();

        private new const int Margin = 0;
        protected IWindowStrategy strategy;
        private List<IWindowObserver> observers = new List<IWindowObserver>();
        private readonly List<IWindowEffect> effects = new();
        public Guid Id { get; } = Guid.NewGuid();

        public event EventHandler<EventArgs> WindowMoved;
        public event EventHandler<SizeChangedEventArgs> WindowResized;
        private bool isMoving = false;
        private bool isResizing = false;
        private bool isDragging = false;
        private bool shouldBringToFront = false;

        #region Win32 API Constants and Imports
        private const int WM_SYSCOMMAND = 0x0112;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int SC_CLOSE = 0xF060;
        private const int SC_MINIMIZE = 0xF020;
        private const int SC_MAXIMIZE = 0xF030;
        private const int SC_RESTORE = 0xF120;
        private const int HWND_TOP = 0;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int SC_MOVE = 0xF010;
        private const int HTCAPTION = 2;
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE = 3;

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            int hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags
        );

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
        #endregion

        public GameWindow(Point location, Size size, IWindowStrategy strategy)
        {
            this.strategy = strategy;
            this.Strategy = strategy;
            this.OriginalSize = size;
            this.MinimumSize = new Size(100, 100);
            
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
        public void AddChild(IEffectTarget child)
        {
            Children.Add(child);
            if (child is GameWindow window)
            {
                window.Parent = this;
            }
        }

        public void RemoveChild(IEffectTarget child)
        {
            if (Children.Remove(child))
            {
                if (child is GameWindow window)
                {
                    window.Parent = null;
                }
            }
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
        }
        #endregion

        #region IUpdatable and IDrawable Implementation
        public async Task UpdateAsync(float deltaTime)
        {
            strategy.Update(this, deltaTime);
            strategy.HandleInput(this);
            UpdateBounds();
        }

        public void Draw(Graphics g)
        {
            if (MainGame.IsDebugMode)
            {
                DrawDebugInfo(g);
            }
            else
            {
                g.DrawString($"Window ID: {Id}", this.Font, Brushes.Black, 10, 10);
                g.DrawString($"Type: {strategy.GetType().Name}", this.Font, Brushes.Black, 10, 30);
            }
        }

        private void DrawDebugInfo(Graphics g)
        {
            g.DrawString($"Window ID: {Id}", this.Font, Brushes.Black, 10, 10);
            g.DrawString($"Type: {strategy.GetType().Name}", this.Font, Brushes.Black, 10, 30);
            g.DrawString($"Children: {Children.Count}", this.Font, Brushes.Red, 10, 50);
            g.DrawString($"Parent: {Parent?.Id.ToString() ?? "None"}", this.Font, Brushes.Red, 10, 70);

            int y = 90;
            foreach (var effect in effects)
            {
                g.DrawString($"Effect: {effect.Type} Active: {effect.IsActive}",
                    this.Font, Brushes.Blue, 10, y);
                y += 20;
            }
        }
        #endregion

        #region Window Event Handlers
        private void GameWindow_Load(object sender, EventArgs e)
        {
            IntPtr hMenu = GetSystemMenu(this.Handle, false);
            EnableMenuItem(hMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
        }

        private void GameWindow_Move(object? sender, EventArgs e)
        {
            UpdateBounds();
            WindowMoved?.Invoke(this, EventArgs.Empty);
            NotifyObservers(WindowChangeType.Moved);
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

        private Rectangle GetClientRectangle()
        {
            RECT rect;
            GetClientRect(this.Handle, out rect);
            POINT point = new POINT { X = rect.Left, Y = rect.Top };
            ClientToScreen(this.Handle, ref point);
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
                case WM_MOUSEACTIVATE:
                    m.Result = (IntPtr)MA_NOACTIVATE;
                    return;
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
                    if (m.WParam.ToInt32() == HTCAPTION) return;  // タイトルバーでのクリックを無視
                    break;
                case WM_SYSCOMMAND:
                    int command = m.WParam.ToInt32() & 0xFFF0;
                    if (command == SC_CLOSE) return;
                    if (command == SC_MINIMIZE)
                        (Strategy as DeletableWindowStrategy)?.HandleMinimize(this);
                    else if (command == SC_RESTORE)
                        (Strategy as DeletableWindowStrategy)?.HandleRestore(this);
                    break;
                case 0x0201: // WM_LBUTTONDOWN
                    shouldBringToFront = true;
                    isDragging = true;
                    break;

                case 0x0202: // WM_LBUTTONUP
                    if (shouldBringToFront)
                    {
                        WindowManager.Instance.CheckPotentialParentWindow(this);
                        WindowManager.Instance.HandleWindowActivation(this);
                        WindowManager.Instance.CheckPotentialParentWindow(this);
                        shouldBringToFront = false;
                    }

                    isDragging = false;
                    break;

                case 0x0231: // WM_ENTERSIZEMOVE
                    if (Strategy is MovableWindowStrategy) isMoving = true;
                    else if (Strategy is ResizableWindowStrategy) isResizing = true;
                    break;

                case 0x0232: // WM_EXITSIZEMOVE
                    isMoving = isResizing = false;
                    break;
                case 0x0200: // WM_MOUSEMOVE
                    Strategy.UpdateCursor(this, PointToClient(Cursor.Position));
                    if (!isDragging) return;
                    foreach (var child in Children)
                    {
                        if (child is Player player)
                        {
                            player.UpdateMovableRegion(WindowManager.Instance.CalculateMovableRegion(this));
                        }
                    }
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

        public new void BringToFront()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SetWindowPos(this.Handle, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE)));
            }
            else
            {
                SetWindowPos(this.Handle, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
            }
        }
        #endregion

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