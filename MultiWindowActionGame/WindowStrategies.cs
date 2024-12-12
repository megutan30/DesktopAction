using MultiWindowActionGame;
using System;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;

namespace MultiWindowActionGame
{
    public interface IWindowStrategy
    {
        void Update(GameWindow window, float deltaTime);
        void HandleInput(GameWindow window);
        void HandleResize(GameWindow window);
        void HandleWindowMessage(GameWindow window, Message m);
        void UpdateCursor(GameWindow window, Point clientMousePos);
    }

    public class NormalWindowStrategy : IWindowStrategy
    {
        public void Update(GameWindow window, float deltaTime) { }
        public void HandleInput(GameWindow window) { }
        public void HandleResize(GameWindow window) { }
        public void HandleWindowMessage(GameWindow window, Message m) { }
        public void UpdateCursor(GameWindow window, Point clientMousePos)
        {
            window.Cursor = Cursors.Default;
        }
    }

    public class ResizableWindowStrategy : IWindowStrategy
    {
        private readonly ResizeEffect resizeEffect = new ResizeEffect();
        private bool isResizing = false;
        private Point lastMousePos;
        private Size originalSize;

        public void Update(GameWindow window, float deltaTime)
        {
            if (isResizing)
            {
                ApplyResizeEffect(window);
            }
        }
        public void HandleInput(GameWindow window) { }

        public void HandleResize(GameWindow window)
        {
            // リサイズ後の処理が必要な場合はここに実装します
            //window.OnWindowResized()
        }

        public void HandleWindowMessage(GameWindow window, Message m)
        {
            switch (m.Msg)
            {
                case 0x0201: // WM_LBUTTONDOWN
                    StartResizing(window);
                    break;

                case 0x0202: // WM_LBUTTONUP
                    StopResizing();
                    break;
            }
        }
        private void StartResizing(GameWindow window)
        {
            isResizing = true;
            lastMousePos = window.PointToClient(Cursor.Position);
            originalSize = window.CollisionBounds.Size;
        }

        private void StopResizing()
        {
            isResizing = false;
            resizeEffect.ResetAll();
        }
        private void ApplyResizeEffect(GameWindow window)
        {
            Point currentMousePos = window.PointToClient(Cursor.Position);
            Size newSize = CalculateNewSize(window, currentMousePos);

            // このウィンドウのスケールを計算
            SizeF scale = new SizeF(
                (float)newSize.Width / originalSize.Width,
                (float)newSize.Height / originalSize.Height
            );
            // スケールの適用前にバリデーション
            bool isValidResize = true;
            Rectangle proposedBounds = new Rectangle(
                window.CollisionBounds.Location,
                newSize
            );
            // 最上位のウィンドウから子孫すべてにスケールを設定
            if (isValidResize)
            {
                ApplyScaleToWindowAndDescendants(window, scale);
                window.ApplyEffect(resizeEffect);
            }
        }

        private void ApplyScaleToWindowAndDescendants(GameWindow window, SizeF scale)
        {
            // まず現在のウィンドウにスケールを設定
            resizeEffect.UpdateScale(window, scale);


            // 直接の子に対してスケールを設定
            foreach (var child in window.Children)
            {
                resizeEffect.UpdateScale(child, scale);

                // 子がウィンドウの場合、その子孫にも再帰的にスケールを設定
                if (child is GameWindow childWindow)
                {
                    ApplyScaleToWindowAndDescendants(childWindow, scale);
                }
            }
        }

        private Size CalculateNewSize(GameWindow window, Point currentMousePos)
        {
            int dx = currentMousePos.X - lastMousePos.X;
            int dy = currentMousePos.Y - lastMousePos.Y;

            Size proposedSize = new Size(
                Math.Max(originalSize.Width + dx, window.MinimumSize.Width),
                Math.Max(originalSize.Height + dy, window.MinimumSize.Height)
            );

            // 不可侵領域を考慮した有効なサイズを取得
            return NoEntryZoneManager.Instance.GetValidSize(
                window.CollisionBounds,
                proposedSize
            );
        }
        public void UpdateCursor(GameWindow window, Point clientMousePos)
        {
            window.Cursor = Cursors.SizeNWSE;
        }
    }
    public class MovableWindowStrategy : IWindowStrategy
    {
        private readonly MovementEffect movementEffect = new MovementEffect();
        private bool isDragging = false;
        private Point lastMousePos;
        private const int WM_NCHITTEST = 0x84;
        private const int HTCAPTION = 2;
        public MovementEffect MovementEffect => movementEffect;
        public void Update(GameWindow window, float deltaTime)
        {
            if (isDragging)
            {
                ApplyMovementEffect(window);
            }
        }

