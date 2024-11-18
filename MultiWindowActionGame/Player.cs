﻿using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Drawing2D;
using System.Numerics;
using static MultiWindowActionGame.GameWindow;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace MultiWindowActionGame
{
    public class Player : IDrawable, IUpdatable,IEffectTarget
    {
        public Rectangle Bounds { get; private set; }
        private float speed = 400.0f;
        private float gravity = 1000.0f;
        private float jampForce = 500;
        public float verticalVelocity = 0;
        private Size currentSize;
        private Size enterPlayerSize;
        private Size enterWindowSize;
        private SizeF currentScale = new SizeF(1.0f, 1.0f);
        public Size originalSize;
        private GameWindow? currentWindow;
        public Region MovableRegion { get; private set; }

        public bool IsGrounded { get; private set; }

        private bool isResizing = false;
        private bool isWindowMoving = false;
        private bool isWindowResizing = false;
        private Vector2 lastMovement = Vector2.Zero;

        private IPlayerState currentState;

        private Vector2 externalMovement = Vector2.Zero;
        public Player()
        {
            Bounds = new Rectangle(150, 150, 40, 40);
            originalSize = new Size(40, 40);
            enterPlayerSize = originalSize;
            currentSize = originalSize;
            currentState = new NormalState();
            MovableRegion = new Region();
        }

        public GameWindow? GetCurrentWindow()
        {
            return currentWindow;
        }

        public void SetState(IPlayerState newState)
        {
            currentState = newState;
        }

        public void SetCurrentWindow(GameWindow? window)
        {
            if (currentWindow != null)
            {
                currentWindow.WindowMoved -= OnWindowMoved;
                currentWindow.WindowResized -= OnWindowResized;
                currentWindow.MoveStarted -= OnMoveStarted;
                currentWindow.MoveEnded -= OnMoveEnded;
                currentWindow.ResizeStarted -= OnResizeStarted;
                currentWindow.ResizeEnded -= OnResizeEnded;
            }

            currentWindow = window;

            if (currentWindow != null)
            {
                currentWindow.WindowMoved += OnWindowMoved;
                currentWindow.WindowResized += OnWindowResized;
                currentWindow.MoveStarted += OnMoveStarted;
                currentWindow.MoveEnded += OnMoveEnded;
                currentWindow.ResizeStarted += OnResizeStarted;
                currentWindow.ResizeEnded += OnResizeEnded;

                EnterWindow(currentWindow);
            }
        }

        private Vector2 CalculateMovement(float deltaTime)
        {
            Vector2 movement = Vector2.Zero;
            if (Input.IsKeyDown(Keys.A)) movement.X -= speed * deltaTime;
            if (Input.IsKeyDown(Keys.D)) movement.X += speed * deltaTime;
            movement.Y += verticalVelocity * deltaTime;
            return movement;
        }

        public bool CanReceiveEffect(IWindowEffect effect)
        {
            // プレイヤーが効果を受けられる状態かチェック
            if (IsInBetweenWindows()) return false;

            // 現在のウィンドウがある場合、そのウィンドウの効果のみ受け付ける
            if (currentWindow != null && !currentWindow.AdjustedBounds.Contains(Bounds))
            {
                return false;
            }

            return true;
        }

        public void ApplyEffect(IWindowEffect effect)
        {
            if (!CanReceiveEffect(effect)) return;
            effect.Apply(this);
        }

        public bool IsCompletelyContained(GameWindow container)
        {
            return container.AdjustedBounds.Contains(Bounds);
        }
        private bool IsInBetweenWindows()
        {
            var intersectingWindows = WindowManager.Instance.GetIntersectingWindows(Bounds);
            return intersectingWindows.Count > 1;
        }
        public void ApplyExternalMovement(Vector2 movement)
        {
            externalMovement = movement;
        }

        public void ApplyScale(SizeF scale)
        {
            currentScale = scale;
            UpdatePlayerSize();
        }

        private bool IsCompletelyInside(Rectangle bounds, Region region, Graphics g)
        {
            // プレイヤーの四隅がすべて領域内にあるかチェック
            return region.IsVisible(bounds.Left, bounds.Top, g) &&
                   region.IsVisible(bounds.Right - 1, bounds.Top, g) &&
                   region.IsVisible(bounds.Left, bounds.Bottom - 1, g) &&
                   region.IsVisible(bounds.Right - 1, bounds.Bottom - 1, g);
        }

        public async Task UpdateAsync(float deltaTime)
        {
            currentState.HandleInput(this);
            currentState.Update(this, deltaTime);

            Vector2 movement = CalculateMovement(deltaTime);
            movement += externalMovement;
            externalMovement = Vector2.Zero;
            ApplyGravity(deltaTime);

            Rectangle newBounds = new Rectangle(
                (int)(Bounds.X + movement.X),
                (int)(Bounds.Y + movement.Y),
                Bounds.Width,
                Bounds.Height
            );

            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                if (!IsValidMove(newBounds, g))
                {
                    // 移動が無効な場合、X軸とY軸で個別に調整
                    newBounds = AdjustMovement(Bounds, newBounds, g);
                }
            }

            if (true)
            {
                GameWindow? topWindow = WindowManager.Instance.GetTopWindowAt(Bounds,currentWindow);
                if (topWindow != currentWindow)
                {
                    if (topWindow != null && topWindow.CanEnter)
                    {
                        ExitWindow(currentWindow);
                        SetCurrentWindow(topWindow);
                    }
                    else if (currentWindow != null)
                    {
                        newBounds = ConstrainToWindow(newBounds, currentWindow);
                        Bounds = newBounds;
                    }
                }
            }

            Bounds = newBounds;
            CheckGrounded();
            Console.WriteLine($"Player position updated: {Bounds}");
        }

        private Rectangle AdjustMovement(Rectangle oldBounds, Rectangle newBounds, Graphics g)
        {
            Rectangle adjustedBounds = oldBounds;

            // X軸の移動を調整
            if (oldBounds.X != newBounds.X)
            {
                int step = Math.Sign(newBounds.X - oldBounds.X);
                while (adjustedBounds.X != newBounds.X)
                {
                    Rectangle testBounds = new Rectangle(
                        adjustedBounds.X + step,
                        adjustedBounds.Y,
                        adjustedBounds.Width,
                        adjustedBounds.Height
                    );
                    if (IsValidMove(testBounds, g))
                    {
                        adjustedBounds.X += step;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // Y軸の移動を調整
            if (oldBounds.Y != newBounds.Y)
            {
                int step = Math.Sign(newBounds.Y - oldBounds.Y);
                while (adjustedBounds.Y != newBounds.Y)
                {
                    Rectangle testBounds = new Rectangle(
                        adjustedBounds.X,
                        adjustedBounds.Y + step,
                        adjustedBounds.Width,
                        adjustedBounds.Height
                    );
                    if (IsValidMove(testBounds, g))
                    {
                        adjustedBounds.Y += step;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return adjustedBounds;
        }

        private bool IsValidMove(Rectangle bounds, Graphics g)
        {
            if (!MovableRegion.IsEmpty(g))
            {
                return IsCompletelyInside(bounds, MovableRegion, g);
            }
            else
            {
                // 移動可能領域が空の場合（例：デスクトップ上）、メインフォームに制限
                return IsWithinMainForm(bounds);
            }
        }

        private bool IsWithinMainForm(Rectangle bounds)
        {
            if (Program.mainForm != null)
            {
                return bounds.Left >= 0 && bounds.Right <= Program.mainForm.ClientSize.Width &&
                       bounds.Top >= 0 && bounds.Bottom <= Program.mainForm.ClientSize.Height;
            }
            return false;
        }

        private Rectangle ConstrainToRegion(Rectangle bounds, Region region)
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                if (!region.IsVisible(bounds, g))
                {
                    // 領域内の最も近い位置を見つける
                    Point center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                    RectangleF[] scans = region.GetRegionScans(new Matrix());

                    float minDistance = float.MaxValue;
                    Point nearestPoint = center;

                    foreach (RectangleF rect in scans)
                    {
                        Point testPoint = new Point(
                            (int)Math.Max(rect.Left, Math.Min(center.X, rect.Right)),
                            (int)Math.Max(rect.Top, Math.Min(center.Y, rect.Bottom))
                        );

                        float distance = (float)Math.Sqrt(Math.Pow(testPoint.X - center.X, 2) + Math.Pow(testPoint.Y - center.Y, 2));
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            nearestPoint = testPoint;
                        }
                    }

                    bounds.X = nearestPoint.X - bounds.Width / 2;
                    bounds.Y = nearestPoint.Y - bounds.Height / 2;
                }
            }

            return bounds;
        }


        public void UpdateMovableRegion(Region newRegion)
        {
            MovableRegion.Dispose();
            MovableRegion = newRegion;
        }

        private void UpdatePlayerSize()
        {
            try
            {
                int newWidth = (int)(enterPlayerSize.Width * currentScale.Width);
                int newHeight = (int)(enterPlayerSize.Height * currentScale.Height);
                Size newSize = new Size(newWidth, newHeight);

                // プレイヤーの中心位置を維持
                Point center = new Point(Bounds.X + Bounds.Width / 2, Bounds.Y + Bounds.Height / 2);
                Rectangle newBounds = new Rectangle(
                    center.X - newWidth / 2,
                    center.Y - newHeight / 2,
                    newWidth,
                    newHeight
                );

                // 新しい位置とサイズを設定
                Bounds = newBounds;
                currentSize = newSize;

                // ウィンドウ内に収める
                ConstrainToCurrentWindow();
            }
            catch (OverflowException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdatePlayerSize: {ex.Message}");
                // エラーが発生した場合、サイズを変更せずに現在のサイズを維持
            }
        }
        private void OnMoveStarted(object? sender, EventArgs e)
        {
            isWindowMoving = true;
        }

        private void OnMoveEnded(object? sender, EventArgs e)
        {
            isWindowMoving = false;
        }
        private void OnResizeStarted(object? sender, EventArgs e)
        {
            isResizing = true;
        }

        private void OnResizeEnded(object? sender, EventArgs e)
        {
            isResizing = false;
        }
        private void OnWindowMoved(object? sender, EventArgs e)
        {
            if (currentWindow != null)
            {
                // ウィンドウ内での相対位置を維持
                float relativeX = (Bounds.X - currentWindow.AdjustedBounds.X) / (float)currentWindow.AdjustedBounds.Width;
                float relativeY = (Bounds.Y - currentWindow.AdjustedBounds.Y) / (float)currentWindow.AdjustedBounds.Height;

                Bounds = new Rectangle(
                    (int)(currentWindow.AdjustedBounds.X + relativeX * currentWindow.AdjustedBounds.Width),
                    (int)(currentWindow.AdjustedBounds.Y + relativeY * currentWindow.AdjustedBounds.Height),
                    Bounds.Width,
                    Bounds.Height
                );

                IsGrounded = false; // ウィンドウが移動したので、接地状態をリセット
            }
        }

        private void OnWindowResized(object? sender, SizeChangedEventArgs e)
        {
            if (currentWindow != null && currentWindow.IsResizable())
            {
                float scaleX = (float)e.NewSize.Width / enterWindowSize.Width;
                float scaleY = (float)e.NewSize.Height / enterWindowSize.Height;

                if (!float.IsInfinity(scaleX) && !float.IsInfinity(scaleY))
                {
                    currentScale = new SizeF(scaleX, scaleY);
                    UpdatePlayerSize();
                }
            }
        }
        public void SetPosition(Point newPosition)
        {
            Bounds = new Rectangle(
                newPosition,
                Bounds.Size
            );
        }
        public void Draw(Graphics g)
        {
            // プレイヤーを描画
            g.FillRectangle(Brushes.Blue, Bounds);
        }

        public void Move(float deltaTime)
        {
            //float moveX = 0;
            //if (Input.IsKeyDown(Keys.A)) moveX -= speed * deltaTime;
            //if (Input.IsKeyDown(Keys.D)) moveX += speed * deltaTime;

            //Bounds = new Rectangle(
            //    (int)(Bounds.X + moveX),
            //    (int)(Bounds.Y + verticalVelocity * deltaTime),
            //    Bounds.Width,
            //    Bounds.Height
            //);
        }

        public void ApplyGravity(float deltaTime)
        {
            if (!IsGrounded)
            {
                verticalVelocity += gravity * deltaTime;
            }
            else
            {
                verticalVelocity = 0;
            }
        }

        public void DrawDebugInfo(Graphics g)
        {
            // プレイヤーの矩形を黄色で描画
            g.DrawRectangle(new Pen(Color.Yellow, 2), Bounds);
            using (Pen pen = new Pen(Color.Green, 2))
            {
                //g.DrawRectangle(pen, Bounds);  // プレイヤーの境界線を描画

                // 移動可能領域を描画
                using (Matrix matrix = new Matrix())
                {
                    RectangleF[] scans = MovableRegion.GetRegionScans(matrix);
                    foreach (RectangleF rect in scans)
                    {
                        g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                    }
                }
            }
        }

        public void Jump()
        {
            if (IsGrounded)
            {
                verticalVelocity = -jampForce;
                IsGrounded = false;
            }
        }

        private void CheckGrounded()
        {
            if (currentWindow != null)
            {
                IsGrounded = Bounds.Bottom >= currentWindow.AdjustedBounds.Bottom;
                if (IsGrounded)
                {
                    Bounds = new Rectangle(Bounds.X, currentWindow.AdjustedBounds.Bottom - Bounds.Height, Bounds.Width, Bounds.Height);
                    verticalVelocity = 0;
                }
            }
            else if (Program.mainForm != null)
            {
                IsGrounded = Bounds.Bottom >= Program.mainForm.ClientSize.Height;
                if (IsGrounded)
                {
                    Bounds = new Rectangle(Bounds.X, Program.mainForm.ClientSize.Height - Bounds.Height, Bounds.Width, Bounds.Height);
                    verticalVelocity = 0;
                }
            }
        }
        private void EnterWindow(GameWindow window)
        {
            enterPlayerSize = Bounds.Size;
            enterWindowSize = window.Size;

            UpdateMovableRegion(WindowManager.Instance.CalculateMovableRegion(currentWindow));
            System.Diagnostics.Debug.WriteLine($"Player entered window {window.Id}");
        }

        private void ExitWindow(GameWindow window)
        {
            if(currentWindow == null)return;
            System.Diagnostics.Debug.WriteLine($"Player Exit window {window.Id}");
            currentWindow = null;
            IsGrounded = false;
        }
        private void ConstrainToCurrentWindow()
        {
            if (currentWindow != null)
            {
                Rectangle adjustedBounds = currentWindow.AdjustedBounds;
                Bounds = new Rectangle(
                    Math.Max(adjustedBounds.Left, Math.Min(Bounds.X, adjustedBounds.Right - Bounds.Width)),
                    Math.Max(adjustedBounds.Top, Math.Min(Bounds.Y, adjustedBounds.Bottom - Bounds.Height)),
                    Bounds.Width,
                    Bounds.Height
                );
            }
            else if (Program.mainForm != null)
            {
                Bounds = new Rectangle(
                    Math.Max(0, Math.Min(Bounds.X, Program.mainForm.ClientSize.Width - Bounds.Width)),
                    Math.Max(0, Math.Min(Bounds.Y, Program.mainForm.ClientSize.Height - Bounds.Height)),
                    Bounds.Width,
                    Bounds.Height
                );
            }
        }
        public void ConstrainToWindow(GameWindow window)
        {
            Rectangle newBounds = Bounds;
            newBounds.X = Math.Max(window.AdjustedBounds.Left, Math.Min(newBounds.X, window.AdjustedBounds.Right - newBounds.Width));
            newBounds.Y = Math.Max(window.AdjustedBounds.Top, Math.Min(newBounds.Y, window.AdjustedBounds.Bottom - newBounds.Height));
            Bounds = newBounds;

            if (Bounds.Bottom >= window.AdjustedBounds.Bottom)
            {
                IsGrounded = true;
            }
        }

        private Rectangle ConstrainToWindow(Rectangle bounds, GameWindow window)
        {
            Rectangle adjustedBounds = window.AdjustedBounds;
            bounds.X = Math.Max(adjustedBounds.Left, Math.Min(bounds.X, adjustedBounds.Right - bounds.Width));
            bounds.Y = Math.Max(adjustedBounds.Top, Math.Min(bounds.Y, adjustedBounds.Bottom - bounds.Height));
            return bounds;
        }

        private void ConstrainToMainForm(ref Rectangle bounds)
        {
            if (Program.mainForm != null)
            {
                bounds.X = Math.Max(0, Math.Min(bounds.X, Program.mainForm.ClientSize.Width - bounds.Width));
                bounds.Y = Math.Max(0, Math.Min(bounds.Y, Program.mainForm.ClientSize.Height - bounds.Height));

                if (bounds.Bottom >= Program.mainForm.ClientSize.Height)
                {
                    bounds.Y = Program.mainForm.ClientSize.Height - bounds.Height;
                    verticalVelocity = 0;
                    IsGrounded = true;
                }
            }
        }
    }
}