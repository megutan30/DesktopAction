using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;

namespace MultiWindowActionGame
{
    public class Player : IEffectTarget
    {
        private Rectangle bounds;
        public Rectangle Bounds => bounds;
        private float speed = 400.0f;
        private float gravity = 1000.0f;
        private float jumpForce = 500;
        private float verticalVelocity = 0;
        public Size OriginalSize { get; }
        private Size referenceSize;
        private IPlayerState currentState;
        public GameWindow? Parent { get; private set; }
        public ICollection<IEffectTarget> Children { get; } = new HashSet<IEffectTarget>();
        public bool IsGrounded { get; private set; }

        public Region MovableRegion { get; private set; }

        public Player()
        {
            OriginalSize = new Size(40, 40);
            referenceSize = OriginalSize;
            bounds = new Rectangle(150, 150, OriginalSize.Width, OriginalSize.Height);
            currentState = new NormalState();
            MovableRegion = new Region();
        }

        public void UpdatePosition(Point newPosition)
        {
            bounds.Location = newPosition;
        }

        public void UpdateSize(Size newSize)
        {
            bounds.Size = newSize;
            ConstrainToCurrentWindow();
        }
        public void UpdateMovableRegion(Region newRegion)
        {
            MovableRegion.Dispose();
            MovableRegion = newRegion;
        }
        private bool IsCompletelyInside(Rectangle bounds, Region region, Graphics g)
        {
            return region.IsVisible(bounds.Left, bounds.Top, g) &&
                   region.IsVisible(bounds.Right - 1, bounds.Top, g) &&
                   region.IsVisible(bounds.Left, bounds.Bottom - 1, g) &&
                   region.IsVisible(bounds.Right - 1, bounds.Bottom - 1, g);
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

        private bool IsValidMove(Rectangle bounds, Graphics g)
        {
            if (!MovableRegion.IsEmpty(g))
            {
                return IsCompletelyInside(bounds, MovableRegion, g);
            }
            else
            {
                return IsWithinMainForm(bounds);
            }
        }
        public void AddChild(IEffectTarget child)
        {
            Children.Add(child);
        }

        public void RemoveChild(IEffectTarget child)
        {
            Children.Remove(child);
        }

        public bool CanReceiveEffect(IWindowEffect effect)
        {
            //if (isBeingOperated) return false;

            // 親がない場合は効果を受けない
            if (Parent == null) return false;

            // 効果の送信元が現在の親でない場合は受け付けない
            // これは将来的に効果に送信元の情報を追加する必要があります
            return true;
        }
        public void SetReferenceSize(Size size)
        {
            referenceSize = size;
        }
        public void SetParent(GameWindow? newParent)
        {
            if (Parent == newParent) return;

            Parent?.RemoveChild(this);
            Parent = newParent;
            Parent?.AddChild(this);

            // 親が変更されたときの移動可能領域を更新
            if (Parent != null)
            {
                OnEnterWindow(Parent);
                UpdateMovableRegion(WindowManager.Instance.CalculateMovableRegion(Parent));
            }
            else
            {
                MovableRegion.Dispose();
                MovableRegion = new Region();
            }

            OnParentChanged();
        }

        private void OnParentChanged()
        {
            // 新しい親のウィンドウ内に収める
            ConstrainToCurrentWindow();
            // 接地状態のチェックは維持するが、速度は保持
            CheckGrounded();
        }
        public void OnEnterWindow(GameWindow window)
        {
            // 新しいウィンドウに入った時に現在のサイズを基準サイズとして設定
            referenceSize = bounds.Size;
        }
        // 効果の適用を拡張
        public void ApplyEffect(IWindowEffect effect)
        {
            if (!CanReceiveEffect(effect)) return;

            switch (effect)
            {
                case MovementEffect moveEffect:
                    HandleMovementEffect(moveEffect);
                    break;
                case ResizeEffect resizeEffect:
                    HandleResizeEffect(resizeEffect);
                    break;
            }
        }
        private void HandleMovementEffect(MovementEffect effect)
        {
            Rectangle newBounds = new Rectangle(
                bounds.Location,
                bounds.Size
            );
            newBounds = ValidateNewPosition(newBounds);
            bounds = newBounds;

            ConstrainToCurrentWindow();
        }

        private void HandleResizeEffect(ResizeEffect effect)
        {
            var currentScale = effect.GetCurrentScale(this);
            Size newSize = new Size(
                (int)(referenceSize.Width * currentScale.Width),
                (int)(referenceSize.Height * currentScale.Height)
            );
            // 中心位置を保持したままサイズを変更
            Point center = new Point(
                bounds.X + bounds.Width / 2,
                bounds.Y + bounds.Height / 2
            );

            bounds = new Rectangle(
                center.X - newSize.Width / 2,
                center.Y - newSize.Height / 2,
                newSize.Width,
                newSize.Height
            );

            ConstrainToCurrentWindow();
        }

        private Vector2 CalculateMovement(float deltaTime)
        {
            Vector2 movement = Vector2.Zero;

            if (Input.IsKeyDown(Keys.A))
            {
                movement.X -= speed * deltaTime;
            }
            if (Input.IsKeyDown(Keys.D))
            {
                movement.X += speed * deltaTime;
            }

            if (Input.IsKeyDown(Keys.Space) && IsGrounded)
            {
                Jump();
            }

            movement.Y += verticalVelocity * deltaTime;

            return movement;
        }

        public async Task UpdateAsync(float deltaTime)
        {
            currentState.HandleInput(this);
            currentState.Update(this, deltaTime);

            Vector2 movement = CalculateMovement(deltaTime);

            var preGravityPosition = bounds.Location;

            ApplyGravity(deltaTime);

            Rectangle newBounds = new Rectangle(
                (int)(bounds.X + movement.X),
                (int)(bounds.Y + movement.Y),
                bounds.Width,
                bounds.Height
            );

            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                if (!IsValidMove(newBounds, g))
                {
                    newBounds = AdjustMovement(bounds, newBounds, g);
                }
            }

            // 移動前に新しい位置でのウィンドウをチェック
            GameWindow? newWindow = WindowManager.Instance.GetTopWindowAt(newBounds, Parent);
            if (newWindow != Parent)
            {
                if (newWindow != null)
                {
                    float previousVelocity = verticalVelocity;
                    SetParent(newWindow);
                    verticalVelocity = previousVelocity;

                    // 新しいウィンドウに対する移動可能領域を更新

                    UpdateMovableRegion(WindowManager.Instance.CalculateMovableRegion(newWindow));
                }
            }
            bounds = newBounds;
            CheckGrounded();
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

        private Rectangle ValidateNewPosition(Rectangle newBounds)
        {
            if (Parent != null)
            {
                return ConstrainToWindow(newBounds, Parent);
            }
            else if (Program.mainForm != null)
            {
                return ConstrainToMainForm(newBounds);
            }
            return newBounds;
        }

        public void Draw(Graphics g)
        {
            currentState.Draw(this, g);

            if (MainGame.IsDebugMode)
            {
                DrawDebugInfo(g);
            }
        }

        private void DrawDebugInfo(Graphics g)
        {
            g.DrawRectangle(new Pen(Color.Yellow, 2), bounds);
            if (Parent != null)
            {
                g.DrawString($"Parent: {Parent.Id}", SystemFonts.DefaultFont, Brushes.Yellow, 
                    bounds.X, bounds.Y - 20);
            }
        }

        public void Jump()
        {
            if (IsGrounded)
            {
                verticalVelocity = -jumpForce;
                IsGrounded = false;
                SetState(new JumpingState());
            }
        }

        private void ApplyGravity(float deltaTime)
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

        private void CheckGrounded()
        {
            bool wasGrounded = IsGrounded;

            if (Parent != null)
            {
                IsGrounded = bounds.Bottom >= Parent.AdjustedBounds.Bottom;
                if (IsGrounded)
                {
                    bounds = new Rectangle(
                        bounds.X,
                        Parent.AdjustedBounds.Bottom - bounds.Height,
                        bounds.Width,
                        bounds.Height
                    );
                    if (!wasGrounded)
                    {
                        // 着地時の処理
                        verticalVelocity = 0;
                        SetState(new NormalState());
                        Console.WriteLine("Landed in window"); // デバッグ用
                    }
                }
            }
            else if (Program.mainForm != null)
            {
                IsGrounded = bounds.Bottom >= Program.mainForm.ClientSize.Height;
                if (IsGrounded)
                {
                    bounds = new Rectangle(
                        bounds.X,
                        Program.mainForm.ClientSize.Height - bounds.Height,
                        bounds.Width,
                        bounds.Height
                    );
                    if (!wasGrounded)
                    {
                        // 着地時の処理
                        verticalVelocity = 0;
                        SetState(new NormalState());
                        Console.WriteLine("Landed on desktop"); // デバッグ用
                    }
                }
            }
        }

        private void ConstrainToCurrentWindow()
        {
            if (Parent != null)
            {
                bounds = ConstrainToWindow(bounds, Parent);
            }
            else if (Program.mainForm != null)
            {
                bounds = ConstrainToMainForm(bounds);
            }
        }

        private Rectangle ConstrainToWindow(Rectangle bounds, GameWindow window)
        {
            return new Rectangle(
                Math.Max(window.AdjustedBounds.Left, 
                    Math.Min(bounds.X, window.AdjustedBounds.Right - bounds.Width)),
                Math.Max(window.AdjustedBounds.Top, 
                    Math.Min(bounds.Y, window.AdjustedBounds.Bottom - bounds.Height)),
                bounds.Width,
                bounds.Height
            );
        }

        private Rectangle ConstrainToMainForm(Rectangle bounds)
        {
            if (Program.mainForm == null) return bounds;

            return new Rectangle(
                Math.Max(0, Math.Min(bounds.X, Program.mainForm.ClientSize.Width - bounds.Width)),
                Math.Max(0, Math.Min(bounds.Y, Program.mainForm.ClientSize.Height - bounds.Height)),
                bounds.Width,
                bounds.Height
            );
        }

        public void SetState(IPlayerState newState)
        {
            currentState = newState;
        }
    }
}