using MultiWindowActionGame;
using System;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;
using static MultiWindowActionGame.GameSettings;

namespace MultiWindowActionGame
{
    public interface IWindowStrategy
    {
        void Update(GameWindow window, float deltaTime);
        void HandleInput(GameWindow window);
        void HandleResize(GameWindow window);
        void HandleWindowMessage(GameWindow window, Message m);
        void UpdateCursor(GameWindow window, Point clientMousePos);
        void DrawStrategyMark(Graphics g, Rectangle bounds, bool isHovered);
    }

    public class NormalWindowStrategy : IWindowStrategy
    {
        public void Update(GameWindow window, float deltaTime) { }
        public void HandleInput(GameWindow window) { }
        public void HandleResize(GameWindow window) { }
        public void HandleWindowMessage(GameWindow window, Message m) { }
        public void DrawStrategyMark(Graphics g, Rectangle bounds, bool isHovered) { }
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
        private bool isInitialized = false;
        private readonly WindowSettings settings;
        public ResizableWindowStrategy()
        {
            settings = GameSettings.Instance.Window;
        }

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

            // 現在のサイズと同じ場合は何もしない
            if (newSize == window.Size)
            {
                return;
            }
            // このウィンドウのスケールを計算
            SizeF scale = new SizeF(
                (float)newSize.Width / originalSize.Width,
                (float)newSize.Height / originalSize.Height
            );

            // 実際のリサイズ前に境界チェック
            Rectangle proposedBounds = new Rectangle(
                window.CollisionBounds.Location,
                newSize
            );
            if (!NoEntryZoneManager.Instance.IntersectsWithAnyZone(proposedBounds))
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

            // originalSizeを基準にした新しいサイズを計算
            Size proposedSize = new Size(
                Math.Max(originalSize.Width + dx, settings.MinimumSize.Width),
                Math.Max(originalSize.Height + dy, settings.MinimumSize.Height)
            );

            // 不可侵領域との衝突をチェックする前のサイズを保存
            Size newSize = proposedSize;

            // 不可侵領域を考慮した有効なサイズを取得
            newSize = NoEntryZoneManager.Instance.GetValidSize(
                new Rectangle(window.CollisionBounds.Location, originalSize),  // 開始時の大きさを基準に判定
                newSize
            );

            return newSize;
        }
        public void DrawStrategyMark(Graphics g, Rectangle bounds, bool isHovered)
        {
            // マークの大きさと位置を計算
            int markSize = 60;
            int x = bounds.X + (bounds.Width - markSize) / 2;
            int y = bounds.Y + (bounds.Height - markSize) / 2;

            using (var pen = new Pen(isHovered ? Color.White : Color.FromArgb(128, 128, 128), 2))
            {
                // 中心から右下への線
                g.DrawLine(pen, x + markSize / 4, y + markSize / 4, x + markSize * 3 / 4, y + markSize * 3 / 4);

                // 左上矢印
                g.DrawLine(pen, x + markSize / 4, y + markSize / 4, x + markSize / 2, y + markSize / 4);
                g.DrawLine(pen, x + markSize / 4, y + markSize / 4, x + markSize / 4, y + markSize / 2);

                // 右下矢印
                g.DrawLine(pen, x + markSize * 3 / 4, y + markSize * 3 / 4, x + markSize / 2, y + markSize * 3 / 4);
                g.DrawLine(pen, x + markSize * 3 / 4, y + markSize * 3 / 4, x + markSize * 3 / 4, y + markSize / 2);
            }
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
        private bool isBlockedRight = false;  // 右方向への移動が制限されているか
        private bool isBlockedLeft = false;   // 左方向への移動が制限されているか
        private bool isBlockedDown = false;   // 下方向への移動が制限されているか
        private bool isBlockedUp = false;     // 上方向への移動が制限されているか
        private Point lastValidPosition;  // 最後の有効な位置
        public MovementEffect MovementEffect => movementEffect;
        private readonly WindowSettings settings;
        public MovableWindowStrategy()
        {
            settings = GameSettings.Instance.Window;
        }

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
            ResetBlockFlags();
            lastMousePos = window.PointToClient(Cursor.Position);
            lastValidPosition = window.Location;
        }
        private void ResetBlockFlags()
        {
            isBlockedRight = false;
            isBlockedLeft = false;
            isBlockedDown = false;
            isBlockedUp = false;
        }

        private void StopDragging()
        {
            isDragging = false;
            ResetBlockFlags();
            movementEffect.UpdateMovement(Vector2.Zero);
        }

