using MultiWindowActionGame;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace MultiWindowActionGame
{
    public interface IWindowStrategy
    {
        void Update(GameWindow window, float deltaTime);
        void HandleInput(GameWindow window);
        void HandleResize(GameWindow window);
    }

    public class NormalWindowStrategy : IWindowStrategy
    {
        public void Update(GameWindow window, float deltaTime)
        {
            // 通常ウィンドウの更新ロジック（必要に応じて）
        }

        public void HandleInput(GameWindow window)
        {
            // 通常ウィンドウの入力処理（必要に応じて）
        }
        public void HandleResize(GameWindow window) { }
    }

    public class ResizableWindowStrategy : IWindowStrategy
    {
        private bool isResizing = false;
        private Point lastMousePos;
        private Rectangle originalBounds;
        private ResizeDirection currentResizeDirection;

        private const int ResizeBorderSize = 5;

        private enum ResizeDirection
        {
            None,
            Top,
            TopRight,
            Right,
            BottomRight,
            Bottom,
            BottomLeft,
            Left,
            TopLeft
        }

        public void Update(GameWindow window, float deltaTime)
        {
            // 通常の更新ロジック（必要に応じて）
        }

        public void HandleInput(GameWindow window)
        {
            // キーボード入力の処理（必要に応じて）
        }

        public void HandleResize(GameWindow window)
        {
            window.OnWindowResized();
        }

        public void HandleWindowMessage(GameWindow window, Message m)
        {
            switch (m.Msg)
            {
                case 0x0020: // WM_SETCURSOR
                    Point cursorPos = window.PointToClient(Cursor.Position);
                    ResizeDirection direction = GetResizeDirection(window, cursorPos);
                    SetCursor(direction);
                    m.Result = (IntPtr)1; // カーソル設定を処理したことを示す
                    return;

                case 0x0201: // WM_LBUTTONDOWN
                    isResizing = true;
                    lastMousePos = Cursor.Position;
                    originalBounds = window.Bounds;
                    currentResizeDirection = GetResizeDirection(window, window.PointToClient(lastMousePos));
                    window.Capture = true;
                    break;

                case 0x0202: // WM_LBUTTONUP
                    isResizing = false;
                    window.Capture = false;
                    break;

                case 0x0200: // WM_MOUSEMOVE
                    if (isResizing)
                    {
                        Point currentMousePos = Cursor.Position;
                        ResizeWindow(window, currentMousePos);
                        lastMousePos = currentMousePos;
                    }
                    break;
            }
        }

        private ResizeDirection GetResizeDirection(GameWindow window, Point mousePos)
        {
            bool top = mousePos.Y <= ResizeBorderSize;
            bool bottom = mousePos.Y >= window.ClientSize.Height - ResizeBorderSize;
            bool left = mousePos.X <= ResizeBorderSize;
            bool right = mousePos.X >= window.ClientSize.Width - ResizeBorderSize;

            if (top && left) return ResizeDirection.TopLeft;
            if (top && right) return ResizeDirection.TopRight;
            if (bottom && left) return ResizeDirection.BottomLeft;
            if (bottom && right) return ResizeDirection.BottomRight;
            if (top) return ResizeDirection.Top;
            if (bottom) return ResizeDirection.Bottom;
            if (left) return ResizeDirection.Left;
            if (right) return ResizeDirection.Right;

            return ResizeDirection.None;
        }

        private void SetCursor(ResizeDirection direction)
        {
            switch (direction)
            {
                case ResizeDirection.Top:
                case ResizeDirection.Bottom:
                    Cursor.Current = Cursors.SizeNS;
                    break;
                case ResizeDirection.Left:
                case ResizeDirection.Right:
                    Cursor.Current = Cursors.SizeWE;
                    break;
                case ResizeDirection.TopLeft:
                case ResizeDirection.BottomRight:
                    Cursor.Current = Cursors.SizeNWSE;
                    break;
                case ResizeDirection.TopRight:
                case ResizeDirection.BottomLeft:
                    Cursor.Current = Cursors.SizeNESW;
                    break;
                default:
                    Cursor.Current = Cursors.Default;
                    break;
            }
        }

        private void ResizeWindow(GameWindow window, Point currentMousePos)
        {
            int dx = currentMousePos.X - lastMousePos.X;
            int dy = currentMousePos.Y - lastMousePos.Y;

            Rectangle newBounds = window.Bounds;

            switch (currentResizeDirection)
            {
                case ResizeDirection.Top:
                    newBounds.Y += dy;
                    newBounds.Height -= dy;
                    break;
                case ResizeDirection.TopRight:
                    newBounds.Y += dy;
                    newBounds.Height -= dy;
                    newBounds.Width += dx;
                    break;
                case ResizeDirection.Right:
                    newBounds.Width += dx;
                    break;
                case ResizeDirection.BottomRight:
                    newBounds.Width += dx;
                    newBounds.Height += dy;
                    break;
                case ResizeDirection.Bottom:
                    newBounds.Height += dy;
                    break;
                case ResizeDirection.BottomLeft:
                    newBounds.X += dx;
                    newBounds.Width -= dx;
                    newBounds.Height += dy;
                    break;
                case ResizeDirection.Left:
                    newBounds.X += dx;
                    newBounds.Width -= dx;
                    break;
                case ResizeDirection.TopLeft:
                    newBounds.X += dx;
                    newBounds.Y += dy;
                    newBounds.Width -= dx;
                    newBounds.Height -= dy;
                    break;
            }

            // 最小サイズの制約を適用
            if (newBounds.Width < window.MinimumSize.Width)
            {
                if (currentResizeDirection == ResizeDirection.Left ||
                    currentResizeDirection == ResizeDirection.TopLeft ||
                    currentResizeDirection == ResizeDirection.BottomLeft)
                {
                    newBounds.X = window.Bounds.Right - window.MinimumSize.Width;
                }
                newBounds.Width = window.MinimumSize.Width;
            }
            if (newBounds.Height < window.MinimumSize.Height)
            {
                if (currentResizeDirection == ResizeDirection.Top ||
                    currentResizeDirection == ResizeDirection.TopLeft ||
                    currentResizeDirection == ResizeDirection.TopRight)
                {
                    newBounds.Y = window.Bounds.Bottom - window.MinimumSize.Height;
                }
                newBounds.Height = window.MinimumSize.Height;
            }

            window.Bounds = newBounds;
            window.OnWindowResized();
        }
    }

    public class MovableWindowStrategy : IWindowStrategy
    {
        private bool isDragging = false;
        private Point lastMousePos;
        private const int WM_NCHITTEST = 0x84;
        private const int HTCAPTION = 2;
        public void Update(GameWindow window, float deltaTime)
        {
            // 更新ロジック（必要に応じて）
        }

        public void HandleInput(GameWindow window)
        {
            // キーボード入力の処理（必要に応じて）
        }

        public void HandleWindowMessage(GameWindow window, Message m)
        {
            switch (m.Msg)
            {
                case WM_NCHITTEST:
                    m.Result = (IntPtr)HTCAPTION;
                    break;

                case 0x0201: // WM_LBUTTONDOWN
                    isDragging = true;
                    lastMousePos = window.PointToClient(Cursor.Position);
                    break;

                case 0x0202: // WM_LBUTTONUP
                    isDragging = false;
                    break;

                case 0x0200: // WM_MOUSEMOVE
                    if (isDragging)
                    {
                        Point currentMousePos = window.PointToClient(Cursor.Position);
                        int dx = currentMousePos.X - lastMousePos.X;
                        int dy = currentMousePos.Y - lastMousePos.Y;
                        window.Location = new Point(window.Location.X + dx, window.Location.Y + dy);
                        window.OnWindowMoved();

                        // プレイヤーの位置を更新
                        Player? player = WindowManager.Instance.GetPlayerInWindow(window);
                        if (player != null)
                        {
                            player.ConstrainToWindow(window);
                        }
                    }
                    break;
            }
        }
        public void HandleResize(GameWindow window) { }
    }

    public class DeletableWindowStrategy : IWindowStrategy
    {
        public void Update(GameWindow window, float deltaTime)
        {
            // 削除可能ウィンドウの更新ロジック（必要に応じて）
        }

        public void HandleInput(GameWindow window)
        {
            if (Input.IsKeyDown(Keys.Delete))
            {
                window.Close();
                window.NotifyObservers(WindowChangeType.Deleted);
            }
        }
        public void HandleResize(GameWindow window) { }
    }
}