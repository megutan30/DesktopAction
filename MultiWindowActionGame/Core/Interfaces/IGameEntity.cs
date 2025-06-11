// Core/Interfaces/IGameEntity.cs
using MultiWindowActionGame.Core.Entities;

namespace MultiWindowActionGame.Core.Interfaces
{
    /// <summary>
    /// ゲームエンティティの基本インターフェース
    /// </summary>
    public interface IGameEntity : IDrawable, IUpdatable, ITransformable, IActivatable, IIdentifiable
    {
        /// <summary>
        /// エンティティの種類
        /// </summary>
        EntityType EntityType { get; }

        /// <summary>
        /// 入力を受け取ることができるかどうか
        /// </summary>
        bool CanReceiveInput { get; }

        /// <summary>
        /// 表示されているかどうか
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// キー入力を処理する
        /// </summary>
        /// <param name="key">入力されたキー</param>
        /// <param name="isPressed">押下されたかどうか</param>
        void HandleInput(Keys key, bool isPressed);

        /// <summary>
        /// マウス入力を処理する
        /// </summary>
        /// <param name="mousePosition">マウス位置</param>
        /// <param name="button">マウスボタン</param>
        /// <param name="isPressed">押下されたかどうか</param>
        void HandleMouseInput(Point mousePosition, MouseButtons button, bool isPressed);

        /// <summary>
        /// 他のエンティティとの交差をチェック
        /// </summary>
        /// <param name="other">他のエンティティ</param>
        /// <returns>交差している場合true</returns>
        bool IntersectsWith(IGameEntity other);

        /// <summary>
        /// 他のエンティティとの距離を計算
        /// </summary>
        /// <param name="other">他のエンティティ</param>
        /// <returns>距離</returns>
        float DistanceTo(IGameEntity other);

        /// <summary>
        /// 指定された点を含むかチェック
        /// </summary>
        /// <param name="point">チェック対象の点</param>
        /// <returns>含む場合true</returns>
        bool Contains(Point point);

        /// <summary>
        /// デバッグ情報を取得
        /// </summary>
        /// <returns>デバッグ情報文字列</returns>
        string GetDebugInfo();

        /// <summary>
        /// エンティティ状態変更イベント
        /// </summary>
        event EventHandler<EntityStateChangedEventArgs>? StateChanged;
    }

    /// <summary>
    /// ゲームウィンドウのインターフェース
    /// </summary>
    public interface IGameWindow : IGameEntity, IHierarchical<IGameWindow>, ICollidable, IZOrderable, IMinimizable
    {
        /// <summary>
        /// ウィンドウ戦略の種類
        /// </summary>
        WindowStrategyType StrategyType { get; }

        /// <summary>
        /// ウィンドウに入ることができるかどうか
        /// </summary>
        bool CanEnter { get; set; }

        /// <summary>
        /// ウィンドウから出ることができるかどうか
        /// </summary>
        bool CanExit { get; set; }

        /// <summary>
        /// クライアント領域の境界
        /// </summary>
        Rectangle ClientBounds { get; }

        /// <summary>
        /// 調整された境界（マージンを考慮）
        /// </summary>
        Rectangle AdjustedBounds { get; }

        /// <summary>
        /// 元のサイズ
        /// </summary>
        Size OriginalSize { get; }

        /// <summary>
        /// ウィンドウの背景色
        /// </summary>
        Color BackColor { get; set; }

        /// <summary>
        /// テキスト表示（TextDisplayWindowの場合）
        /// </summary>
        string? DisplayText { get; set; }

        /// <summary>
        /// ウィンドウが移動したときのイベント
        /// </summary>
        event EventHandler<WindowMovedEventArgs>? WindowMoved;

        /// <summary>
        /// ウィンドウがリサイズされたときのイベント
        /// </summary>
        event EventHandler<WindowResizedEventArgs>? WindowResized;

        /// <summary>
        /// ウィンドウが最前面に移動する
        /// </summary>
        void BringToFront();

        /// <summary>
        /// ウィンドウが最背面に移動する
        /// </summary>
        void SendToBack();

        /// <summary>
        /// 境界を更新する
        /// </summary>
        void UpdateBounds();

        /// <summary>
        /// 戦略を変更する
        /// </summary>
        /// <param name="newStrategyType">新しい戦略の種類</param>
        void ChangeStrategy(WindowStrategyType newStrategyType);
    }