        private void ApplyMovementEffect(GameWindow window)
        {
            Point currentMousePos = window.PointToClient(Cursor.Position);
            int deltaX = currentMousePos.X - lastMousePos.X;
            int deltaY = currentMousePos.Y - lastMousePos.Y;

            // 現在の位置で不可侵領域との接触をチェック
            Rectangle currentBounds = window.CollisionBounds;
            Rectangle rightCheck = new Rectangle(
                currentBounds.X + 1, currentBounds.Y,
                currentBounds.Width, currentBounds.Height
            );
            Rectangle leftCheck = new Rectangle(
                currentBounds.X - 1, currentBounds.Y,
                currentBounds.Width, currentBounds.Height
            );
            Rectangle downCheck = new Rectangle(
                currentBounds.X, currentBounds.Y + 1,
                currentBounds.Width, currentBounds.Height
            );
            Rectangle upCheck = new Rectangle(
                currentBounds.X, currentBounds.Y - 1,
                currentBounds.Width, currentBounds.Height
            );

            // 各方向の接触状態を更新
            isBlockedRight = NoEntryZoneManager.Instance.IntersectsWithAnyZone(rightCheck);
            isBlockedLeft = NoEntryZoneManager.Instance.IntersectsWithAnyZone(leftCheck);
            isBlockedDown = NoEntryZoneManager.Instance.IntersectsWithAnyZone(downCheck);
            isBlockedUp = NoEntryZoneManager.Instance.IntersectsWithAnyZone(upCheck);

            // 移動方向に基づいてブロックをチェック
            Vector2 movement = new Vector2(
                (deltaX > 0 && isBlockedRight) || (deltaX < 0 && isBlockedLeft) ? 0 : deltaX,
                (deltaY > 0 && isBlockedDown) || (deltaY < 0 && isBlockedUp) ? 0 : deltaY
            );

            Rectangle proposedBounds = new Rectangle(
                window.CollisionBounds.X + (int)movement.X,
                window.CollisionBounds.Y + (int)movement.Y,
                window.CollisionBounds.Width,
                window.CollisionBounds.Height
            );

            Rectangle validBounds = NoEntryZoneManager.Instance.GetValidPosition(
                window.CollisionBounds,
                proposedBounds
            );

            movement = new Vector2(
                validBounds.X - window.CollisionBounds.X,
                validBounds.Y - window.CollisionBounds.Y
            );

            if (movement != Vector2.Zero)
            {
                lastValidPosition = new Point(
                    lastValidPosition.X + (int)movement.X,
                    lastValidPosition.Y + (int)movement.Y
                );
            }

            movementEffect.UpdateMovement(movement);
            window.ApplyEffect(movementEffect);
        }
        public void DrawStrategyMark(Graphics g, Rectangle bounds, bool isHovered)
        {
            int markSize = 60;
            int x = bounds.X + (bounds.Width - markSize) / 2;
            int y = bounds.Y + (bounds.Height - markSize) / 2;

            using (var pen = new Pen(isHovered ? Color.White : Color.FromArgb(128, 128, 128), 2))
            {
                // 中心の十字
                g.DrawLine(pen, x + markSize / 2, y, x + markSize / 2, y + markSize);  // 縦線
                g.DrawLine(pen, x, y + markSize / 2, x + markSize, y + markSize / 2);  // 横線

                // 上矢印の傘
                g.DrawLine(pen, x + markSize / 2, y, x + markSize / 3, y + markSize / 5);
                g.DrawLine(pen, x + markSize / 2, y, x + markSize * 2 / 3, y + markSize / 5);

                // 下矢印の傘
                g.DrawLine(pen, x + markSize / 2, y + markSize, x + markSize / 3, y + markSize * 4 / 5);
                g.DrawLine(pen, x + markSize / 2, y + markSize, x + markSize * 2 / 3, y + markSize * 4 / 5);

                // 左矢印の傘
                g.DrawLine(pen, x, y + markSize / 2, x + markSize / 5, y + markSize / 3);
                g.DrawLine(pen, x, y + markSize / 2, x + markSize / 5, y + markSize * 2 / 3);

                // 右矢印の傘
                g.DrawLine(pen, x + markSize, y + markSize / 2, x + markSize * 4 / 5, y + markSize / 3);
                g.DrawLine(pen, x + markSize, y + markSize / 2, x + markSize * 4 / 5, y + markSize * 2 / 3);
            }
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
        public void DrawStrategyMark(Graphics g, Rectangle bounds, bool isHovered) { }
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
        public void HandleWindowMessage(GameWindow window, Message m)
        {
            switch (m.Msg)
            {
                case 0x0201: // WM_LBUTTONDOWN
                    window.OnMinimize();
                    break;
            }
        }
        public void DrawStrategyMark(Graphics g, Rectangle bounds, bool isHovered)
        {
            // マークの大きさと位置を計算
            int markSize = 60;
            int x = bounds.X + (bounds.Width - markSize) / 2;
            int y = bounds.Y + (bounds.Height - markSize) / 2;

            // マークの色を設定（ホバー時は白、通常時はグレー）
            Color markColor = isHovered ? Color.White : Color.FromArgb(128, 128, 128);

            // 最小化ボタンを描画
            using (var brush = new SolidBrush(markColor))
            {
                g.FillRectangle(brush, x, y, markSize, markSize / 6);
            }
        }
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
        public void DrawStrategyMark(Graphics g, Rectangle bounds, bool isHovered) { }

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