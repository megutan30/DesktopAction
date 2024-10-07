using System.Drawing;
using static MultiWindowActionGame.GameWindow;

namespace MultiWindowActionGame
{
    public class Player : IDrawable, IUpdatable
    {
        public Rectangle Bounds { get; private set; }
        private float speed = 200.0f;
        private float gravity = 500.0f;
        private float verticalVelocity = 0;
        private Size currentSize;
        private Size enterPlayerSize;
        private Size enterWindowSize;
        private SizeF currentScale = new SizeF(1.0f, 1.0f);
        private Size originalSize;
        private GameWindow? currentWindow;

        public bool IsGrounded { get; private set; }

        private IPlayerState currentState;

        public Player()
        {
            Bounds = new Rectangle(100, 100, 40, 40);
            originalSize = new Size(40, 40);
            enterPlayerSize = originalSize;
            currentSize = originalSize;
            currentState = new NormalState();
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
            }

            currentWindow = window;

            if (currentWindow != null)
            {
                currentWindow.WindowMoved += OnWindowMoved;
                currentWindow.WindowResized += OnWindowResized;

                if (currentWindow.IsResizable())
                {
                    // 新しいウィンドウがリサイズ可能な場合、プレイヤーのサイズを更新
                    float scaleX = (float)currentWindow.Size.Width / enterWindowSize.Width;
                    float scaleY = (float)currentWindow.Size.Height / enterWindowSize.Height;
                    currentScale = new SizeF(scaleX, scaleY);
                    UpdatePlayerSize();
                }
            }
        }

        
    public async Task UpdateAsync(float deltaTime)
    {
        currentState.HandleInput(this);
        currentState.Update(this, deltaTime);

        Rectangle newBounds = CalculateNewPosition(deltaTime);

        GameWindow? newWindow = WindowManager.Instance.GetWindowAt(newBounds);

        if (newWindow != currentWindow)
        {
            if (newWindow != null && newWindow.CanEnter)
            {
                // 新しいウィンドウに入る
                if (currentWindow != null)
                {
                    ExitWindow();
                }
                EnterWindow(newWindow);
                await WindowManager.Instance.BringWindowToFrontAsync(newWindow);
                ConstrainToWindow(newWindow, ref newBounds);
            }
            else if (currentWindow != null && currentWindow.CanExit)
            {
                // 現在のウィンドウから出る
                if (currentWindow.Strategy is DeletableWindowStrategy)
                {
                    // DeletableWindowStrategyの場合は、そのまま外に出る
                    ExitWindow();
                    ConstrainToMainForm(ref newBounds);
                }
                else
                {
                    // それ以外の場合は、最も近いウィンドウに戻す
                    GameWindow? nearestWindow = WindowManager.Instance.GetNearestWindow(newBounds);
                    if (nearestWindow != null)
                    {
                        ExitWindow();
                        EnterWindow(nearestWindow);
                        await WindowManager.Instance.BringWindowToFrontAsync(nearestWindow);
                        ConstrainToWindow(nearestWindow, ref newBounds);
                    }
                    else
                    {
                        // 近くにウィンドウがない場合は、現在のウィンドウ内に制限
                        ConstrainToWindow(currentWindow, ref newBounds);
                    }
                }
            }
            else
            {
                // 現在のウィンドウ内で移動を制限
                ConstrainToWindow(currentWindow, ref newBounds);
            }
        }
        else if (currentWindow != null)
        {
            // 同じウィンドウ内で移動を制限
            ConstrainToWindow(currentWindow, ref newBounds);
        }
        else
        {
            // メインフォーム内で移動を制限
            ConstrainToMainForm(ref newBounds);
        }

        Bounds = newBounds;
        Console.WriteLine($"Player position updated: {Bounds}");
    }

