// Core/Entities/ConcreteEntities.cs
using MultiWindowActionGame.Core.Interfaces;
using MultiWindowActionGame.Core.Constants;
using System.Numerics;

namespace MultiWindowActionGame.Core.Entities
{
    /// <summary>
    /// ゲームウィンドウの実装
    /// </summary>
    public class GameWindow : BaseGameEntity, IGameWindow, IHierarchical<IGameWindow>, ICollidable, IZOrderable
    {
        #region Fields
        private readonly HashSet<IGameWindow> _children = new();
        private IGameWindow? _parent;
        private WindowStrategyType _strategyType;
        private IWindowStrategy _strategy;
        private Rectangle _clientBounds;
        private Rectangle _adjustedBounds;
        private Size _originalSize;
        private int _zOrder;
        private ZOrderPriority _priority = ZOrderPriority.Window;
        private bool _canEnter = true;
        private bool _canExit = true;
        private string? _displayText;
        #endregion

        #region Properties
        public override EntityType EntityType => EntityType.Window;
        public WindowStrategyType StrategyType => _strategyType;
        public IReadOnlyCollection<IGameWindow> Children => _children;
        public IGameWindow? Parent => _parent;
        public Rectangle ClientBounds => _clientBounds;
        public Rectangle AdjustedBounds => _adjustedBounds;
        public Size OriginalSize => _originalSize;
        public bool CanEnter { get => _canEnter; set => _canEnter = value; }
        public bool CanExit { get => _canExit; set => _canExit = value; }
        public string? DisplayText { get => _displayText; set => _displayText = value; }

        // ICollidable
        public Rectangle CollisionBounds => _adjustedBounds;
        public bool IsSolid => true;
        public CollisionLayer CollisionLayer => CollisionLayer.Window;

        // IZOrderable
        public int ZOrder => _zOrder;
        public ZOrderPriority Priority => _priority;
        #endregion

        #region Events
        public event EventHandler<WindowMovedEventArgs>? WindowMoved;
        public event EventHandler<WindowResizedEventArgs>? WindowResized;
        public event EventHandler<HierarchyChangedEventArgs<IGameWindow>>? HierarchyChanged;
        public event EventHandler<CollisionEventArgs>? CollisionDetected;
        public event EventHandler<ZOrderChangedEventArgs>? ZOrderChanged;
        #endregion

        #region Constructor
        public GameWindow(Point location, Size size, IWindowStrategy strategy)
            : base(location, size)
        {
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            _strategyType = DetermineStrategyType(strategy);
            _originalSize = size;
            
            InitializeWindow();
            UpdateBounds();
        }

        private void InitializeWindow()
        {
            Name = $"GameWindow_{Id:N}[..8]";
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.ControlBox = true;
            this.MaximizeBox = false;
            this.MinimizeBox = _strategyType == WindowStrategyType.Minimizable;
        }

        private WindowStrategyType DetermineStrategyType(IWindowStrategy strategy)
        {
            return strategy.GetType().Name switch
            {
                nameof(MovableWindowStrategy) => WindowStrategyType.Movable,
                nameof(ResizableWindowStrategy) => WindowStrategyType.Resizable,
                nameof(MinimizableWindowStrategy) => WindowStrategyType.Minimizable,
                nameof(DeletableWindowStrategy) => WindowStrategyType.Deletable,
                nameof(TextDisplayWindowStrategy) => WindowStrategyType.TextDisplay,
                _ => WindowStrategyType.Normal
            };
        }
        #endregion

        #region IGameWindow Implementation
        public void BringToFront()
        {
            // Z-Orderサービス経由で実装
            var zOrderService = Program.ServiceContainer?.GetService<IZOrderService>();
            zOrderService?.BringToFront(this);
        }

        public void SendToBack()
        {
            var zOrderService = Program.ServiceContainer?.GetService<IZOrderService>();
            zOrderService?.SendToBack(this);
        }