    /// <summary>
    /// プレイヤーのインターフェース
    /// </summary>
    public interface IPlayer : IGameEntity, ICollidable, IMinimizable
    {
        /// <summary>
        /// 地面に接触しているかどうか
        /// </summary>
        bool IsGrounded { get; }

        /// <summary>
        /// 垂直方向の速度
        /// </summary>
        float VerticalVelocity { get; }

        /// <summary>
        /// 現在の状態
        /// </summary>
        IPlayerState CurrentState { get; }

        /// <summary>
        /// 最後に有効だった親ウィンドウ
        /// </summary>
        IGameWindow? LastValidParent { get; }

        /// <summary>
        /// 移動可能領域
        /// </summary>
        Region MovableRegion { get; }

        /// <summary>
        /// 接地判定エリア
        /// </summary>
        Rectangle GroundCheckArea { get; }

        /// <summary>
        /// プレイヤーの位置をリセット
        /// </summary>
        /// <param name="position">新しい位置</param>
        void ResetPosition(Point position);

        /// <summary>
        /// プレイヤーのサイズをリセット
        /// </summary>
        /// <param name="size">新しいサイズ</param>
        void ResetSize(Size size);

        /// <summary>
        /// ジャンプする
        /// </summary>
        void Jump();

        /// <summary>
        /// 状態を変更する
        /// </summary>
        /// <param name="newState">新しい状態</param>
        void ChangeState(IPlayerState newState);

        /// <summary>
        /// 移動可能領域を更新する
        /// </summary>
        /// <param name="newRegion">新しい移動可能領域</param>
        void UpdateMovableRegion(Region newRegion);

        /// <summary>
        /// 親ウィンドウを設定する
        /// </summary>
        /// <param name="parent">親ウィンドウ</param>
        void SetParentWindow(IGameWindow? parent);

        /// <summary>
        /// プレイヤー状態変更イベント
        /// </summary>
        event EventHandler<PlayerStateChangedEventArgs>? PlayerStateChanged;

        /// <summary>
        /// ジャンプイベント
        /// </summary>
        event EventHandler<PlayerJumpedEventArgs>? Jumped;

        /// <summary>
        /// 着地イベント
        /// </summary>
        event EventHandler<PlayerLandedEventArgs>? Landed;
    }

    /// <summary>
    /// ゴールのインターフェース
    /// </summary>
    public interface IGoal : IGameEntity, ICollidable
    {
        /// <summary>
        /// 前面に表示されるかどうか
        /// </summary>
        bool IsInFront { get; set; }

        /// <summary>
        /// ゴールに到達したかチェック
        /// </summary>
        /// <param name="player">プレイヤー</param>
        /// <returns>到達した場合true</returns>
        bool CheckReached(IPlayer player);

        /// <summary>
        /// ゴール到達イベント
        /// </summary>
        event EventHandler<GoalReachedEventArgs>? GoalReached;
    }

    /// <summary>
    /// ゲームボタンのインターフェース
    /// </summary>
    public interface IGameButton : IGameEntity, ICollidable
    {
        /// <summary>
        /// ボタンの種類
        /// </summary>
        ButtonType ButtonType { get; }

        /// <summary>
        /// ホバー状態かどうか
        /// </summary>
        bool IsHovered { get; }

        /// <summary>
        /// 有効かどうか
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// ボタンのテキスト
        /// </summary>
        string Text { get; set; }

        /// <summary>
        /// ボタンがクリックされたときのイベント
        /// </summary>
        event EventHandler<ButtonClickedEventArgs>? Clicked;

        /// <summary>
        /// ホバー状態変更イベント
        /// </summary>
        event EventHandler<ButtonHoverEventArgs>? HoverStateChanged;
    }

    /// <summary>
    /// プレイヤー状態のインターフェース
    /// </summary>
    public interface IPlayerState
    {
        /// <summary>
        /// 状態名
        /// </summary>
        string StateName { get; }

        /// <summary>
        /// 地面チェックを行うかどうか
        /// </summary>
        bool ShouldCheckGround { get; }

        /// <summary>
        /// 状態を更新する
        /// </summary>
        /// <param name="player">プレイヤー</param>
        /// <param name="deltaTime">経過時間</param>
        void Update(IPlayer player, float deltaTime);

