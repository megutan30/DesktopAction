// Core/Entities/BaseGameEntity.cs
using MultiWindowActionGame.Core.Interfaces;
using MultiWindowActionGame.Core.Constants;
using System.Numerics;

namespace MultiWindowActionGame.Core.Entities
{
    /// <summary>
    /// すべてのゲームエンティティの基底クラス
    /// </summary>
    public abstract class BaseGameEntity : Form, 
        IGameEntity, IDrawable, IUpdatable, ITransformable, 
        IActivatable, IIdentifiable, IMinimizable, IDisposable
    {
        #region Fields
        private Rectangle _bounds;
        private bool _isActive = true;
        private bool _isMinimized = false;
        private DateTime? _minimizedTime;
        private bool _needsRedraw = true;
        private bool _disposed = false;
        #endregion

        #region Properties
        
        // IIdentifiable
        public Guid Id { get; } = Guid.NewGuid();
        public virtual string Name { get; protected set; } = "";
        public virtual string TypeName => GetType().Name;

        // ITransformable
        public virtual Point Position => _bounds.Location;
        public virtual Size Size => _bounds.Size;
        public virtual Rectangle Bounds => _bounds;

        // IDrawable
        public bool NeedsRedraw => _needsRedraw;
        public virtual int DrawPriority => 0;

        // IUpdatable
        public virtual int UpdatePriority => 0;
        public virtual bool IsUpdateEnabled => _isActive && !_isMinimized;

        // IActivatable
        public bool IsActive => _isActive;

        // IMinimizable
        public bool IsMinimized => _isMinimized;
        public DateTime? MinimizedTime => _minimizedTime;

        // IGameEntity
        public abstract EntityType EntityType { get; }
        public virtual bool CanReceiveInput => _isActive && !_isMinimized;
        public virtual bool IsVisible => _isActive && !_isMinimized;

        #endregion

        #region Events

        // ITransformable Events
        public event EventHandler<TransformChangedEventArgs>? TransformChanged;

        // IActivatable Events
        public event EventHandler<ActiveStateChangedEventArgs>? ActiveStateChanged;

        // IMinimizable Events
        public event EventHandler<MinimizeStateChangedEventArgs>? MinimizeStateChanged;

        // IGameEntity Events
        public event EventHandler<EntityStateChangedEventArgs>? StateChanged;

        #endregion

        #region Constructor

        protected BaseGameEntity()
        {
            InitializeEntity();
        }

        protected BaseGameEntity(Point location, Size size)
        {
            _bounds = new Rectangle(location, size);
            InitializeEntity();
        }

        private void InitializeEntity()
        {
            Name = $"{TypeName}_{Id:N}[..8]";
            
            // フォームの基本設定
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = false;
            this.Location = _bounds.Location;
            this.Size = _bounds.Size;

            // ダブルバッファリングを有効化
            this.SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);

            // イベントハンドラーの設定
            this.Paint += OnEntityPaint;
            this.Move += OnEntityMove;
            this.Resize += OnEntityResize;
        }

        #endregion

        #region ITransformable Implementation

        public virtual void SetPosition(Point position)
        {
            ThrowIfDisposed();
            
            var oldPosition = _bounds.Location;
            if (oldPosition == position) return;

            _bounds.Location = position;
            this.Location = position;
            
            OnTransformChanged(oldPosition, position, _bounds.Size, _bounds.Size, TransformChangeType.Position);
            MarkForRedraw();
        }

        public virtual void SetSize(Size size)
        {
            ThrowIfDisposed();
            
            var oldSize = _bounds.Size;
            if (oldSize == size) return;

            _bounds.Size = size;
            this.Size = size;
            
            OnTransformChanged(_bounds.Location, _bounds.Location, oldSize, size, TransformChangeType.Size);
            MarkForRedraw();
        }

        public virtual void Move(Vector2 delta)
        {
            ThrowIfDisposed();
            
            var newPosition = new Point(
                _bounds.X + (int)delta.X,
                _bounds.Y + (int)delta.Y);
            
            SetPosition(newPosition);
        }

