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
    public abstract class BaseWindowStrategy : IWindowStrategy
    {
        // 共通のフィールド
        protected bool isActive = false;
        protected readonly WindowSettings settings;
        protected BaseWindowStrategy()
        {
            settings = GameSettings.Instance.Window;
        }

        // 基本実装を提供するメソッド
        public virtual void Update(GameWindow window, float deltaTime) { }
        public virtual void HandleInput(GameWindow window) { }
        public virtual void HandleResize(GameWindow window)
        {
            window.Invalidate();
        }
        public virtual void HandleWindowMessage(GameWindow window, Message m)
        {
            switch (m.Msg)
            {
                case WindowMessages.WM_LBUTTONDOWN:
                    OnMouseDown(window);
                    break;
                case WindowMessages.WM_LBUTTONUP:
                    OnMouseUp(window);
                    break;
                case WindowMessages.WM_MOUSEMOVE:
                    OnMouseMove(window);
                    break;
            }
        }

        // 新しい共通メソッド
        protected virtual void OnMouseDown(GameWindow window) { }
        protected virtual void OnMouseUp(GameWindow window) { }
        protected virtual void OnMouseMove(GameWindow window) { }

        // カーソル管理の共通実装
        public virtual void UpdateCursor(GameWindow window, Point clientMousePos)
        {
            window.Cursor = GetStrategyCursor();
        }

        // 各ストラテジーで実装が必要なメソッド
        public abstract void DrawStrategyMark(Graphics g, Rectangle bounds, bool isHovered);
        protected abstract Cursor GetStrategyCursor();
    }
    public static class StrategyMarkUtility
    {
        public const int DEFAULT_MARK_SIZE = 60;

        public static void DrawMarkBackground(Graphics g, Rectangle bounds, int markSize, Color color)
        {
            int x = bounds.X + (bounds.Width - markSize) / 2;
            int y = bounds.Y + (bounds.Height - markSize) / 2;

            using (var brush = new SolidBrush(Color.FromArgb(50, color)))
            {
                g.FillRectangle(brush, x, y, markSize, markSize);
            }
        }

        public static Point GetMarkCenter(Rectangle bounds, int markSize)
        {
            return new Point(
                bounds.X + (bounds.Width - markSize) / 2,
                bounds.Y + (bounds.Height - markSize) / 2
            );
        }

        public static Color GetMarkColor(bool isHovered)
        {
            return isHovered ? Color.White : Color.FromArgb(128, 128, 128);
        }

        public static void DrawArrowHead(Graphics g, Pen pen, Point start, Point end, int headSize = 10)
        {
            float angle = (float)Math.Atan2(end.Y - start.Y, end.X - start.X);
            float arrowAngle = (float)(Math.PI / 6); // 30度

            PointF p1 = new PointF(
                end.X - headSize * (float)Math.Cos(angle + arrowAngle),
                end.Y - headSize * (float)Math.Sin(angle + arrowAngle)
            );

            PointF p2 = new PointF(
                end.X - headSize * (float)Math.Cos(angle - arrowAngle),
                end.Y - headSize * (float)Math.Sin(angle - arrowAngle)
            );

            g.DrawLine(pen, end, p1);
            g.DrawLine(pen, end, p2);
        }
    }
    public class NormalWindowStrategy : BaseWindowStrategy
    {
        public override void DrawStrategyMark(Graphics g, Rectangle bounds, bool isHovered) { }
        protected override Cursor GetStrategyCursor() => Cursors.Default;
    }
    public class ResizableWindowStrategy : BaseWindowStrategy
    {
        private readonly ResizeEffect resizeEffect;
        private bool isResizing = false;
        private Point lastMousePos;
        private Size originalSize;
        private readonly Dictionary<IEffectTarget, Size> originalSizes = new();
        private SizeF currentScale = new(1.0f, 1.0f);  // 現在のスケールを保持

        public ResizableWindowStrategy()
        {
            resizeEffect = new ResizeEffect();
            WindowEffectManager.Instance.AddEffect(resizeEffect);
        }

        protected override void OnMouseDown(GameWindow window)
        {
            StartResizing(window);
            window.Capture = true;
        }

        protected override void OnMouseUp(GameWindow window)
        {
            window.Capture = false;
            StopResizing();
        }

        public override void Update(GameWindow window, float deltaTime)
        {
            if (isResizing)
            {
                UpdateResize(window);
            }
        }

        private void UpdateResize(GameWindow window)
        {
            Point currentMousePos = window.PointToClient(Cursor.Position);
            Size newSize = CalculateNewSize(window, currentMousePos);

            if (newSize == window.Size) return;

            SizeF scale = new(
                (float)newSize.Width / originalSize.Width,
                (float)newSize.Height / originalSize.Height
            );

            Rectangle proposedBounds = new(
                window.CollisionBounds.Location,
                newSize
            );

            if (!NoEntryZoneManager.Instance.IntersectsWithAnyZone(proposedBounds))
            {
                var player = MainGame.GetPlayer();
                if (player != null)
                {
                    player.UpdateMovableRegion(WindowManager.Instance.CalculateMovableRegion(player.Parent));
                }

                ApplyScaleToHierarchy(window, scale);
                WindowEffectManager.Instance.ApplyEffects(window);
            }
        }

        private void ApplyScaleToHierarchy(GameWindow window, SizeF scale)
        {
            foreach (var child in window.Children)
            {
                if (!originalSizes.ContainsKey(child))
                {
                    // 新しい子要素の元のサイズを記録する際、現在のスケールで割り戻す
                    originalSizes[child] = new Size(
                        (int)(child.Bounds.Width / currentScale.Width),
                        (int)(child.Bounds.Height / currentScale.Height)
                    );
                }
            }

            currentScale = scale;  // 現在のスケールを更新
            resizeEffect.UpdateScale(window, scale, originalSize);

            foreach (var child in window.Children)
            {
                var childOriginalSize = originalSizes[child];
                resizeEffect.UpdateScale(child, scale, childOriginalSize);
            }
        }

        private Size CalculateNewSize(GameWindow window, Point currentMousePos)
        {
            int dx = currentMousePos.X - lastMousePos.X;
            int dy = currentMousePos.Y - lastMousePos.Y;

            Size proposedSize = new(
                Math.Max(originalSize.Width + dx, settings.MinimumSize.Width),
                Math.Max(originalSize.Height + dy, settings.MinimumSize.Height)
            );

            return NoEntryZoneManager.Instance.GetValidSize(
                new Rectangle(window.CollisionBounds.Location, originalSize),
                proposedSize
            );
        }

        private void StartResizing(GameWindow window)
        {
            if (isResizing) return;
            isResizing = true;
            lastMousePos = window.PointToClient(Cursor.Position);
            originalSize = window.Size;
            currentScale = new SizeF(1.0f, 1.0f);

            // リサイズ開始時に、すべての子要素の元のサイズを記録
            foreach (var child in window.Children)
            {
                originalSizes[child] = child.GetOriginalSize();
            }
        }

        private void StopResizing()
        {
            if (!isResizing) return;
            isResizing = false;
            originalSizes.Clear();
            currentScale = new SizeF(1.0f, 1.0f);
            resizeEffect.ResetAll();
        }

        public override void DrawStrategyMark(Graphics g, Rectangle bounds, bool isHovered)
        {
            DrawResizeMark(g, bounds, isHovered ? Color.White : Color.FromArgb(128, 128, 128));
        }

        protected override Cursor GetStrategyCursor() => Cursors.SizeNWSE;

        private void DrawResizeMark(Graphics g, Rectangle bounds, Color color)
        {
            int markSize = 60;
            int x = bounds.X + (bounds.Width - markSize) / 2;
            int y = bounds.Y + (bounds.Height - markSize) / 2;

            using (var pen = new Pen(color, 2))
            {
                g.DrawLine(pen, x + markSize / 4, y + markSize / 4, x + markSize * 3 / 4, y + markSize * 3 / 4);
                DrawResizeArrows(g, pen, x, y, markSize);
            }
        }

        private void DrawResizeArrows(Graphics g, Pen pen, int x, int y, int size)
        {
            // 左上矢印
            DrawArrowHead(g, pen, x + size / 4, y + size / 4, x + size / 2, y + size / 4);
            DrawArrowHead(g, pen, x + size / 4, y + size / 4, x + size / 4, y + size / 2);

            // 右下矢印
            DrawArrowHead(g, pen, x + size * 3 / 4, y + size * 3 / 4, x + size / 2, y + size * 3 / 4);
            DrawArrowHead(g, pen, x + size * 3 / 4, y + size * 3 / 4, x + size * 3 / 4, y + size / 2);
        }

        private void DrawArrowHead(Graphics g, Pen pen, int x1, int y1, int x2, int y2)
        {
            g.DrawLine(pen, x1, y1, x2, y2);
        }
    }
    public class MovableWindowStrategy : BaseWindowStrategy
    {
        private readonly MovementEffect movementEffect = new MovementEffect();
        private Point lastMousePos;
        private bool isDragging = false;
        private Point lastValidPosition;

        private bool isBlockedRight = false;
        private bool isBlockedLeft = false;
        private bool isBlockedDown = false;
        private bool isBlockedUp = false;

        public MovableWindowStrategy()
        {
            movementEffect = new MovementEffect();
            WindowEffectManager.Instance.AddEffect(movementEffect);
        }
        protected override void OnMouseDown(GameWindow window)
        {
            StartDragging(window);
            window.Capture = true;
        }
        protected override void OnMouseUp(GameWindow window)
        {
            window.Capture = false;
            StopDragging();
        }
        public override void Update(GameWindow window, float deltaTime)
        {
            if (isDragging)
            {
                UpdateMovement(window);
            }
        }
        private void UpdateMovement(GameWindow window)
        {
            Point currentMousePos = window.PointToClient(Cursor.Position);
            var movement = CalculateMovement(window, currentMousePos);

            // 移動の適用
            movementEffect.UpdateMovement(movement);
            WindowEffectManager.Instance.ApplyEffects(window);
        }
        private Vector2 CalculateMovement(GameWindow window, Point currentMousePos)
        {
            int deltaX = currentMousePos.X - lastMousePos.X;
            int deltaY = currentMousePos.Y - lastMousePos.Y;

            UpdateBlockFlags(window);

            Vector2 movement = new(
                (deltaX > 0 && isBlockedRight) || (deltaX < 0 && isBlockedLeft) ? 0 : deltaX,
                (deltaY > 0 && isBlockedDown) || (deltaY < 0 && isBlockedUp) ? 0 : deltaY
            );

            Rectangle proposedBounds = new(
                window.CollisionBounds.X + (int)movement.X,
                window.CollisionBounds.Y + (int)movement.Y,
                window.CollisionBounds.Width,
                window.CollisionBounds.Height
            );

            Rectangle validBounds = NoEntryZoneManager.Instance.GetValidPosition(
                window.CollisionBounds,
                proposedBounds
            );

            return new Vector2(
                validBounds.X - window.CollisionBounds.X,
                validBounds.Y - window.CollisionBounds.Y
            );
        }
        private void UpdateBlockFlags(GameWindow window)
        {
            var bounds = window.CollisionBounds;
            isBlockedRight = CheckCollision(bounds, 1, 0);
            isBlockedLeft = CheckCollision(bounds, -1, 0);
            isBlockedDown = CheckCollision(bounds, 0, 1);
            isBlockedUp = CheckCollision(bounds, 0, -1);
        }
        private bool CheckCollision(Rectangle bounds, int dx, int dy)
        {
            Rectangle checkBounds = new(
                bounds.X + dx,
                bounds.Y + dy,
                bounds.Width,
                bounds.Height
            );
            return NoEntryZoneManager.Instance.IntersectsWithAnyZone(checkBounds);
        }
        public override void HandleWindowMessage(GameWindow window, Message m)
        {
            switch (m.Msg)
            {
                case WindowMessages.WM_LBUTTONDOWN:
                    OnMouseDown(window);
                    break;
                case WindowMessages.WM_LBUTTONUP:
                    OnMouseUp(window);
                    break;
                case WindowMessages.WM_MOUSEMOVE:
                    OnMouseMove(window);
                    break;
            }
        }
        private void StartDragging(GameWindow window)
        {
            if (isDragging) return;  // 既にドラッグ中なら開始しない
            isDragging = true;
            ResetBlockFlags();
            lastMousePos = window.PointToClient(Cursor.Position);
            lastValidPosition = window.Location;
        }

        private void StopDragging()
        {
            if (!isDragging) return;  // ドラッグ中でなければ何もしない
            isDragging = false;
            ResetBlockFlags();
            movementEffect.UpdateMovement(Vector2.Zero);
        }
        public override void DrawStrategyMark(Graphics g, Rectangle bounds, bool isHovered)
        {
            DrawMovementMark(g, bounds, isHovered ? Color.White : Color.FromArgb(128, 128, 128));
        }
        protected override Cursor GetStrategyCursor() => Cursors.SizeAll;
        private void DrawMovementMark(Graphics g, Rectangle bounds, Color color)
        {
            int markSize = 60;
            int x = bounds.X + (bounds.Width - markSize) / 2;
            int y = bounds.Y + (bounds.Height - markSize) / 2;

            using (var pen = new Pen(color, 2))
            {
                // 移動マークの描画
                DrawArrows(g, pen, x, y, markSize);
            }
        }
        private void DrawArrows(Graphics g, Pen pen, int x, int y, int size)
        {
            // 中心の十字
            g.DrawLine(pen, x + size / 2, y, x + size / 2, y + size);
            g.DrawLine(pen, x, y + size / 2, x + size, y + size / 2);

            // 矢印の描画
            DrawArrowHead(g, pen, x + size / 2, y, false);            // 上
            DrawArrowHead(g, pen, x + size / 2, y + size, true);      // 下
            DrawArrowHead(g, pen, x, y + size / 2, false, true);      // 左
            DrawArrowHead(g, pen, x + size, y + size / 2, true, true); // 右
        }
        private void DrawArrowHead(Graphics g, Pen pen, int x, int y, bool isReversed, bool isHorizontal = false)
        {
            int size = 10;
            if (isHorizontal)
            {
                g.DrawLine(pen, x, y, x + (isReversed ? -size : size), y - size);
                g.DrawLine(pen, x, y, x + (isReversed ? -size : size), y + size);
            }
            else
            {
                g.DrawLine(pen, x, y, x - size, y + (isReversed ? -size : size));
                g.DrawLine(pen, x, y, x + size, y + (isReversed ? -size : size));
            }
        }
        private void ResetBlockFlags()
        {
            isBlockedRight = false;
            isBlockedLeft = false;
            isBlockedDown = false;
            isBlockedUp = false;
        }
    }
    public class DeletableWindowStrategy : BaseWindowStrategy
    {
        public override void HandleInput(GameWindow window)
        {
            if (Input.IsKeyDown(Keys.Delete))
            {
                RemoveAndClose(window);
            }
        }
        private void RemoveAndClose(GameWindow window)
        {
            // 子要素の解放
            foreach (var child in window.Children.ToList())
            {
                window.RemoveChild(child);
            }

            // 親からの削除
            window.Parent?.RemoveChild(window);

            window.Close();
            window.NotifyObservers(WindowChangeType.Deleted);
        }
        public override void DrawStrategyMark(Graphics g, Rectangle bounds, bool isHovered)
        {
            int markSize = 60;
            int x = bounds.X + (bounds.Width - markSize) / 2;
            int y = bounds.Y + (bounds.Height - markSize) / 2;

            using (var pen = new Pen(isHovered ? Color.White : Color.FromArgb(128, 128, 128), 2))
            {
                // X印を描画
                g.DrawLine(pen, x, y, x + markSize, y + markSize);
                g.DrawLine(pen, x + markSize, y, x, y + markSize);
            }
        }
        protected override Cursor GetStrategyCursor() => Cursors.Default;
    }
    public class MinimizableWindowStrategy : BaseWindowStrategy
    {
        private readonly MinimizeEffect minimizeEffect;

        public MinimizableWindowStrategy()
        {
            minimizeEffect = new MinimizeEffect();
        }

        protected override void OnMouseDown(GameWindow window)
        {
            minimizeEffect.Activate();
            WindowEffectManager.Instance.AddEffect(minimizeEffect);
            WindowEffectManager.Instance.ApplyEffects(window);
        }

        // 最小化状態の復元も効果的に処理
        public override void HandleWindowMessage(GameWindow window, Message m)
        {
            switch (m.Msg)
            {
                case WindowMessages.WM_SYSCOMMAND:
                    int command = m.WParam.ToInt32() & 0xFFF0;
                    if (command == WindowMessages.SC_MINIMIZE)
                    {
                        WindowEffectManager.Instance.ApplyEffects(window);
                    }
                    else if (command == WindowMessages.SC_RESTORE)
                    {
                        window.OnRestore();
                    }
                    break;
                default:
                    base.HandleWindowMessage(window, m);
                    break;
            }
        }

        public override void DrawStrategyMark(Graphics g, Rectangle bounds, bool isHovered)
        {
            var center = StrategyMarkUtility.GetMarkCenter(bounds, StrategyMarkUtility.DEFAULT_MARK_SIZE);
            var color = StrategyMarkUtility.GetMarkColor(isHovered);

            int markSize = StrategyMarkUtility.DEFAULT_MARK_SIZE;
            int barHeight = markSize / 6;

            using (var brush = new SolidBrush(color))
            {
                g.FillRectangle(brush,
                    center.X,
                    center.Y + (markSize - barHeight) / 2,
                    markSize,
                    barHeight);
            }
        }

        protected override Cursor GetStrategyCursor() => Cursors.Default;

        public override void Update(GameWindow window, float deltaTime)
        {
            // ここで必要に応じて最小化アニメーションなどの更新を行う
            base.Update(window, deltaTime);
        }

        public override void HandleResize(GameWindow window)
        {
            // 最小化/復元時のサイズ変更を適切に処理
            base.HandleResize(window);
        }
    }
    public class TextDisplayWindowStrategy : BaseWindowStrategy
    {
        private readonly string displayText;
        private const float DEFAULT_FONT_SIZE_RATIO = 0.2f;

        public TextDisplayWindowStrategy(string text)
        {
            displayText = text;
        }
        public override void DrawStrategyMark(Graphics g, Rectangle bounds, bool isHovered)
        {
            // テキスト表示ウィンドウはマークを表示しない
        }
        protected override Cursor GetStrategyCursor() => Cursors.Default;

        public string GetDisplayText() => displayText;

        public override void HandleResize(GameWindow window)
        {
            base.HandleResize(window);
            // リサイズ時のテキスト再描画
            window.Invalidate();
        }
    }
}