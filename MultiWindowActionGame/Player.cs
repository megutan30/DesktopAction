using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace MultiWindowActionGame
{
    public class Player :IEffectTarget
    {
        private class PlayerSavedState
        {
            public Rectangle Bounds { get; set; }
            public float VerticalVelocity { get; set; }
            public GameWindow? Parent { get; set; }
            public IPlayerState State { get; set; }
        }

        private PlayerSavedState? savedState = null;
        private Rectangle bounds;
        public Rectangle Bounds => bounds;
        private float speed = 400.0f;
        private float gravity = 1000.0f;
        private float jumpForce = 500;
        private float verticalVelocity = 0;
        public float VerticalVelocity => verticalVelocity;
        public bool IsMinimized { get; private set; }

        public Size OriginalSize { get; }
        private Size referenceSize;
        private IPlayerState currentState;
        public GameWindow? Parent { get; private set; }
        public ICollection<IEffectTarget> Children { get; } = new HashSet<IEffectTarget>();
        public bool IsGrounded { get; private set; }
        private GameWindow? lastValidParent;
        public GameWindow? LastValidParent => lastValidParent;
        public Region MovableRegion { get; private set; }
        private DateTime? minimizedTime;
        public TimeSpan TimeSinceMinimized =>
            minimizedTime.HasValue ? DateTime.Now - minimizedTime.Value : TimeSpan.MaxValue;
        public Player(Point startPosition)
        {
            OriginalSize = new Size(40, 40);
            referenceSize = OriginalSize;
            bounds = new Rectangle(startPosition, OriginalSize);
            currentState = new NormalState();
            MovableRegion = new Region();
        }
        private void SaveState()
        {
            savedState = new PlayerSavedState
            {
                Bounds = this.bounds,
                VerticalVelocity = this.verticalVelocity,
                Parent = this.Parent,
                State = this.currentState
            };
        }
        public void ResetPosition(Point position)
        {
            bounds.Location = position;
            verticalVelocity = 0;
            SetState(new NormalState());
            IsGrounded = false;
        }
        public void ResetSize(Size size)
        {
            bounds.Size = size;
            referenceSize = size;
        }
        private void RestoreState()
        {
            if (savedState != null)
            {
                this.bounds = savedState.Bounds;
                this.verticalVelocity = savedState.VerticalVelocity;
                this.Parent = savedState.Parent;
                this.currentState = savedState.State;
                savedState = null;
            }
        }
        public void UpdatePosition(Point newPosition)
        {
            bounds.Location = newPosition;
        }

        public void UpdateSize(Size newSize)
        {
            bounds.Size = newSize;
            AdjustPositionAfterResize(newSize);
        }

        public void UpdateMovableRegion(Region newRegion)
        {
            MovableRegion.Dispose();
            MovableRegion = newRegion;
        }
        public void UpdateTargetSize(Size newSize)
        {
            this.UpdateSize(newSize); 
            AdjustPositionAfterResize(newSize);
        }
        public void UpdateTargetPosition(Point newPosition)
        {
            this.UpdatePosition(newPosition);
        }
        private bool IsCompletelyInside(Rectangle bounds, Region region, Graphics g)
        {
            return region.IsVisible(bounds.Left, bounds.Top, g) &&
                   region.IsVisible(bounds.Right - 1, bounds.Top, g) &&
                   region.IsVisible(bounds.Left, bounds.Bottom - 1, g) &&
                   region.IsVisible(bounds.Right - 1, bounds.Bottom - 1, g);
        }
        private bool isVisible = true;
        public bool IsVisible
        {
            get => isVisible;
            private set => isVisible = value;
        }

        public void Hide()
        {
            isVisible = false;
        }

        public void Show()
        {
            isVisible = true;
        }
        public void OnMinimize()
        {
            IsMinimized = true;
            minimizedTime = DateTime.Now;
            Hide();
            SaveState();

            // 親との関係を解除
            if (Parent != null)
            {
                Parent.RemoveChild(this);
            }
        }

        public void OnRestore()
        {
            IsMinimized = false;
            Show();
            RestoreState();

            UpdateParentOnRestore();
        }
        private void UpdateParentOnRestore()
        {
            var potentialParent = WindowManager.Instance.GetTopWindowAt(bounds,Parent);
            if (potentialParent != null && !potentialParent.IsMinimized)
            {
                SetParent(potentialParent);
            }
            else
            {
                // デスクトップ上での移動を可能にする
                SetParent(null);
                MovableRegion = new Region(new Rectangle(0, 0,
                    Program.mainForm?.ClientSize.Width ?? 0,
                    Program.mainForm?.ClientSize.Height ?? 0));
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

        private bool IsValidMove(Rectangle bounds, Graphics g)
        {
            // まず不可侵領域との判定を行う
            if (NoEntryZoneManager.Instance.IntersectsWithAnyZone(bounds))
            {
                return false;
            }
            if (Parent == null)
            {
                // ウィンドウの外にいる場合はメインフォームの境界のみをチェック
                return IsWithinMainForm(bounds);
            }
            else
            {
                // ウィンドウ内にいる場合は、完全に内部に入っているときのみMovableRegionをチェック
                if (!MovableRegion.IsEmpty(g))
                {
                    return IsCompletelyInside(bounds, MovableRegion, g);
                }
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
            // はざまにいる場合は現在の親を維持
            if (newParent == null && Parent != null)
            {
                // 現在位置が完全にデスクトップ上にある場合のみ親をnullに
                var intersectingWindows = WindowManager.Instance
                    .GetIntersectingWindows(bounds)
                    .Where(w => w.AdjustedBounds.IntersectsWith(bounds));

                if (intersectingWindows.Any())
                {
                    // ウィンドウと交差している場合は現在の親を維持
                    return;
                }
            }

            if (Parent != null)
            {
                lastValidParent = Parent;
            }

            Parent?.RemoveChild(this);
            Parent = newParent;
            Parent?.AddChild(this);

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

            if (Input.IsKeyDown(Keys.A)|| Input.IsKeyDown(Keys.Left))
            {
                movement.X -= speed * deltaTime;
            }
            if (Input.IsKeyDown(Keys.D) || Input.IsKeyDown(Keys.Right))
            {
                movement.X += speed * deltaTime;
            }

            if ((Input.IsKeyDown(Keys.Space) || Input.IsKeyDown(Keys.Up)) && IsGrounded)
            {
                Jump();
            }

            movement.Y += verticalVelocity * deltaTime;

            return movement;
        }

        public async Task UpdateAsync(float deltaTime)
        {
            if (IsMinimized) return;
            currentState.HandleInput(this);
            currentState.Update(this, deltaTime);

            Vector2 movement = CalculateMovement(deltaTime);
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

            // プレイヤーが外にいる場合の処理
            if (Parent == null)
            {
                // ウィンドウとの衝突判定と位置調整
                newBounds = HandleWindowCollisions(newBounds);

                // プレイヤーが完全にウィンドウ内に包含されているかチェック
                GameWindow? coveringWindow = WindowManager.Instance.GetWindowFullyContaining(newBounds);
                if (coveringWindow != null)
                {
                    // プレイヤーがウィンドウに完全に覆われた場合、そのウィンドウの子になる
                    float previousVelocity = verticalVelocity;
                    SetParent(coveringWindow);
                    verticalVelocity = previousVelocity;
                    UpdateMovableRegion(WindowManager.Instance.CalculateMovableRegion(coveringWindow));
                }
            }
            else
            {      
                // 移動前に新しい位置でのウィンドウをチェック
                GameWindow? newWindow = WindowManager.Instance.GetTopWindowAt(newBounds, Parent);

                bool isCompletelyInsideNewWindow = newWindow != null &&
                newWindow.AdjustedBounds.Contains(newBounds);

                if (newWindow != Parent)
                {
                    if(Parent != null)
                    {
                        // 現在のウィンドウから別のウィンドウに移動するケース
                        if (isCompletelyInsideNewWindow)
                        {
                            // 新しいウィンドウに完全に入っている場合は親を変更
                            float previousVelocity = verticalVelocity;
                            SetParent(newWindow);
                            verticalVelocity = previousVelocity;
                            UpdateMovableRegion(WindowManager.Instance.CalculateMovableRegion(newWindow));
                        }
                    }
                }
            }

            bounds = newBounds;

            if (currentState.ShouldCheckGround)
            {
                CheckGrounded();
            }
        }
        private Rectangle HandleWindowCollisions(Rectangle newBounds)
        {
            var adjustedBounds = newBounds;

            // 不可侵領域との衝突チェック
            adjustedBounds = NoEntryZoneManager.Instance.GetValidPosition(bounds, adjustedBounds);

            // ウィンドウとの衝突チェック
            var intersectingWindows = WindowManager.Instance.GetIntersectingWindows(adjustedBounds);
            foreach (var window in intersectingWindows)
            {
                Rectangle windowBounds = window.CollisionBounds;

                // 上からの衝突（タイトルバーを含む）
                if (bounds.Bottom <= windowBounds.Top && adjustedBounds.Bottom > windowBounds.Top)
                {
                    adjustedBounds.Y = windowBounds.Top - adjustedBounds.Height;
                    verticalVelocity = 0;
                    IsGrounded = true;
                }
                // 下からの衝突
                else if (bounds.Top >= windowBounds.Bottom && adjustedBounds.Top < windowBounds.Bottom)
                {
                    adjustedBounds.Y = windowBounds.Bottom;
                    verticalVelocity = 0;
                }
                // 左からの衝突
                else if (bounds.Right <= windowBounds.Left && adjustedBounds.Right > windowBounds.Left)
                {
                    adjustedBounds.X = windowBounds.Left - adjustedBounds.Width;
                }
                // 右からの衝突
                else if (bounds.Left >= windowBounds.Right && adjustedBounds.Left < windowBounds.Right)
                {
                    adjustedBounds.X = windowBounds.Right;
                }
            }

            return adjustedBounds;
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
        private Region GetValidRegion()
        {
            if (Parent != null)
            {
                // 親ウィンドウと重なっているウィンドウすべての領域を取得
                return WindowManager.Instance.CalculateMovableRegion(Parent);
            }
            else if (Program.mainForm != null)
            {
                // メインフォームの領域を返す
                return new Region(new Rectangle(0, 0,
                    Program.mainForm.ClientSize.Width,
                    Program.mainForm.ClientSize.Height));
            }
            return new Region();
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

                // 現在位置で収まらない場合、有効な位置を探す
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    if (!IsCompletelyInside(newBounds, validRegion, g))
                    {
                        // まず現在の位置で調整を試みる
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
        public void Draw(Graphics g)
        {
            if(IsMinimized)return;
            currentState.Draw(this, g);

            if (MainGame.IsDebugMode)
            {
                DrawDebugInfo(g);
            }
        }

        public void DrawDebugInfo(Graphics g)
        {
            g.DrawRectangle(new Pen(Color.Yellow, 2), bounds);
            if (Parent != null)
            {
                g.DrawString($"Parent: {Parent.Id}", SystemFonts.DefaultFont, Brushes.Yellow, 
                    bounds.X, bounds.Y - 20);

                //System.Diagnostics.Debug.WriteLine($"Parent: {Parent.Id}");
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
            if(currentState is JumpingState)return;
            bool wasGrounded = IsGrounded;
            IsGrounded = false;

            var windowManager = WindowManager.Instance;
            var noEntryManager = NoEntryZoneManager.Instance;

            // プレイヤーの足元の現在の範囲を作成
            Rectangle currentFeetBounds = new Rectangle(
                bounds.X,
                bounds.Bottom - 25,
                bounds.Width,
                30
            );

            // 移動量に基づいて拡張された判定領域を作成（より大きな範囲をカバー）
            float maxVerticalStep = Math.Max(Math.Abs(verticalVelocity * GameTime.DeltaTime), 20f); // 最小20ピクセルの判定範囲を確保
            Rectangle sweepBounds = new Rectangle(
                currentFeetBounds.X,
                Math.Min(currentFeetBounds.Y, currentFeetBounds.Y + (int)(verticalVelocity * GameTime.DeltaTime)) - 5, // 上側にも少し余裕を持たせる
                currentFeetBounds.Width,
                (int)maxVerticalStep + currentFeetBounds.Height + 10 // 下側にも余裕を持たせる
            );

            // 不可侵領域との接地判定
            foreach (var zone in noEntryManager.Zones)
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

            // 足元の範囲で交差判定を行う
            var intersectingWindows = windowManager.GetIntersectingWindows(sweepBounds)
                .OrderByDescending(w => windowManager.GetWindowZIndex(w));

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
                            if (windowManager.GetWindowZIndex(otherWindow) >
                                windowManager.GetWindowZIndex(window) &&
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
                            // このセグメントでより手前のウィンドウが見つかった場合、そちらを優先
                            bool isTopWindow = true;
                            foreach (var otherWindow in intersectingWindows)
                            {
                                if (windowManager.GetWindowZIndex(otherWindow) > windowManager.GetWindowZIndex(window))
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
                                break; // 最前面のウィンドウが見つかったら、このセグメントの探索を終了
                            }
                        }
                    }

                    // このセグメントで見つかった最前面のウィンドウの地面を候補として追加
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

            if (Program.mainForm != null && bounds.Bottom >= Program.mainForm.ClientSize.Height)
            {
                HandleGrounding(Program.mainForm.ClientSize.Height, wasGrounded);
            }
        }
        private void HandleGrounding(int groundY, bool wasGrounded)
        {
            IsGrounded = true;
            bounds = new Rectangle(
                bounds.X,
                groundY - bounds.Height,
                bounds.Width,
                bounds.Height
            );

            if (!wasGrounded)
            {
                verticalVelocity = 0;
                SetState(new NormalState());
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