        public void UpdateBounds()
        {
            // クライアント領域の計算
            _clientBounds = this.ClientRectangle;
            
            // 調整された境界の計算（マージンを考慮）
            _adjustedBounds = new Rectangle(
                _clientBounds.X + GameConstants.Window.MARGIN,
                _clientBounds.Y + GameConstants.Window.MARGIN,
                _clientBounds.Width - (2 * GameConstants.Window.MARGIN),
                _clientBounds.Height - (2 * GameConstants.Window.MARGIN)
            );

            MarkForRedraw();
        }

        public void ChangeStrategy(WindowStrategyType newStrategyType)
        {
            if (_strategyType == newStrategyType) return;

            _strategy = CreateStrategy(newStrategyType);
            _strategyType = newStrategyType;
            
            UpdateStrategySpecificProperties();
            OnStateChanged($"StrategyChanged_{newStrategyType}");
        }

        private IWindowStrategy CreateStrategy(WindowStrategyType strategyType)
        {
            return strategyType switch
            {
                WindowStrategyType.Normal => new NormalWindowStrategy(),
                WindowStrategyType.Movable => new MovableWindowStrategy(),
                WindowStrategyType.Resizable => new ResizableWindowStrategy(),
                WindowStrategyType.Minimizable => new MinimizableWindowStrategy(),
                WindowStrategyType.Deletable => new DeletableWindowStrategy(),
                WindowStrategyType.TextDisplay => new TextDisplayWindowStrategy(_displayText ?? ""),
                _ => new NormalWindowStrategy()
            };
        }

        private void UpdateStrategySpecificProperties()
        {
            this.MinimizeBox = _strategyType == WindowStrategyType.Minimizable;
            // その他の戦略固有のプロパティ更新
        }
        #endregion

        #region IHierarchical Implementation
        public void SetParent(IGameWindow? parent)
        {
            if (_parent == parent) return;

            var oldParent = _parent;
            
            // 古い親から削除
            if (_parent != null)
            {
                _parent.RemoveChild(this);
            }

            _parent = parent;

            // 新しい親に追加
            if (_parent != null)
            {
                _parent.AddChild(this);
            }

            OnHierarchyChanged(this, oldParent, _parent, HierarchyChangeType.ParentChanged);
        }

        public void AddChild(IGameWindow child)
        {
            if (child == null || _children.Contains(child) || child == this) return;

            _children.Add(child);
            if (child.Parent != this)
            {
                child.SetParent(this);
            }

            OnHierarchyChanged(child, null, this, HierarchyChangeType.ChildAdded);
        }

        public void RemoveChild(IGameWindow child)
        {
            if (child == null || !_children.Contains(child)) return;

            _children.Remove(child);
            if (child.Parent == this)
            {
                child.SetParent(null);
            }

            OnHierarchyChanged(child, this, null, HierarchyChangeType.ChildRemoved);
        }

        public bool IsAncestorOf(IGameWindow other)
        {
            var current = other.Parent;
            while (current != null)
            {
                if (current == this) return true;
                current = current.Parent;
            }
            return false;
        }

        public bool IsDescendantOf(IGameWindow other)
        {
            return other.IsAncestorOf(this);
        }

        public IGameWindow GetRoot()
        {
            var current = this;
            while (current.Parent != null)
            {
                current = current.Parent;
            }
            return current;
        }

        public int GetDepth()
        {
            int depth = 0;
            var current = _parent;
            while (current != null)
            {
                depth++;
                current = current.Parent;
            }
            return depth;
        }
        #endregion

        #region ICollidable Implementation
        public bool CheckCollision(ICollidable other)
        {
            return CollisionBounds.IntersectsWith(other.CollisionBounds);
        }

        public bool CheckCollision(Rectangle bounds)
        {
            return CollisionBounds.IntersectsWith(bounds);
        }

        public new bool Contains(Point point)
        {
            return CollisionBounds.Contains(point);
        }
        #endregion

        #region IZOrderable Implementation
        public void SetZOrder(int zOrder)
        {
            if (_zOrder == zOrder) return;

            var oldZOrder = _zOrder;
            _zOrder = zOrder;
            
            OnZOrderChanged(oldZOrder, zOrder);
        }
        #endregion

        #region Protected Methods
        protected override void DrawEntity(Graphics graphics)
        {
            // 戦略パターンによる描画委譲
            _strategy?.Draw(this, graphics);
        }