        private void UpdatePlayerSize()
        {
            int newWidth = (int)(enterPlayerSize.Width * currentScale.Width);
            int newHeight = (int)(enterPlayerSize.Height * currentScale.Height);
            currentSize = new Size(newWidth, newHeight);

            // プレイヤーの位置を調整して中心を維持
            int x = Bounds.X + (Bounds.Width - newWidth) / 2;
            int y = Bounds.Y + (Bounds.Height - newHeight) / 2;

            Bounds = new Rectangle(x, y, newWidth, newHeight);
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

                // プレイヤーをウィンドウ内に収める
                ConstrainToWindow(currentWindow);

                IsGrounded = false; // ウィンドウが移動したので、接地状態をリセット
            }
        }

        private void OnWindowResized(object? sender, SizeChangedEventArgs e)
        {
            if (currentWindow != null && currentWindow.IsResizable())
            {
                float scaleX = (float)e.NewSize.Width / enterWindowSize.Width;
                float scaleY = (float)e.NewSize.Height / enterWindowSize.Height;

                if (scaleX != currentScale.Width || scaleY != currentScale.Height)
                {
                    currentScale = new SizeF(scaleX, scaleY);
                    UpdatePlayerSize();
                }
            }
        }

        private Rectangle CalculateNewPosition(float deltaTime)
        {
            float moveX = 0;
            float moveY = 0;

            if (Input.IsKeyDown(Keys.A)) moveX -= speed * deltaTime;
            if (Input.IsKeyDown(Keys.D)) moveX += speed * deltaTime;

            // 重力の適用
            if (!IsGrounded)
            {
                verticalVelocity += gravity * deltaTime;
            }
            moveY = verticalVelocity * deltaTime;

            return new Rectangle(
                (int)(Bounds.X + moveX),
                (int)(Bounds.Y + moveY),
                Bounds.Width,
                Bounds.Height
            );
        }

        public void Draw(Graphics g)
        {
            currentState.Draw(this, g);
        }

        public void Move(float deltaTime)
        {
            float moveX = 0;
            if (Input.IsKeyDown(Keys.A)) moveX -= speed * deltaTime;
            if (Input.IsKeyDown(Keys.D)) moveX += speed * deltaTime;

            Bounds = new Rectangle(
                (int)(Bounds.X + moveX),
                Bounds.Y,
                Bounds.Width,
                Bounds.Height
            );
        }

        public void ApplyGravity(float deltaTime)
        {
            if (!IsGrounded)
            {
                verticalVelocity += gravity * deltaTime;
                Bounds = new Rectangle(
                    Bounds.X,
                    (int)(Bounds.Y + verticalVelocity * deltaTime),
                    Bounds.Width,
                    Bounds.Height
                );
            }
        }

        public void Jump()
        {
            verticalVelocity = -300;
            IsGrounded = false;
        }

        private void EnterWindow(GameWindow window)
        {
            currentWindow = window;
            
            enterPlayerSize = Bounds.Size;
            enterWindowSize = window.Size;
            System.Diagnostics.Debug.WriteLine($"Player entered window {window.Id}");
        }

        private void ExitWindow()
        {
            Console.WriteLine($"Player exited window {currentWindow?.Id}");
            currentWindow = null;
            IsGrounded = false;
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

        private void ConstrainToWindow(GameWindow window, ref Rectangle bounds)
        {
            int newX = Math.Max(window.AdjustedBounds.Left, Math.Min(bounds.X, window.AdjustedBounds.Right - bounds.Width));
            int newY = Math.Max(window.AdjustedBounds.Top, Math.Min(bounds.Y, window.AdjustedBounds.Bottom - bounds.Height));

            // X軸の移動が制限された場合
            if (newX != bounds.X)
            {
                bounds.X = newX;
            }

            // Y軸の移動が制限された場合
            if (newY != bounds.Y)
            {
                bounds.Y = newY;
                if (bounds.Bottom >= window.AdjustedBounds.Bottom)
                {
                    verticalVelocity = 0;
                    IsGrounded = true;
                }
                else if (bounds.Top <= window.AdjustedBounds.Top)
                {
                    verticalVelocity = 0;
                }
            }

            // ウィンドウ内にいない場合はIsGroundedをfalseに設定
            if (!window.AdjustedBounds.Contains(bounds))
            {
                IsGrounded = false;
            }
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