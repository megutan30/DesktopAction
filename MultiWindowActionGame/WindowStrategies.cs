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
        private float scaleFactor = 1.0f;
        private const float ScaleSpeed = 0.5f;

        public void Update(GameWindow window, float deltaTime)
        {
            window.Size = new Size(
                (int)(window.OriginalSize.Width * scaleFactor),
                (int)(window.OriginalSize.Height * scaleFactor)
            );
        }
        public void HandleInput(GameWindow window)
        {
            // 通常ウィンドウの入力処理（必要に応じて）
        }

        public void HandleResize(GameWindow window)
        {
            // リサイズ後の処理
            Console.WriteLine($"Window {window.Id} resized to {window.Size}");

            // プレイヤーの位置を調整する（必要に応じて）
            Player? player = WindowManager.Instance.GetPlayerInWindow(window);
            if (player != null)
            {
                player.ConstrainToWindow(window);
            }
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