        protected override async Task UpdateEntityAsync(float deltaTime)
        {
            // 戦略パターンによる更新委譲
            if (_strategy != null)
            {
                await Task.Run(() => _strategy.Update(this, deltaTime));
            }

            // 子ウィンドウの更新
            var childUpdateTasks = _children.Select(child => child.UpdateAsync(deltaTime));
            await Task.WhenAll(childUpdateTasks);
        }

        protected override void OnInputReceived(Keys key, bool isPressed)
        {
            _strategy?.HandleInput(this, key, isPressed);
        }

        protected override void OnMouseInputReceived(Point mousePosition, MouseButtons button, bool isPressed)
        {
            _strategy?.HandleMouseInput(this, mousePosition, button, isPressed);
        }
        #endregion

        #region Event Raisers
        private void OnHierarchyChanged(IGameWindow child, IGameWindow? oldParent, IGameWindow? newParent, HierarchyChangeType changeType)
        {
            HierarchyChanged?.Invoke(this, new HierarchyChangedEventArgs<IGameWindow>(child, oldParent, newParent, changeType));
        }

        private void OnZOrderChanged(int oldZOrder, int newZOrder)
        {
            ZOrderChanged?.Invoke(this, new ZOrderChangedEventArgs(oldZOrder, newZOrder, _priority));
        }
        #endregion
    }

    /// <summary>
    /// プレイヤーの実装
    /// </summary>
    public class Player : BaseGameEntity, IPlayer, ICollidable
    {
        #region Fields
        private bool _isGrounded;
        private float _verticalVelocity;
        private IPlayerState _currentState;
        private IGameWindow? _lastValidParent;
        private Region _movableRegion;
        private readonly PlayerSettings _settings;
        #endregion

        #region Properties
        public override EntityType EntityType => EntityType.Player;
        public bool IsGrounded => _isGrounded;
        public float VerticalVelocity => _verticalVelocity;
        public IPlayerState CurrentState => _currentState;
        public IGameWindow? LastValidParent => _lastValidParent;
        public Region MovableRegion => _movableRegion;

        public Rectangle GroundCheckArea => new Rectangle(
            Bounds.X,
            Bounds.Bottom - _settings.GroundCheckHeight,
            Bounds.Width,
            _settings.GroundCheckHeight);

        // ICollidable
        public Rectangle CollisionBounds => Bounds;
        public bool IsSolid => false;
        public CollisionLayer CollisionLayer => CollisionLayer.Player;
        #endregion

        #region Events
        public event EventHandler<PlayerStateChangedEventArgs>? PlayerStateChanged;
        public event EventHandler<PlayerJumpedEventArgs>? Jumped;
        public event EventHandler<PlayerLandedEventArgs>? Landed;
        public event EventHandler<CollisionEventArgs>? CollisionDetected;
        #endregion

        #region Constructor
        public Player(Point startPosition, Size size) : base(startPosition, size)
        {
            _settings = GetPlayerSettings();
            _currentState = new NormalState();
            _movableRegion = new Region(Bounds);
            
            InitializePlayer();
        }

        private PlayerSettings GetPlayerSettings()
        {
            // 設定サービスから取得
            var settingsService = Program.ServiceContainer?.GetService<ISettingsService>();
            return new PlayerSettings
            {
                MovementSpeed = settingsService?.GetSetting("Player.MovementSpeed", GameConstants.Player.DEFAULT_MOVEMENT_SPEED) ?? GameConstants.Player.DEFAULT_MOVEMENT_SPEED,
                Gravity = settingsService?.GetSetting("Player.Gravity", GameConstants.Player.DEFAULT_GRAVITY) ?? GameConstants.Player.DEFAULT_GRAVITY,
                JumpForce = settingsService?.GetSetting("Player.JumpForce", GameConstants.Player.DEFAULT_JUMP_FORCE) ?? GameConstants.Player.DEFAULT_JUMP_FORCE,
                GroundCheckHeight = settingsService?.GetSetting("Player.GroundCheckHeight", GameConstants.Player.GROUND_CHECK_HEIGHT) ?? GameConstants.Player.GROUND_CHECK_HEIGHT
            };
        }