        /// <summary>
        /// 状態を描画する
        /// </summary>
        /// <param name="player">プレイヤー</param>
        /// <param name="graphics">描画コンテキスト</param>
        void Draw(IPlayer player, Graphics graphics);

        /// <summary>
        /// 入力を処理する
        /// </summary>
        /// <param name="player">プレイヤー</param>
        void HandleInput(IPlayer player);

        /// <summary>
        /// 状態に入るときの処理
        /// </summary>
        /// <param name="player">プレイヤー</param>
        void OnEnter(IPlayer player);

        /// <summary>
        /// 状態から出るときの処理
        /// </summary>
        /// <param name="player">プレイヤー</param>
        void OnExit(IPlayer player);
    }

    // ===== イベント引数 =====

    /// <summary>
    /// ウィンドウ移動イベント引数
    /// </summary>
    public class WindowMovedEventArgs : EventArgs
    {
        public Point OldPosition { get; }
        public Point NewPosition { get; }

        public WindowMovedEventArgs(Point oldPosition, Point newPosition)
        {
            OldPosition = oldPosition;
            NewPosition = newPosition;
        }
    }

    /// <summary>
    /// ウィンドウリサイズイベント引数
    /// </summary>
    public class WindowResizedEventArgs : EventArgs
    {
        public Size OldSize { get; }
        public Size NewSize { get; }

        public WindowResizedEventArgs(Size oldSize, Size newSize)
        {
            OldSize = oldSize;
            NewSize = newSize;
        }
    }

    /// <summary>
    /// プレイヤー状態変更イベント引数
    /// </summary>
    public class PlayerStateChangedEventArgs : EventArgs
    {
        public IPlayerState OldState { get; }
        public IPlayerState NewState { get; }

        public PlayerStateChangedEventArgs(IPlayerState oldState, IPlayerState newState)
        {
            OldState = oldState;
            NewState = newState;
        }
    }

    /// <summary>
    /// プレイヤージャンプイベント引数
    /// </summary>
    public class PlayerJumpedEventArgs : EventArgs
    {
        public float JumpForce { get; }
        public Point Position { get; }

        public PlayerJumpedEventArgs(float jumpForce, Point position)
        {
            JumpForce = jumpForce;
            Position = position;
        }
    }

    /// <summary>
    /// プレイヤー着地イベント引数
    /// </summary>
    public class PlayerLandedEventArgs : EventArgs
    {
        public Point Position { get; }
        public IGameWindow? LandedWindow { get; }

        public PlayerLandedEventArgs(Point position, IGameWindow? landedWindow)
        {
            Position = position;
            LandedWindow = landedWindow;
        }
    }

    /// <summary>
    /// ゴール到達イベント引数
    /// </summary>
    public class GoalReachedEventArgs : EventArgs
    {
        public IPlayer Player { get; }
        public IGoal Goal { get; }
        public DateTime ReachedTime { get; }

        public GoalReachedEventArgs(IPlayer player, IGoal goal)
        {
            Player = player;
            Goal = goal;
            ReachedTime = DateTime.Now;
        }
    }

    /// <summary>
    /// ボタンクリックイベント引数
    /// </summary>
    public class ButtonClickedEventArgs : EventArgs
    {
        public ButtonType ButtonType { get; }
        public Point ClickPosition { get; }

        public ButtonClickedEventArgs(ButtonType buttonType, Point clickPosition)
        {
            ButtonType = buttonType;
            ClickPosition = clickPosition;
        }
    }

    /// <summary>
    /// ボタンホバーイベント引数
    /// </summary>
    public class ButtonHoverEventArgs : EventArgs
    {
        public bool IsHovered { get; }

        public ButtonHoverEventArgs(bool isHovered)
        {
            IsHovered = isHovered;
        }
    }

    // ===== 列挙型 =====

    /// <summary>
    /// ウィンドウ戦略の種類
    /// </summary>
    public enum WindowStrategyType
    {
        Normal,
        Movable,
        Resizable,
        Minimizable,
        Deletable,
        TextDisplay
    }

    /// <summary>
    /// ボタンの種類
    /// </summary>
    public enum ButtonType
    {
        Start,
        Retry,
        ToTitle,
        Exit,
        Settings
    }
}