        public virtual void Scale(SizeF scale)
        {
            ThrowIfDisposed();
            
            var newSize = new Size(
                (int)(_bounds.Width * scale.Width),
                (int)(_bounds.Height * scale.Height));
            
            SetSize(newSize);
        }

        #endregion

        #region IActivatable Implementation

        public virtual void Activate()
        {
            ThrowIfDisposed();
            
            if (_isActive) return;
            
            _isActive = true;
            OnActiveStateChanged(true);
            MarkForRedraw();
        }

        public virtual void Deactivate()
        {
            ThrowIfDisposed();
            
            if (!_isActive) return;
            
            _isActive = false;
            OnActiveStateChanged(false);
            MarkForRedraw();
        }

        #endregion

        #region IMinimizable Implementation

        public virtual void Minimize()
        {
            ThrowIfDisposed();
            
            if (_isMinimized) return;
            
            _isMinimized = true;
            _minimizedTime = DateTime.Now;
            this.WindowState = FormWindowState.Minimized;
            
            OnMinimizeStateChanged(true);
            OnStateChanged("Minimized");
        }

        public virtual void Restore()
        {
            ThrowIfDisposed();
            
            if (!_isMinimized) return;
            
            _isMinimized = false;
            _minimizedTime = null;
            this.WindowState = FormWindowState.Normal;
            this.Show();
            this.BringToFront();
            
            OnMinimizeStateChanged(false);
            OnStateChanged("Restored");
            MarkForRedraw();
        }

        #endregion

        #region IDrawable Implementation

        public virtual void Draw(Graphics graphics)
        {
            ThrowIfDisposed();
            
            if (!IsVisible) return;
            
            try
            {
                DrawEntity(graphics);
                _needsRedraw = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error drawing entity {Name}: {ex.Message}");
            }
        }

        public void MarkForRedraw()
        {
            if (_disposed) return;
            
            _needsRedraw = true;
            this.Invalidate();
        }

        /// <summary>
        /// 派生クラスで実装する描画メソッド
        /// </summary>
        protected abstract void DrawEntity(Graphics graphics);

        #endregion

        #region IUpdatable Implementation