        private void InitializePlayer()
        {
            Name = "Player";
            this.BackColor = GameConstants.Colors.PLAYER_BLUE;
            this.TransparencyKey = Color.Magenta;
        }
        #endregion

        #region IPlayer Implementation
        public void ResetPosition(Point position)
        {
            SetPosition(position);
            _verticalVelocity = 0;
            _isGrounded = false;
            ChangeState(new NormalState());
        }

        public void ResetSize(Size size)
        {
            SetSize(size);
        }

        public void Jump()
        {
            if (!_isGrounded) return;

            _verticalVelocity = -_settings.JumpForce;
            _isGrounded = false;
            ChangeState(new JumpingState());

            Jumped?.Invoke(this, new PlayerJumpedEventArgs(_settings.JumpForce, Position));
        }

        public void ChangeState(IPlayerState newState)
        {
            if (_currentState == newState) return;

            var oldState = _currentState;
            _currentState?.OnExit(this);
            _currentState = newState;
            _currentState?.OnEnter(this);

            PlayerStateChanged?.Invoke(this, new PlayerStateChangedEventArgs(oldState, newState));
            OnStateChanged(newState.StateName);
        }

        public void UpdateMovableRegion(Region newRegion)
        {
            _movableRegion?.Dispose();
            _movableRegion = newRegion;
        }

        public void SetParentWindow(IGameWindow? parent)
        {
            if (parent != null)
            {
                _lastValidParent = parent;
            }
            // 実際の親子関係の設定はWindowManagerが管理
        }
        #endregion

        #region ICollidable Implementation
        public bool CheckCollision(ICollidable other)
        {
            var hasCollision = CollisionBounds.IntersectsWith(other.CollisionBounds);
            if (hasCollision)
            {
                var intersectionArea = Rectangle.Intersect(CollisionBounds, other.CollisionBounds);
                CollisionDetected?.Invoke(this, new CollisionEventArgs(other, intersectionArea, CollisionType.Stay));
            }
            return hasCollision;
        }

        public bool CheckCollision(Rectangle bounds)
        {
            return CollisionBounds.IntersectsWith(bounds);
        }

        public new bool Contains(Point point)
        {
            return CollisionBounds.Contains(point);
        }
        #endregion

        #region Protected Methods
        protected override void DrawEntity(Graphics graphics)
        {
            // プレイヤーの基本描画
            using (var brush = new SolidBrush(GameConstants.Colors.PLAYER_BLUE))
            {
                graphics.FillRectangle(brush, ClientRectangle);
            }

            // 状態による追加描画
            _currentState?.Draw(this, graphics);

            // アウトライン描画（親ウィンドウがある場合）
            if (_lastValidParent != null)
            {
                var outlineColor = OutlineRenderer.CalculateOutlineColor(_lastValidParent.BackColor);
                using (var pen = new Pen(outlineColor, 2))
                {
                    graphics.DrawRectangle(pen, new Rectangle(0, 0, Width - 1, Height - 1));
                }
            }
        }

        protected override async Task UpdateEntityAsync(float deltaTime)
        {
            if (IsMinimized) return;

            // 入力処理
            HandleInput();

            // 状態更新
            _currentState?.Update(this, deltaTime);

            // 物理演算
            ApplyGravity(deltaTime);
            await HandleMovementAsync(deltaTime);

            // 接地判定
            CheckGrounded();
        }

        protected override void OnInputReceived(Keys key, bool isPressed)
        {
            _currentState?.HandleInput(this);
        }
        #endregion

        #region Private Methods
        private void HandleInput()
        {
            // ジャンプ入力
            if (_isGrounded && IsJumpKeyPressed())
            {
                Jump();
            }
        }

        private bool IsJumpKeyPressed()
        {
            var inputService = Program.ServiceContainer?.GetService<IInputService>();
            if (inputService != null)
            {
                return GameConstants.Input.JUMP_KEYS.Any(key => inputService.IsKeyPressed(key));
            }

            // フォールバック: 直接入力チェック
            return GameConstants.Input.JUMP_KEYS.Any(key => Input.IsKeyDown(key));
        }