        public void HandleInput(GameWindow window) { }
        public void HandleResize(GameWindow window) { }
        public void HandleWindowMessage(GameWindow window, Message m)
        {
            switch (m.Msg)
            {
                case WM_NCHITTEST:
                    m.Result = (IntPtr)HTCAPTION;
                    break;

                case 0x0201: // WM_LBUTTONDOWN
                    StartDragging(window);
                    break;

                case 0x0202: // WM_LBUTTONUP
                    StopDragging();
                    break;
            }
        }
        private void StartDragging(GameWindow window)
        {
            isDragging = true;
            lastMousePos = window.PointToClient(Cursor.Position);
        }

        private void StopDragging()
        {
            isDragging = false;
            movementEffect.UpdateMovement(Vector2.Zero);
        }
        private void ApplyMovementEffect(GameWindow window)
        {
            Point currentMousePos = window.PointToClient(Cursor.Position);
            Vector2 movement = new Vector2(
                currentMousePos.X - lastMousePos.X,
                currentMousePos.Y - lastMousePos.Y
            );

            // 移動先の位置をチェック
            Rectangle proposedBounds = new Rectangle(
                window.CollisionBounds.X + (int)movement.X,
                window.CollisionBounds.Y + (int)movement.Y,
                window.CollisionBounds.Width,
                window.CollisionBounds.Height
            );

            // 不可侵領域を考慮した有効な位置を取得
            Rectangle validBounds = NoEntryZoneManager.Instance.GetValidPosition(
                window.ClientBounds,
                proposedBounds
            );

            // 有効な位置に基づいて移動量を調整
            movement = new Vector2(
                validBounds.X - window.CollisionBounds.X,
                validBounds.Y - window.CollisionBounds.Y
            );

            movementEffect.UpdateMovement(movement);
            window.ApplyEffect(movementEffect);
        }

        public void UpdateCursor(GameWindow window, Point clientMousePos)
        {
            window.Cursor = Cursors.SizeAll;
        }
    }
    public class DeletableWindowStrategy : IWindowStrategy
    {
        public bool IsMinimized { get; private set; }

        public void Update(GameWindow window, float deltaTime) { }

        public void HandleInput(GameWindow window)
        {
            if (Input.IsKeyDown(Keys.Delete))
            {
                // 削除前に子要素をすべて解放
                foreach (var child in window.Children.ToList())
                {
                    window.RemoveChild(child);
                }

                // 親からも削除
                window.Parent?.RemoveChild(window);

                window.Close();
                window.NotifyObservers(WindowChangeType.Deleted);
            }
        }

        public void HandleResize(GameWindow window) { }
        public void HandleWindowMessage(GameWindow window, Message m) { }

        public void HandleMinimize(GameWindow window)
        {
            IsMinimized = true;
        }

        public void HandleRestore(GameWindow window)
        {
            IsMinimized = false;
        }

        public void UpdateCursor(GameWindow window, Point clientMousePos)
        {
            window.Cursor = Cursors.Default;
        }
    }
    public class MinimizableWindowStrategy : IWindowStrategy
    {
        private readonly MinimizeEffect minimizeEffect = new MinimizeEffect();

        public void HandleMinimize(GameWindow window)
        {
            window.OnMinimize();
        }

        public void HandleRestore(GameWindow window)
        {
            window.OnRestore();
        }

        public void Update(GameWindow window, float deltaTime) { }
        public void HandleInput(GameWindow window) { }
        public void HandleResize(GameWindow window) { }
        public void HandleWindowMessage(GameWindow window, Message m) { }
        public void UpdateCursor(GameWindow window, Point clientMousePos)
        {
            window.Cursor = Cursors.Default;
        }
    }
    public class TextDisplayWindowStrategy : IWindowStrategy
    {
        private string displayText;
        private float fontSizeRatio = 0.2f;

        public TextDisplayWindowStrategy(string text)
        {
            displayText = text;
        }

        public void Update(GameWindow window, float deltaTime) { }

        public void HandleInput(GameWindow window) { }

        public void HandleResize(GameWindow window) { window.Invalidate(); }

        public void HandleWindowMessage(GameWindow window, Message m) { }

        public void UpdateCursor(GameWindow window, Point clientMousePos)
        {
            window.Cursor = Cursors.Default;
        }

        public string GetDisplayText()
        {
            return displayText;
        }
    }
}