﻿using System.Drawing.Drawing2D;
using System.Numerics;
using System.Runtime.InteropServices;

namespace MultiWindowActionGame
{
    public class PlayerForm : BaseEffectTarget
    {
        private GameSettings.PlayerSettings settings;
        private IPlayerState currentState;
        private GameWindow? lastValidParent;
        private DateTime? minimizedTime;
        private float verticalVelocity = 0;
        private Region movableRegion;
        private Size originalSize;
        public GameWindow? LastValidParent => lastValidParent;
        public bool IsGrounded { get; private set; }
        public float VerticalVelocity => verticalVelocity;
        public TimeSpan TimeSinceMinimized =>
            minimizedTime.HasValue ? DateTime.Now - minimizedTime.Value : TimeSpan.MaxValue;
        public PlayerForm(Point startPosition)
        {
            settings = GameSettings.Instance.Player;
            GameSettings.Instance.SettingsChanged += OnSettingsChanged;
            bounds = new Rectangle(startPosition, settings.DefaultSize);
            originalSize = settings.DefaultSize;
            currentState = new NormalState();
            movableRegion = new Region();
            InitializeForm();
            this.Load += PlayerForm_Load;

            WindowManager.Instance.RegisterFormOrder(this, WindowManager.ZOrderPriority.Player);
        }
        public IPlayerState GetCurrentState() => currentState;
        public Region GetMovableRegion() => movableRegion.Clone();
        public Rectangle GetGroundCheckArea() => new Rectangle(
            bounds.X,
            bounds.Bottom - settings.GroundCheckHeight,
            bounds.Width,
            settings.GroundCheckHeight
        );
        private void OnSettingsChanged(object? sender, GameSettings.SettingsChangedEventArgs e)
        {
            if (e.Type == GameSettings.SettingType.Player || e.Type == GameSettings.SettingType.All)
            {
                // 設定を再読み込み
                settings = GameSettings.Instance.Player;

                if (this.InvokeRequired)
                {
                    this.Invoke(UpdatePlayerProperties);
                }
                else
                {
                    UpdatePlayerProperties();
                }
            }
        }
        private void UpdatePlayerProperties()
        {
            if (bounds.Size != settings.DefaultSize)
            {
                ResetSize(settings.DefaultSize);
            }
        }
        private void InitializeForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = bounds.Location;
            this.Size = bounds.Size;
            this.ShowInTaskbar = true;
            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;
            this.MinimumSize = new Size(5, 5);
            this.Text = "Player";

            this.SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint,
                true
            );