        private void ApplyGravity(float deltaTime)
        {
            if (!_isGrounded)
            {
                _verticalVelocity += _settings.Gravity * deltaTime;
            }
            else
            {
                _verticalVelocity = 0;
            }
        }

        private async Task HandleMovementAsync(float deltaTime)
        {
            var movement = CalculateMovement(deltaTime);
            var proposedBounds = new Rectangle(
                Bounds.X + (int)movement.X,
                Bounds.Y + (int)movement.Y,
                Bounds.Width,
                Bounds.Height);

            // 移動の妥当性チェック
            if (IsValidMove(proposedBounds))
            {
                SetPosition(proposedBounds.Location);
            }
        }

        private Vector2 CalculateMovement(float deltaTime)
        {
            var movement = Vector2.Zero;
            var inputService = Program.ServiceContainer?.GetService<IInputService>();

            if (inputService != null)
            {
                if (GameConstants.Input.MOVEMENT_LEFT.Any(key => inputService.IsKeyDown(key)))
                {
                    movement.X -= _settings.MovementSpeed * deltaTime;
                }
                if (GameConstants.Input.MOVEMENT_RIGHT.Any(key => inputService.IsKeyDown(key)))
                {
                    movement.X += _settings.MovementSpeed * deltaTime;
                }
            }
            else
            {
                // フォールバック
                if (GameConstants.Input.MOVEMENT_LEFT.Any(key => Input.IsKeyDown(key)))
                {
                    movement.X -= _settings.MovementSpeed * deltaTime;
                }
                if (GameConstants.Input.MOVEMENT_RIGHT.Any(key => Input.IsKeyDown(key)))
                {
                    movement.X += _settings.MovementSpeed * deltaTime;
                }
            }

            movement.Y += _verticalVelocity * deltaTime;
            return movement;
        }

        private bool IsValidMove(Rectangle newBounds)
        {
            // 不可侵領域チェック
            var noEntryService = Program.ServiceContainer?.GetService<INoEntryZoneService>();
            if (noEntryService?.IntersectsWithAnyZone(newBounds) == true)
            {
                return false;
            }

            // 移動可能領域チェック
            if (!_movableRegion.IsEmpty(Graphics.FromHwnd(IntPtr.Zero)))
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    return _movableRegion.IsVisible(newBounds, g);
                }
            }

