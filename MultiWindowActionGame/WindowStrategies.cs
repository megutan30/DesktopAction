using System;
using System.Drawing;
using System.Windows.Forms;

namespace MultiWindowActionGame
{
    public interface IWindowStrategy
    {
        void Update(GameWindow window, float deltaTime);
        void HandleInput(GameWindow window);
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
            if (Input.IsKeyDown(Keys.Add))
            {
                scaleFactor += ScaleSpeed * GameTime.DeltaTime;
            }
            else if (Input.IsKeyDown(Keys.Subtract))
            {
                scaleFactor -= ScaleSpeed * GameTime.DeltaTime;
            }

            scaleFactor = Math.Max(0.5f, Math.Min(scaleFactor, 2.0f));
        }
    }

    public class MovableWindowStrategy : IWindowStrategy
    {
        private const float MoveSpeed = 100f;

        public void Update(GameWindow window, float deltaTime)
        {
            // 移動可能ウィンドウの更新ロジック（必要に応じて）
        }

        public void HandleInput(GameWindow window)
        {
            Point newLocation = window.Location;

            if (Input.IsKeyDown(Keys.Left))
                newLocation.X -= (int)(MoveSpeed * GameTime.DeltaTime);
            if (Input.IsKeyDown(Keys.Right))
                newLocation.X += (int)(MoveSpeed * GameTime.DeltaTime);
            if (Input.IsKeyDown(Keys.Up))
                newLocation.Y -= (int)(MoveSpeed * GameTime.DeltaTime);
            if (Input.IsKeyDown(Keys.Down))
                newLocation.Y += (int)(MoveSpeed * GameTime.DeltaTime);

            window.Location = newLocation;
        }
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
    }
}