        public virtual async Task UpdateAsync(float deltaTime)
        {
            ThrowIfDisposed();
            
            if (!IsUpdateEnabled) return;
            
            try
            {
                await UpdateEntityAsync(deltaTime);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating entity {Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// 派生クラスで実装する更新メソッド
        /// </summary>
        protected abstract Task UpdateEntityAsync(float deltaTime);

        #endregion

        #region IGameEntity Implementation

        public virtual void HandleInput(Keys key, bool isPressed)
        {
            if (!CanReceiveInput) return;
            OnInputReceived(key, isPressed);
        }

        public virtual void HandleMouseInput(Point mousePosition, MouseButtons button, bool isPressed)
        {
            if (!CanReceiveInput) return;
            OnMouseInputReceived(mousePosition, button, isPressed);
        }

        /// <summary>
        /// 派生クラスで実装する入力処理メソッド
        /// </summary>
        protected virtual void OnInputReceived(Keys key, bool isPressed) { }

        /// <summary>
        /// 派生クラスで実装するマウス入力処理メソッド
        /// </summary>
        protected virtual void OnMouseInputReceived(Point mousePosition, MouseButtons button, bool isPressed) { }

        #endregion

        #region Event Handlers

        private void OnEntityPaint(object? sender, PaintEventArgs e)
        {
            Draw(e.Graphics);
        }

        private void OnEntityMove(object? sender, EventArgs e)
        {
            var newPosition = this.Location;
            if (_bounds.Location != newPosition)
            {
                var oldPosition = _bounds.Location;
                _bounds.Location = newPosition;
                OnTransformChanged(oldPosition, newPosition, _bounds.Size, _bounds.Size, TransformChangeType.Position);
            }
        }

        private void OnEntityResize(object? sender, EventArgs e)
        {
            var newSize = this.Size;
            if (_bounds.Size != newSize)
            {
                var oldSize = _bounds.Size;
                _bounds.Size = newSize;
                OnTransformChanged(_bounds.Location, _bounds.Location, oldSize, newSize, TransformChangeType.Size);
            }
        }

        #endregion

        #region Event Raisers

        protected virtual void OnTransformChanged(Point oldPosition, Point newPosition, Size oldSize, Size newSize, TransformChangeType changeType)
        {
            TransformChanged?.Invoke(this, new TransformChangedEventArgs(oldPosition, newPosition, oldSize, newSize, changeType));
        }

        protected virtual void OnActiveStateChanged(bool isActive)
        {
            ActiveStateChanged?.Invoke(this, new ActiveStateChangedEventArgs(isActive));
        }

        protected virtual void OnMinimizeStateChanged(bool isMinimized)
        {
            MinimizeStateChanged?.Invoke(this, new MinimizeStateChangedEventArgs(isMinimized));
        }

        protected virtual void OnStateChanged(string stateName)
        {
            StateChanged?.Invoke(this, new EntityStateChangedEventArgs(this, stateName));
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// エンティティが指定された点を含むかチェック
        /// </summary>
        public virtual bool Contains(Point point)
        {
            return _bounds.Contains(point);
        }

        /// <summary>
        /// 他のエンティティとの交差をチェック
        /// </summary>
        public virtual bool IntersectsWith(IGameEntity other)
        {
            return _bounds.IntersectsWith(other.Bounds);
        }

        /// <summary>
        /// 他のエンティティとの距離を計算
        /// </summary>
        public virtual float DistanceTo(IGameEntity other)
        {
            var thisCenter = new Point(_bounds.X + _bounds.Width / 2, _bounds.Y + _bounds.Height / 2);
            var otherCenter = new Point(other.Bounds.X + other.Bounds.Width / 2, other.Bounds.Y + other.Bounds.Height / 2);
            
            var dx = thisCenter.X - otherCenter.X;
            var dy = thisCenter.Y - otherCenter.Y;
            
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// デバッグ情報を文字列として取得
        /// </summary>
        public virtual string GetDebugInfo()
        {
            return $"{TypeName} [{Id:D}] - Position: {Position}, Size: {Size}, Active: {IsActive}, Minimized: {IsMinimized}";
        }

        #endregion

        #region Protected Methods

        protected void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        /// <summary>
        /// エンティティの境界を更新する（内部使用）
        /// </summary>
        protected void UpdateBounds(Rectangle newBounds)
        {
            if (_bounds == newBounds) return;
            
            var oldPosition = _bounds.Location;
            var oldSize = _bounds.Size;
            
            _bounds = newBounds;
            
            var changeType = TransformChangeType.Both;
            if (oldPosition == newBounds.Location)
                changeType = TransformChangeType.Size;
            else if (oldSize == newBounds.Size)
                changeType = TransformChangeType.Position;
            
            OnTransformChanged(oldPosition, newBounds.Location, oldSize, newBounds.Size, changeType);
            MarkForRedraw();
        }

        #endregion

        #region IDisposable Implementation

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // イベントハンドラーの解除
                this.Paint -= OnEntityPaint;
                this.Move -= OnEntityMove;
                this.Resize -= OnEntityResize;

                // イベントの解除
                TransformChanged = null;
                ActiveStateChanged = null;
                MinimizeStateChanged = null;
                StateChanged = null;
            }

            _disposed = true;
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>
    /// エンティティの種類
    /// </summary>
    public enum EntityType
    {
        Window,
        Player,
        Goal,
        Button,
        NoEntryZone,
        Effect,
        UI
    }

    /// <summary>
    /// エンティティ状態変更イベント引数
    /// </summary>
    public class EntityStateChangedEventArgs : EventArgs
    {
        public IGameEntity Entity { get; }
        public string StateName { get; }
        public DateTime Timestamp { get; }

        public EntityStateChangedEventArgs(IGameEntity entity, string stateName)
        {
            Entity = entity;
            StateName = stateName;
            Timestamp = DateTime.Now;
        }
    }
}