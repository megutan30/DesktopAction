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
        void UpdateCursor(GameWindow window, Point clientMousePos);
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
        public void UpdateCursor(GameWindow window, Point clientMousePos)
        {
            window.Cursor = Cursors.Default;
        }
    }

    public class ResizableWindowStrategy : IWindowStrategy
    {
        private bool isResizing = false;
        private Point lastMousePos;
        private Size originalSize;

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
            // リサイズ後の処理が必要な場合はここに実装します
            window.OnWindowResized();
        }

        public void HandleWindowMessage(GameWindow window, Message m)
        {
            switch (m.Msg)
            {
                case 0x0201: // WM_LBUTTONDOWN
                    isResizing = true;
                    lastMousePos = window.PointToClient(Cursor.Position);
                    originalSize = window.Size;
                    break;

                case 0x0202: // WM_LBUTTONUP
                    isResizing = false;
                    break;

                case 0x0200: // WM_MOUSEMOVE
                    if (isResizing)
                    {
                        Point currentMousePos = window.PointToClient(Cursor.Position);
                        int dx = currentMousePos.X - lastMousePos.X;
                        int dy = currentMousePos.Y - lastMousePos.Y;

                        Size newSize = new Size(originalSize.Width + dx, originalSize.Height + dy);
                        newSize.Width = Math.Max(newSize.Width, window.MinimumSize.Width);
                        newSize.Height = Math.Max(newSize.Height, window.MinimumSize.Height);

                        window.Size = newSize;
                        window.OnWindowResized();
                    }
                    break;
            }
        }

        public void UpdateCursor(GameWindow window, Point clientMousePos)
        {
            window.Cursor = Cursors.SizeNWSE;
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
        public void UpdateCursor(GameWindow window, Point clientMousePos)
        {
            // ウィンドウ全体で移動カーソルを表示
            window.Cursor = Cursors.SizeAll;
        }
        public void HandleResize(GameWindow window) { }
    }

    public class DeletableWindowStrategy : IWindowStrategy
    {
        public bool IsMinimized { get; private set; }

        public void Update(GameWindow window, float deltaTime)
        {
            // 既存の更新ロジック
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

        public void HandleMinimize(GameWindow window)
        {
            IsMinimized = true;
            // ここで最小化に関する追加の処理を行うことができます
        }

        public void HandleRestore(GameWindow window)
        {
            IsMinimized = false;
            // ここで復元に関する追加の処理を行うことができます
        }
        public void UpdateCursor(GameWindow window, Point clientMousePos)
        {
            window.Cursor = Cursors.Default;
        }
    }
}