            return true;
        }

        private void CheckGrounded()
        {
            if (_currentState is JumpingState) return;

            var wasGrounded = _isGrounded;
            _isGrounded = false;

            // 地面チェックロジック（簡略化）
            var groundCheckArea = GroundCheckArea;
            var windowManager = Program.ServiceContainer?.GetService<IWindowManagerService>();
            
            if (windowManager != null)
            {
                var intersectingWindows = windowManager.GetAllWindows()
                    .Where(w => w.CollisionBounds.IntersectsWith(groundCheckArea));

                foreach (var window in intersectingWindows)
                {
                    if (Bounds.Bottom >= window.CollisionBounds.Top - GameConstants.Physics.GROUND_CHECK_TOLERANCE_Y &&
                        Bounds.Bottom <= window.CollisionBounds.Top + GameConstants.Physics.GROUND_CHECK_TOLERANCE_Y)
                    {
                        _isGrounded = true;
                        SetPosition(new Point(Position.X, window.CollisionBounds.Top - Bounds.Height));
                        
                        if (!wasGrounded)
                        {
                            Landed?.Invoke(this, new PlayerLandedEventArgs(Position, window));
                            ChangeState(new NormalState());
                        }
                        break;
                    }
                }
            }

            // メインフォーム底面チェック
            if (!_isGrounded && Program.mainForm != null)
            {
                if (Bounds.Bottom >= Program.mainForm.ClientSize.Height)
                {
                    _isGrounded = true;
                    SetPosition(new Point(Position.X, Program.mainForm.ClientSize.Height - Bounds.Height));
                    
                    if (!wasGrounded)
                    {
                        Landed?.Invoke(this, new PlayerLandedEventArgs(Position, null));
                        ChangeState(new NormalState());
                    }
                }
            }
        }
        #endregion

        #region Nested Classes
        private class PlayerSettings
        {
            public float MovementSpeed { get; set; }
            public float Gravity { get; set; }
            public float JumpForce { get; set; }
            public int GroundCheckHeight { get; set; }
        }
        #endregion
    }

    /// <summary>
    /// ゴールの実装
    /// </summary>
    public class Goal : BaseGameEntity, IGoal, ICollidable
    {
        #region Fields
        private bool _isInFront;
        #endregion

        #region Properties
        public override EntityType EntityType => EntityType.Goal;
        public bool IsInFront { get => _isInFront; set => _isInFront = value; }

        // ICollidable
        public Rectangle CollisionBounds => Bounds;
        public bool IsSolid => false;
        public CollisionLayer CollisionLayer => CollisionLayer.Goal;
        #endregion

        #region Events
        public event EventHandler<GoalReachedEventArgs>? GoalReached;
        public event EventHandler<CollisionEventArgs>? CollisionDetected;
        #endregion

        #region Constructor
        public Goal(Point position, Size size, bool isInFront) : base(position, size)
        {
            _isInFront = isInFront;
            InitializeGoal();
        }

        private void InitializeGoal()
        {
            Name = "Goal";
            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;
            this.TopMost = _isInFront;
        }
        #endregion

        #region IGoal Implementation
        public bool CheckReached(IPlayer player)
        {
            var reached = CheckCollision(player as ICollidable);
            if (reached)
            {
                GoalReached?.Invoke(this, new GoalReachedEventArgs(player, this));
            }
            return reached;
        }
        #endregion

        #region ICollidable Implementation
        public bool CheckCollision(ICollidable other)
        {
            if (other == null) return false;
            
            var hasCollision = CollisionBounds.IntersectsWith(other.CollisionBounds);
            if (hasCollision)
            {
                var intersectionArea = Rectangle.Intersect(CollisionBounds, other.CollisionBounds);
                CollisionDetected?.Invoke(this, new CollisionEventArgs(other, intersectionArea, CollisionType.Enter));
            }
            return hasCollision;
        }

        public bool CheckCollision(Rectangle bounds)
        {
            return CollisionBounds.IntersectsWith(bounds);
        }

        public new bool Contains(Point point)
        {
            return CollisionBounds.Contains(point);
        }
        #endregion

        #region Protected Methods
        protected override void DrawEntity(Graphics graphics)
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // ゴールマークの描画
            var text = "G";
            var font = CreateScaledFont(graphics, text);

            using (font)
            {
                var textSize = graphics.MeasureString(text, font);
                var x = (Width - textSize.Width) / 2;
                var y = (Height - textSize.Height) / 2;

                // アウトライン描画
                var outlineColor = GetOutlineColor();
                DrawTextOutline(graphics, text, font, outlineColor, x, y);

                // メインテキスト描画
                using (var brush = new SolidBrush(GameConstants.Colors.GOAL_GOLD))
                {
                    graphics.DrawString(text, font, brush, x, y);
                }
            }
        }

        protected override async Task UpdateEntityAsync(float deltaTime)
        {
            // ゴールは基本的に静的なので特別な更新処理は不要
            await Task.CompletedTask;
        }
        #endregion

        #region Private Methods
        private Font CreateScaledFont(Graphics graphics, string text)
        {
            var fontFamily = SystemFonts.DefaultFont.FontFamily;
            var fontSize = Math.Min(Width, Height) * 0.8f;
            return new Font(fontFamily, fontSize, FontStyle.Bold);
        }

        private Color GetOutlineColor()
        {
            // 親ウィンドウがある場合はその背景色から計算
            var parentWindow = GetParentWindow();
            if (parentWindow != null)
            {
                return OutlineRenderer.CalculateOutlineColor(parentWindow.BackColor);
            }
            return Color.Black;
        }

        private IGameWindow? GetParentWindow()
        {
            var windowManager = Program.ServiceContainer?.GetService<IWindowManagerService>();
            return windowManager?.GetAllWindows().FirstOrDefault(w => w.CollisionBounds.Contains(Bounds));
        }

        private void DrawTextOutline(Graphics graphics, string text, Font font, Color outlineColor, float x, float y)
        {
            var offset = 2f;
            using (var brush = new SolidBrush(outlineColor))
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx != 0 || dy != 0)
                        {
                            graphics.DrawString(text, font, brush, x + dx * offset, y + dy * offset);
                        }
                    }
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// ゲームボタンの基底実装
    /// </summary>
    public abstract class BaseButton : BaseGameEntity, IGameButton, ICollidable
    {
        #region Fields
        private bool _isHovered;
        private bool _isEnabled = true;
        private string _text = "";
        #endregion

        #region Properties
        public override EntityType EntityType => EntityType.Button;
        public abstract ButtonType ButtonType { get; }
        public bool IsHovered => _isHovered;
        public bool IsEnabled { get => _isEnabled; set => _isEnabled = value; }
        public string Text { get => _text; set => _text = value; }

        // ICollidable
        public Rectangle CollisionBounds => Bounds;
        public bool IsSolid => false;
        public CollisionLayer CollisionLayer => CollisionLayer.Button;
        #endregion

        #region Events
        public event EventHandler<ButtonClickedEventArgs>? Clicked;
        public event EventHandler<ButtonHoverEventArgs>? HoverStateChanged;
        public event EventHandler<CollisionEventArgs>? CollisionDetected;
        #endregion

        #region Constructor
        protected BaseButton(Point location, Size size) : base(location, size)
        {
            InitializeButton();
        }

        private void InitializeButton()
        {
            this.MouseEnter += (s, e) => SetHovered(true);
            this.MouseLeave += (s, e) => SetHovered(false);
            this.MouseClick += OnMouseClick;
        }
        #endregion

        #region ICollidable Implementation
        public bool CheckCollision(ICollidable other)
        {
            return CollisionBounds.IntersectsWith(other.CollisionBounds);
        }

        public bool CheckCollision(Rectangle bounds)
        {
            return CollisionBounds.IntersectsWith(bounds);
        }

        public new bool Contains(Point point)
        {
            return CollisionBounds.Contains(point);
        }
        #endregion

        #region Protected Methods
        protected override void DrawEntity(Graphics graphics)
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // ボタン背景
            var backgroundColor = _isHovered && _isEnabled ? 
                GameConstants.Colors.BUTTON_HOVERED : 
                GameConstants.Colors.BUTTON_NORMAL;

            using (var brush = new SolidBrush(backgroundColor))
            {
                graphics.FillRectangle(brush, ClientRectangle);
            }

            // ボタン枠
            using (var pen = new Pen(GameConstants.Colors.BUTTON_BORDER, 2))
            {
                graphics.DrawRectangle(pen, 1, 1, Width - 2, Height - 2);
            }

            // ボタンコンテンツ
            DrawButtonContent(graphics);
        }

        protected override async Task UpdateEntityAsync(float deltaTime)
        {
            await Task.CompletedTask;
        }

        protected abstract void DrawButtonContent(Graphics graphics);
        protected abstract void OnButtonClicked();

        private void OnMouseClick(object? sender, MouseEventArgs e)
        {
            if (!_isEnabled || e.Button != MouseButtons.Left) return;

            OnButtonClicked();
            Clicked?.Invoke(this, new ButtonClickedEventArgs(ButtonType, e.Location));
        }

        private void SetHovered(bool hovered)
        {
            if (_isHovered == hovered) return;

            _isHovered = hovered;
            HoverStateChanged?.Invoke(this, new ButtonHoverEventArgs(hovered));
            MarkForRedraw();
        }
        #endregion
    }

    /// <summary>
    /// 具体的なボタン実装例
    /// </summary>
    public class StartButton : BaseButton
    {
        public override ButtonType ButtonType => ButtonType.Start;

        public StartButton(Point location, Size size) : base(location, size)
        {
            Text = GameConstants.Messages.BUTTON_START;
        }

        protected override void DrawButtonContent(Graphics graphics)
        {
            var fontManager = Program.ServiceContainer?.GetService<IFontManager>();
            var font = fontManager?.PressStartFont ?? SystemFonts.DefaultFont;

            using (var brush = new SolidBrush(Color.Black))
            {
                var textSize = graphics.MeasureString(Text, font);
                var x = (Width - textSize.Width) / 2;
                var y = (Height - textSize.Height) / 2;
                graphics.DrawString(Text, font, brush, x, y);
            }
        }

        protected override void OnButtonClicked()
        {
            var stageManager = Program.ServiceContainer?.GetService<IStageManagerService>();
            stageManager?.StartNextStage();
        }
    }

    public class RetryButton : BaseButton
    {
        public override ButtonType ButtonType => ButtonType.Retry;

        public RetryButton(Point location, Size size) : base(location, size)
        {
            Text = GameConstants.Messages.BUTTON_RETRY;
        }

        protected override void DrawButtonContent(Graphics graphics)
        {
            var fontManager = Program.ServiceContainer?.GetService<IFontManager>();
            var font = fontManager?.PressStartFont ?? SystemFonts.DefaultFont;

            using (var brush = new SolidBrush(Color.Black))
            {
                var textSize = graphics.MeasureString(Text, font);
                var x = (Width - textSize.Width) / 2;
                var y = (Height - textSize.Height) / 2;
                graphics.DrawString(Text, font, brush, x, y);
            }
        }

        protected override void OnButtonClicked()
        {
            var stageManager = Program.ServiceContainer?.GetService<IStageManagerService>();
            stageManager?.RestartCurrentStage();
        }
    }

    public class ToTitleButton : BaseButton
    {
        public override ButtonType ButtonType => ButtonType.ToTitle;

        public ToTitleButton(Point location, Size size) : base(location, size)
        {
            Text = GameConstants.Messages.BUTTON_TITLE;
        }

        protected override void DrawButtonContent(Graphics graphics)
        {
            var fontManager = Program.ServiceContainer?.GetService<IFontManager>();
            var font = fontManager?.PressStartFont ?? SystemFonts.DefaultFont;

            using (var brush = new SolidBrush(Color.Black))
            {
                var textSize = graphics.MeasureString(Text, font);
                var x = (Width - textSize.Width) / 2;
                var y = (Height - textSize.Height) / 2;
                graphics.DrawString(Text, font, brush, x, y);
            }
        }

        protected override void OnButtonClicked()
        {
            var stageManager = Program.ServiceContainer?.GetService<IStageManagerService>();
            stageManager?.ReturnToTitle();
        }
    }

    public class ExitButton : BaseButton
    {
        public override ButtonType ButtonType => ButtonType.Exit;

        public ExitButton(Point location, Size size) : base(location, size)
        {
            Text = GameConstants.Messages.BUTTON_EXIT;
        }

        protected override void DrawButtonContent(Graphics graphics)
        {
            var fontManager = Program.ServiceContainer?.GetService<IFontManager>();
            var font = fontManager?.PressStartFont ?? SystemFonts.DefaultFont;

            using (var brush = new SolidBrush(Color.Black))
            {
                var textSize = graphics.MeasureString(Text, font);
                var x = (Width - textSize.Width) / 2;
                var y = (Height - textSize.Height) / 2;
                graphics.DrawString(Text, font, brush, x, y);
            }
        }

        protected override void OnButtonClicked()
        {
            // アプリケーション終了処理
            Application.Exit();
        }
    }

    public class SettingsButton : BaseButton
    {
        public override ButtonType ButtonType => ButtonType.Settings;

        public SettingsButton(Point location, Size size) : base(location, size)
        {
            Text = GameConstants.Messages.BUTTON_SETTINGS;
        }

        protected override void DrawButtonContent(Graphics graphics)
        {
            var fontManager = Program.ServiceContainer?.GetService<IFontManager>();
            var font = fontManager?.PressStartFont ?? SystemFonts.DefaultFont;

            using (var brush = new SolidBrush(Color.Black))
            {
                var textSize = graphics.MeasureString(Text, font);
                var x = (Width - textSize.Width) / 2;
                var y = (Height - textSize.Height) / 2;
                graphics.DrawString(Text, font, brush, x, y);
            }
        }

        protected override void OnButtonClicked()
        {
            // 設定画面を開く
            var uiService = Program.ServiceContainer?.GetService<IUIService>();
            // TODO: 設定フォームの表示
        }
    }
}