using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MultiWindowActionGame
{
    public class GameWindow : Form, IWindowComponent, IWindowSubject
    {
        public Rectangle ClientBounds { get; private set; }
        public Rectangle AdjustedBounds { get; private set; }
        public bool CanEnter { get; set; } = true;
        public bool CanExit { get; set; } = true;
        public Size OriginalSize { get; private set; }

        private new const int Margin = 3;
        private IWindowStrategy strategy;
        private List<IWindowObserver> observers = new List<IWindowObserver>();
        public Guid Id { get; } = Guid.NewGuid();
        public event EventHandler<EventArgs> WindowMoved;

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


        public GameWindow(Point location, Size size, IWindowStrategy strategy)
        {
            this.strategy = strategy;
            this.OriginalSize = size;

            this.FormBorderStyle = FormBorderStyle.Sizable; // これを変更
            this.StartPosition = FormStartPosition.Manual;
            this.Location = location;
            this.Size = size;
            this.TopMost = true;

            UpdateBounds();

            this.Move += GameWindow_Move;
            this.Resize += GameWindow_Resize;

            Console.WriteLine($"Created window with ID: {Id}, Location: {Location}, Size: {Size}");
            this.Show();
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
            UpdateBounds(); // 毎フレームバウンドを更新
        }

        public void Draw(Graphics g)
        {
            g.DrawRectangle(Pens.Black, Margin, Margin, this.ClientSize.Width - (2 * Margin) - 1, this.ClientSize.Height - (2 * Margin) - 1);
            g.DrawString($"Window ID: {Id}", this.Font, Brushes.Black, 10, 10);
            g.DrawString($"Type: {strategy.GetType().Name}", this.Font, Brushes.Black, 10, 30);
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

        protected override void WndProc(ref Message m)
        {
            if (strategy is MovableWindowStrategy movableStrategy)
            {
                movableStrategy.HandleWindowMessage(this, m);
            }
            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Draw(e.Graphics);
        }
    }
}