            this.Paint += OnPaint;
        }
        private void PlayerForm_Load(object? sender, EventArgs e)
        {
            SetWindowProperties();
            WindowManager.Instance.UpdateFormZOrder(this, WindowManager.ZOrderPriority.Player);
        }
        private void SetWindowProperties()
        {
            int exStyle = WindowMessages.GetWindowLong(this.Handle, WindowMessages.GWL_EXSTYLE);
            exStyle |= WindowMessages.WS_EX_LAYERED;
            exStyle |= WindowMessages.WS_EX_TRANSPARENT;
            WindowMessages.SetWindowLong(this.Handle, WindowMessages.GWL_EXSTYLE, exStyle);
        }
        public override async Task UpdateAsync(float deltaTime)
        {
            if (IsMinimized) return;

            HandleKeyInput();
            currentState.Update(this, deltaTime);
            ApplyGravity(deltaTime);
            await HandleMovement(deltaTime);
            CheckGrounded();
        }
        private void HandleKeyInput()
        {
            if (IsGrounded && (Input.IsKeyDown(Keys.Space) || Input.IsKeyDown(Keys.Up) || Input.IsKeyDown(Keys.W)))
            {
                Jump();
            }
        }
        private Vector2 CalculateMovement(float deltaTime)
        {
            Vector2 movement = Vector2.Zero;

            if (Input.IsKeyDown(Keys.A) || Input.IsKeyDown(Keys.Left))
            {
                movement.X -= settings.MovementSpeed * deltaTime;
            }
            if (Input.IsKeyDown(Keys.D) || Input.IsKeyDown(Keys.Right))
            {
                movement.X += settings.MovementSpeed * deltaTime;
            }

            movement.Y += verticalVelocity * deltaTime;
            return movement;
        }
        private void Jump()
        {
            verticalVelocity = -settings.JumpForce;
            IsGrounded = false;
            SetState(new JumpingState());
        }
        private void ApplyGravity(float deltaTime)
        {
            if (!IsGrounded)
            {
                verticalVelocity += settings.Gravity * deltaTime;
            }
            else
            {
                verticalVelocity = 0;
            }
        }
        private async Task HandleMovement(float deltaTime)
        {
            Vector2 movement = CalculateMovement(deltaTime);
            Rectangle proposedBounds = new Rectangle(
                bounds.X + (int)movement.X,
                bounds.Y + (int)movement.Y,
                bounds.Width,
                bounds.Height
            );

            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                if (!IsValidMove(proposedBounds, g))
                {
                    proposedBounds = AdjustMovement(bounds, proposedBounds, g);
                }
            }

            if (Parent == null)
            {
                proposedBounds = HandleWindowCollisions(proposedBounds);
                var coveringWindow = WindowManager.Instance.GetWindowFullyContaining(proposedBounds);
                if (coveringWindow != null)
                {
                    SetParent(coveringWindow);
                }
            }
            else
            {
                var newWindow = WindowManager.Instance.GetTopWindowAt(proposedBounds, Parent);
                if (newWindow != null && newWindow != Parent)
                {
                    SetParent(newWindow);
                }
            }

            UpdatePosition(proposedBounds.Location);
        }
        private void OnEnterWindow(GameWindow window)
        {
            // 新しいウィンドウに入った時のサイズを基準として保存
            originalSize = bounds.Size;
        }
        private Rectangle HandleWindowCollisions(Rectangle newBounds)
        {
            var adjustedBounds = newBounds;
            adjustedBounds = NoEntryZoneManager.Instance.GetValidPosition(bounds, adjustedBounds);

            var intersectingWindows = WindowManager.Instance.GetIntersectingWindows(adjustedBounds);
            foreach (var window in intersectingWindows)
            {
                Rectangle windowBounds = window.CollisionBounds;
                if (bounds.Bottom <= windowBounds.Top && adjustedBounds.Bottom > windowBounds.Top)
                {
                    adjustedBounds.Y = windowBounds.Top - adjustedBounds.Height;
                    verticalVelocity = 0;
                    IsGrounded = true;
                }
                else if (bounds.Top >= windowBounds.Bottom && adjustedBounds.Top < windowBounds.Bottom)
                {
                    adjustedBounds.Y = windowBounds.Bottom;
                    verticalVelocity = 0;
                }
                else if (bounds.Right <= windowBounds.Left && adjustedBounds.Right > windowBounds.Left)
                {
                    adjustedBounds.X = windowBounds.Left - adjustedBounds.Width;
                }
                else if (bounds.Left >= windowBounds.Right && adjustedBounds.Left < windowBounds.Right)
                {
                    adjustedBounds.X = windowBounds.Right;
                }
            }

            return adjustedBounds;
        }
        private void CheckGrounded()
        {
            if (currentState is JumpingState) return;
            bool wasGrounded = IsGrounded;
            IsGrounded = false;

            Rectangle currentFeetBounds = new Rectangle(
                bounds.X,
                bounds.Bottom - 10,
                bounds.Width,
                settings.GroundCheckHeight
            );

            float maxVerticalStep = Math.Max(Math.Abs(verticalVelocity * GameTime.DeltaTime), 20f);
            Rectangle sweepBounds = new Rectangle(
                currentFeetBounds.X,
                Math.Min(currentFeetBounds.Y, currentFeetBounds.Y + (int)(verticalVelocity * GameTime.DeltaTime)) - 5,
                currentFeetBounds.Width,
                (int)maxVerticalStep + currentFeetBounds.Height + 10
            );

            // 不可侵領域との判定
            foreach (var zone in NoEntryZoneManager.Instance.Zones)
            {
                if (bounds.Bottom >= zone.Bounds.Top &&
                    bounds.Bottom <= zone.Bounds.Top + 5 &&
                    bounds.Right > zone.Bounds.Left &&
                    bounds.Left < zone.Bounds.Right)
                {
                    HandleGrounding(zone.Bounds.Top, wasGrounded);
                    return;
                }
            }

            var intersectingWindows = WindowManager.Instance.GetIntersectingWindows(sweepBounds)
                .OrderByDescending(w => WindowManager.Instance.GetWindowZIndex(w));

            if (Parent == null)
            {
                // 外にいる場合の処理
                foreach (var window in intersectingWindows)
                {
                    if (bounds.Bottom >= window.CollisionBounds.Top &&
                        bounds.Bottom <= window.CollisionBounds.Top + 5)
                    {
                        bool isGroundValid = true;
                        foreach (var otherWindow in intersectingWindows)
                        {
                            if (WindowManager.Instance.GetWindowZIndex(otherWindow) >
                                WindowManager.Instance.GetWindowZIndex(window) &&
                                otherWindow.CollisionBounds.IntersectsWith(currentFeetBounds))
                            {
                                isGroundValid = false;
                                break;
                            }
                        }

                        if (isGroundValid)
                        {
                            HandleGrounding(window.CollisionBounds.Top, wasGrounded);
                            return;
                        }
                    }
                }
            }
            else
            {
                // ウィンドウ内にいる場合の処理
                // プレイヤーの足元の領域を分割して各部分をチェック
                int segmentWidth = currentFeetBounds.Width / 4;
                int highestGroundY = int.MaxValue;
                GameWindow? groundWindow = null;

                // 足元の各セグメントでチェック
                for (int i = 0; i < 4; i++)
                {
                    Rectangle segmentBounds = new Rectangle(
                        currentFeetBounds.X + (i * segmentWidth),
                        currentFeetBounds.Y,
                        segmentWidth,
                        currentFeetBounds.Height
                    );

                    // 各セグメントで最前面のウィンドウのみを検出
                    GameWindow? topWindow = null;
                    foreach (var window in intersectingWindows)
                    {
                        Rectangle windowGroundArea = new Rectangle(
                            window.AdjustedBounds.Left,
                            window.AdjustedBounds.Bottom - 5,
                            window.AdjustedBounds.Width,
                            10
                        );

                        if (segmentBounds.IntersectsWith(windowGroundArea))
                        {
                            bool isTopWindow = true;
                            foreach (var otherWindow in intersectingWindows)
                            {
                                if (WindowManager.Instance.GetWindowZIndex(otherWindow) >
                                    WindowManager.Instance.GetWindowZIndex(window))
                                {
                                    Rectangle otherArea = otherWindow.AdjustedBounds;
                                    if (otherArea.IntersectsWith(segmentBounds))
                                    {
                                        isTopWindow = false;
                                        break;
                                    }
                                }
                            }

                            if (isTopWindow)
                            {
                                topWindow = window;
                                break;
                            }
                        }
                    }

                    if (topWindow != null)
                    {
                        int groundY = topWindow.AdjustedBounds.Bottom;
                        if (groundY < highestGroundY)
                        {
                            highestGroundY = groundY;
                            groundWindow = topWindow;
                        }
                    }
                }

                // 有効な地面が見つかった場合
                if (groundWindow != null && highestGroundY < int.MaxValue)
                {
                    HandleGrounding(highestGroundY, wasGrounded);
                    return;
                }
            }

            // メインフォームとの判定
            if (Program.mainForm != null && bounds.Bottom >= Program.mainForm.ClientSize.Height)
            {
                HandleGrounding(Program.mainForm.ClientSize.Height, wasGrounded);
            }
        }
        private void HandleGrounding(int groundY, bool wasGrounded)
        {
            IsGrounded = true;
            bounds.Y = groundY - bounds.Height;
            this.Location = bounds.Location;

            if (!wasGrounded)
            {
                verticalVelocity = 0;
                SetState(new NormalState());
            }
        }
        private void OnPaint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // 本体の描画
            using (var brush = new SolidBrush(Color.Blue))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }

            // アウトラインの描画
            if (Parent != null)
            {
                var outlineColor = OutlineRenderer.CalculateOutlineColor(Parent.BackColor);
                OutlineRenderer.DrawFormOutline(e.Graphics,
                    new Rectangle(0, 0, Width - 1, Height - 1), outlineColor);
            }

            // 状態に応じた描画
            currentState.Draw(this, e.Graphics);
        }
        public override void Draw(Graphics g)
        {
        }
        public override void SetParent(GameWindow? newParent)
        {
            if (Parent != null)
            {
                lastValidParent = Parent;
                Parent.RemoveChild(this);
            }

            Parent = newParent;
            Parent?.AddChild(this);

            if (Parent != null)
            {
                OnEnterWindow(Parent);
                UpdateMovableRegion(WindowManager.Instance.CalculateMovableRegion(Parent));
            }
            else
            {
                movableRegion.Dispose();
                movableRegion = new Region();
            }
        }
        public void UpdateMovableRegion(Region newRegion)
        {
            movableRegion.Dispose();
            movableRegion = newRegion;
        }
        public void ResetPosition(Point position)
        {
            bounds.Location = position;
            this.Location = position;
            verticalVelocity = 0;
            SetState(new NormalState());
            IsGrounded = false;
        }
        public void ResetSize(Size size)
        {
            bounds.Size = size;
            this.Size = size;
            originalSize = size;
        }
        public override void UpdateTargetPosition(Point newPosition)
        {
            bounds.Location = newPosition;
            this.Location = newPosition;
        }
        public override void UpdateTargetSize(Size newSize)
        {
            bounds.Size = newSize;
            this.Size = newSize;
            AdjustPositionAfterResize(newSize);
        }
        private void AdjustPositionAfterResize(Size newSize)
        {
            using (var validRegion = GetValidRegion())
            {
                Rectangle newBounds = new Rectangle(
                    bounds.X,
                    bounds.Y,
                    newSize.Width,
                    newSize.Height
                );

                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    if (!IsCompletelyInside(newBounds, validRegion, g))
                    {
                        newBounds = new Rectangle(
                            Math.Max(Parent?.AdjustedBounds.Left ?? 0,
                                Math.Min(bounds.X, (Parent?.AdjustedBounds.Right ?? Program.mainForm.ClientSize.Width) - newSize.Width)),
                            Math.Max(Parent?.AdjustedBounds.Top ?? 0,
                                Math.Min(bounds.Y, (Parent?.AdjustedBounds.Bottom ?? Program.mainForm.ClientSize.Height) - newSize.Height)),
                            newSize.Width,
                            newSize.Height
                        );
                    }
                }

                bounds = newBounds;
                UpdateMovableRegion(validRegion.Clone());
            }
        }
        private Region GetValidRegion()
        {
            if (Parent != null)
            {
                return WindowManager.Instance.CalculateMovableRegion(Parent);
            }
            else if (Program.mainForm != null)
            {
                return new Region(new Rectangle(0, 0,
                    Program.mainForm.ClientSize.Width,
                    Program.mainForm.ClientSize.Height));
            }
            return new Region();
        }
        public override void OnMinimize()
        {
            IsMinimized = true;
            minimizedTime = DateTime.Now;
            this.WindowState = FormWindowState.Minimized;

            if (Parent != null)
            {
                lastValidParent = Parent;
                Parent.RemoveChild(this);
                Parent = null;
            }
        }
        public override void OnRestore()
        {
            IsMinimized = false;
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();

            if (lastValidParent != null &&
                !lastValidParent.IsMinimized &&
                lastValidParent.AdjustedBounds.IntersectsWith(bounds))
            {
                SetParent(lastValidParent);
            }
            else
            {
                var newParent = WindowManager.Instance.GetTopWindowAt(bounds, null);
                SetParent(newParent);
            }
        }
        public override Size GetOriginalSize() => Bounds.Size;
        private void UpdatePosition(Point newPosition)
        {
            bounds.Location = newPosition;
            this.Location = newPosition;
        }
        public void SetState(IPlayerState newState)
        {
            currentState = newState;
            this.Invalidate();
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WindowMessages.WM_MOUSEACTIVATE:
                    m.Result = (IntPtr)WindowMessages.MA_NOACTIVATE;
                    return;
                case WindowMessages.WM_SYSCOMMAND:
                    int command = m.WParam.ToInt32() & 0xFFF0;
                    if (command == WindowMessages.SC_MINIMIZE)
                    {
                        OnMinimize();
                    }
                    else if (command == WindowMessages.SC_RESTORE)
                    {
                        OnRestore();
                    }
                    break;
            }
            base.WndProc(ref m);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000;  // WS_EX_LAYERED
                cp.ExStyle |= 0x20;     // WS_EX_TRANSPARENT
                return cp;
            }
        }

        private bool IsValidMove(Rectangle newBounds, Graphics g)
        {
            if (NoEntryZoneManager.Instance.IntersectsWithAnyZone(newBounds))
            {
                return false;
            }

            if (Parent == null)
            {
                return IsWithinMainForm(newBounds);
            }
            else
            {
                if (!movableRegion.IsEmpty(g))
                {
                    return IsCompletelyInside(newBounds, movableRegion, g);
                }
                return IsWithinMainForm(newBounds);
            }
        }

        private bool IsWithinMainForm(Rectangle bounds)
        {
            if (Program.mainForm != null)
            {
                return bounds.Left >= 0 &&
                       bounds.Right <= Program.mainForm.ClientSize.Width &&
                       bounds.Top >= 0 &&
                       bounds.Bottom <= Program.mainForm.ClientSize.Height;
            }
            return false;
        }

        private bool IsCompletelyInside(Rectangle bounds, Region region, Graphics g)
        {
            return region.IsVisible(bounds.Left, bounds.Top, g) &&
                   region.IsVisible(bounds.Right - 1, bounds.Top, g) &&
                   region.IsVisible(bounds.Left, bounds.Bottom - 1, g) &&
                   region.IsVisible(bounds.Right - 1, bounds.Bottom - 1, g);
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

        public override void ApplyEffect(IWindowEffect effect)
        {
            if (!CanReceiveEffect(effect)) return;

            switch (effect)
            {
                case MovementEffect moveEffect:
                    var newPos = new Point(
                        bounds.X + (int)moveEffect.CurrentMovement.X,
                        bounds.Y + (int)moveEffect.CurrentMovement.Y
                    );
                    UpdatePosition(newPos);
                    break;

                case ResizeEffect resizeEffect:
                    var scale = resizeEffect.GetCurrentScale(this);
                    var newSize = new Size(
                        (int)(originalSize.Width * scale.Width),
                        (int)(originalSize.Height * scale.Height)
                    );
                    bounds.Size = newSize;
                    this.Size = newSize;
                    break;
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                WindowManager.Instance.UnregisterFormOrder(this);
                GameSettings.Instance.SettingsChanged -= OnSettingsChanged;
                movableRegion?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}