// Core/Services/Interfaces/IGameServices.cs
using MultiWindowActionGame.Core.Interfaces;
using MultiWindowActionGame.Core.Constants;

namespace MultiWindowActionGame.Core.Services.Interfaces
{
    // ===== イベント関連 =====

    /// <summary>
    /// イベント管理サービス
    /// </summary>
    public interface IEventService
    {
        void Subscribe<T>(Action<T> handler) where T : class;
        void Unsubscribe<T>(Action<T> handler) where T : class;
        void Publish<T>(T eventArgs) where T : class;
        Task PublishAsync<T>(T eventArgs) where T : class;
        void Clear();
        int GetSubscriberCount<T>() where T : class;
    }

    /// <summary>
    /// 設定管理サービス
    /// </summary>
    public interface ISettingsService
    {
        T GetSetting<T>(string key, T defaultValue = default!);
        void SetSetting<T>(string key, T value);
        void SaveSettings();
        void LoadSettings();
        void ResetToDefaults();
        bool HasSetting(string key);
        void RemoveSetting(string key);
        IReadOnlyDictionary<string, object> GetAllSettings();
        
        event EventHandler<SettingChangedEventArgs>? SettingChanged;
    }

    /// <summary>
    /// パフォーマンス監視サービス
    /// </summary>
    public interface IPerformanceService
    {
        float CurrentFPS { get; }
        float AverageFrameTime { get; }
        TimeSpan TotalRunTime { get; }
        
        void UpdateFrameTime(float deltaTime);
        IDisposable BeginMeasurement(string name);
        void RecordMeasurement(string name, TimeSpan duration);
        IReadOnlyDictionary<string, PerformanceMeasurement> GetMeasurements();
        void Reset();
        void SetEnabled(bool enabled);
        
        event EventHandler<PerformanceReportEventArgs>? PerformanceReport;
    }

    /// <summary>
    /// 入力管理サービス
    /// </summary>
    public interface IInputService
    {
        InputState CurrentInputState { get; }
        Point MousePosition { get; }
        
        void Update();
        bool IsKeyDown(Keys key);
        bool IsKeyPressed(Keys key);
        bool IsKeyReleased(Keys key);
        bool IsMouseButtonDown(MouseButtons button);
        bool IsMouseButtonPressed(MouseButtons button);
        bool IsMouseButtonReleased(MouseButtons button);
        
        void SetInputEnabled(bool enabled);
        void RegisterKeyBinding(string actionName, Keys key);
        void UnregisterKeyBinding(string actionName);
        bool IsActionPressed(string actionName);
        bool IsActionDown(string actionName);
        
        event EventHandler<KeyEventArgs>? KeyPressed;
        event EventHandler<KeyEventArgs>? KeyReleased;
        event EventHandler<MouseEventArgs>? MousePressed;
        event EventHandler<MouseEventArgs>? MouseReleased;
        event EventHandler<MouseEventArgs>? MouseMoved;
    }

    // ===== ゲームエンティティ関連サービス =====

    /// <summary>
    /// ウィンドウ管理サービス
    /// </summary>
    public interface IWindowManagerService
    {
        IReadOnlyList<IGameWindow> GetAllWindows();
        IReadOnlyList<IGameWindow> GetActiveWindows();
        
        void RegisterWindow(IGameWindow window);
        void UnregisterWindow(IGameWindow window);
        
        IGameWindow? GetWindowAt(Rectangle bounds);
        IGameWindow? GetTopWindowAt(Rectangle bounds, IGameWindow? exclude = null);
        IGameWindow? GetWindowById(Guid id);
        
        void BringWindowToFront(IGameWindow window);
        void SendWindowToBack(IGameWindow window);
        void UpdateWindowOrders();
        
        Task UpdateAsync(float deltaTime);
        void ClearAllWindows();
        
        event EventHandler<WindowRegisteredEventArgs>? WindowRegistered;
        event EventHandler<WindowUnregisteredEventArgs>? WindowUnregistered;
        event EventHandler<WindowOrderChangedEventArgs>? WindowOrderChanged;
    }

    /// <summary>
    /// Z-Order管理サービス
    /// </summary>
    public interface IZOrderService
    {
        void RegisterWindow(IZOrderable window);
        void UnregisterWindow(IZOrderable window);
        void BringToFront(IZOrderable window);
        void SendToBack(IZOrderable window);
        void UpdateOrders(IReadOnlyList<IZOrderable> windows);
        int GetZOrder(IZOrderable window);
        IReadOnlyList<IZOrderable> GetWindowsByPriority(ZOrderPriority priority);
        
        event EventHandler<ZOrderChangedEventArgs>? ZOrderChanged;
    }

    /// <summary>
    /// ウィンドウ階層管理サービス
    /// </summary>
    public interface IWindowHierarchyService
    {
        void UpdateHierarchy(IGameWindow window);
        void RemoveFromHierarchy(IGameWindow window);
        IGameWindow? FindParentWindow(IGameWindow window, IReadOnlyList<IGameWindow> candidates);
        IReadOnlyList<IGameWindow> GetChildren(IGameWindow parent);
        IReadOnlyList<IGameWindow> GetDescendants(IGameWindow ancestor);
        IReadOnlyList<IGameWindow> GetRootWindows(IReadOnlyList<IGameWindow> allWindows);
        
        event EventHandler<HierarchyChangedEventArgs<IGameWindow>>? HierarchyChanged;
    }

    /// <summary>
    /// ウィンドウ衝突判定サービス
    /// </summary>
    public interface IWindowCollisionService
    {
        bool IsFullyContained(Rectangle inner, Rectangle outer);
        bool Intersects(Rectangle rect1, Rectangle rect2);
        
        IGameWindow? GetTopWindowAt(Rectangle bounds, IReadOnlyList<IGameWindow> windows);
        IReadOnlyList<IGameWindow> GetIntersectingWindows(Rectangle bounds, IReadOnlyList<IGameWindow> windows);
        
        Rectangle GetValidPosition(Rectangle currentBounds, Rectangle proposedBounds, IReadOnlyList<IGameWindow> obstacles);
        Size GetValidSize(Rectangle currentBounds, Size proposedSize, IReadOnlyList<IGameWindow> obstacles);
        
        CollisionResult CheckCollision(ICollidable obj1, ICollidable obj2);
        IReadOnlyList<ICollidable> GetCollisions(ICollidable target, IReadOnlyList<ICollidable> candidates);
    }

    /// <summary>
    /// プレイヤー管理サービス
    /// </summary>
    public interface IPlayerService
    {
        IPlayer? CurrentPlayer { get; }
        
        void SetPlayer(IPlayer player);
        void InitializePlayer(Point startPosition);
        void ResetPlayer(Point position, Size? size = null);
        void RemovePlayer();
        
        Task UpdatePlayerAsync(float deltaTime);
        
        event EventHandler<PlayerChangedEventArgs>? PlayerChanged;
        event EventHandler<PlayerStateChangedEventArgs>? PlayerStateChanged;
    }

    /// <summary>
    /// ステージ管理サービス
    /// </summary>
    public interface IStageManagerService
    {
        int CurrentStageNumber { get; }
        IStageData? CurrentStage { get; }
        IGoal? CurrentGoal { get; }
        int TotalStages { get; }
        
        Task StartStageAsync(int stageNumber);
        void RestartCurrentStage();
        void StartNextStage();
        void StartPreviousStage();
        void ReturnToTitle();
        
        bool CheckGoalReached(IPlayer player);
        bool IsValidStageNumber(int stageNumber);
        
        event EventHandler<StageChangedEventArgs>? StageChanged;
        event EventHandler<GoalReachedEventArgs>? GoalReached;
        event EventHandler<StageCompletedEventArgs>? StageCompleted;
    }

    /// <summary>
    /// 不可侵領域管理サービス
    /// </summary>
    public interface INoEntryZoneService
    {
        void AddZone(Rectangle bounds);
        void AddZone(Point location, Size size);
        void RemoveZone(Rectangle bounds);
        void ClearZones();
        
        bool IntersectsWithAnyZone(Rectangle bounds);
        Rectangle GetValidPosition(Rectangle currentBounds, Rectangle proposedBounds);
        Size GetValidSize(Rectangle currentBounds, Size proposedSize);
        
        IReadOnlyList<Rectangle> GetZones();
        IReadOnlyList<Rectangle> GetIntersectingZones(Rectangle bounds);
    }

    // ===== 描画関連サービス =====

    /// <summary>
    /// 描画管理サービス
    /// </summary>
    public interface IRenderingService
    {
        void Render(Graphics graphics);
        void RegisterDrawable(IDrawable drawable, int layer = 0);
        void UnregisterDrawable(IDrawable drawable);
        void MarkForRedraw();
        void SetViewport(Rectangle viewport);
        
        int LayerCount { get; }
        bool IsEnabled { get; set; }
        
        void SetLayerVisible(int layer, bool visible);
        bool IsLayerVisible(int layer);
        
        event EventHandler<RenderFrameEventArgs>? FrameRendered;
        event EventHandler<LayerChangedEventArgs>? LayerChanged;
    }

    /// <summary>
    /// UI管理サービス
    /// </summary>
    public interface IUIService
    {
        void ShowNotification(string message, TimeSpan duration);
        void ShowErrorMessage(string message);
        void ShowDialog(Form dialog);
        bool IsDialogOpen { get; }
        
        void RegisterButton(IGameButton button);
        void UnregisterButton(IGameButton button);
        IReadOnlyList<IGameButton> GetAllButtons();
        
        event EventHandler<NotificationEventArgs>? NotificationShown;
        event EventHandler<DialogEventArgs>? DialogOpened;
        event EventHandler<DialogEventArgs>? DialogClosed;
    }

    // ===== データ構造 =====

    /// <summary>
    /// 入力状態
    /// </summary>
    public class InputState
    {
        public HashSet<Keys> PressedKeys { get; } = new();
        public HashSet<Keys> JustPressedKeys { get; } = new();
        public HashSet<Keys> JustReleasedKeys { get; } = new();
        
        public HashSet<MouseButtons> PressedMouseButtons { get; } = new();
        public HashSet<MouseButtons> JustPressedMouseButtons { get; } = new();
        public HashSet<MouseButtons> JustReleasedMouseButtons { get; } = new();
        
        public Point MousePosition { get; set; }
        public Point PreviousMousePosition { get; set; }
        public Vector2 MouseDelta => new(MousePosition.X - PreviousMousePosition.X, MousePosition.Y - PreviousMousePosition.Y);
    }

    /// <summary>
    /// パフォーマンス測定データ
    /// </summary>
    public class PerformanceMeasurement
    {
        public string Name { get; set; } = "";
        public TimeSpan Duration { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public TimeSpan MinDuration { get; set; }
        public TimeSpan MaxDuration { get; set; }
        public int CallCount { get; set; }
        public DateTime LastCalled { get; set; }
    }

    /// <summary>
    /// 衝突結果
    /// </summary>
    public class CollisionResult
    {
        public bool HasCollision { get; set; }
        public Rectangle IntersectionArea { get; set; }
        public Vector2 SeparationVector { get; set; }
        public CollisionType CollisionType { get; set; }
    }

    /// <summary>
    /// ステージデータインターフェース
    /// </summary>
    public interface IStageData
    {
        int StageNumber { get; }
        string StageName { get; }
        Point PlayerStartPosition { get; }
        Point? GoalPosition { get; }
        bool IsGoalInFront { get; }
        IReadOnlyList<WindowCreationData> Windows { get; }
        IReadOnlyList<ButtonCreationData> Buttons { get; }
        IReadOnlyList<Rectangle> NoEntryZones { get; }
        bool IsTitleStage { get; }
    }

    /// <summary>
    /// ウィンドウ作成データ
    /// </summary>
    public class WindowCreationData
    {
        public WindowStrategyType StrategyType { get; set; }
        public Point Location { get; set; }
        public Size Size { get; set; }
        public string? Text { get; set; }
        public Color? BackColor { get; set; }
        public bool CanEnter { get; set; } = true;
        public bool CanExit { get; set; } = true;
    }

    /// <summary>
    /// ボタン作成データ
    /// </summary>
    public class ButtonCreationData
    {
        public ButtonType ButtonType { get; set; }
        public Point Location { get; set; }
        public Size Size { get; set; }
        public string? Text { get; set; }
    }

    // ===== 列挙型 =====

    public enum WindowStrategyType
    {
        Normal,
        Movable,
        Resizable,
        Minimizable,
        Deletable,
        TextDisplay
    }

    public enum ButtonType
    {
        Start,
        Retry,
        ToTitle,
        Exit,
        Settings
    }

    // ===== イベント引数 =====

    public class SettingChangedEventArgs : EventArgs
    {
        public string Key { get; }
        public object? OldValue { get; }
        public object? NewValue { get; }

        public SettingChangedEventArgs(string key, object? oldValue, object? newValue)
        {
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    public class PerformanceReportEventArgs : EventArgs
    {
        public float CurrentFPS { get; }
        public TimeSpan FrameTime { get; }
        public IReadOnlyDictionary<string, PerformanceMeasurement> Measurements { get; }

        public PerformanceReportEventArgs(float currentFPS, TimeSpan frameTime, IReadOnlyDictionary<string, PerformanceMeasurement> measurements)
        {
            CurrentFPS = currentFPS;
            FrameTime = frameTime;
            Measurements = measurements;
        }
    }

    public class WindowRegisteredEventArgs : EventArgs
    {
        public IGameWindow Window { get; }

        public WindowRegisteredEventArgs(IGameWindow window)
        {
            Window = window;
        }
    }

    public class WindowUnregisteredEventArgs : EventArgs
    {
        public IGameWindow Window { get; }

        public WindowUnregisteredEventArgs(IGameWindow window)
        {
            Window = window;
        }
    }

    public class WindowOrderChangedEventArgs : EventArgs
    {
        public IGameWindow Window { get; }
        public int OldOrder { get; }
        public int NewOrder { get; }

        public WindowOrderChangedEventArgs(IGameWindow window, int oldOrder, int newOrder)
        {
            Window = window;
            OldOrder = oldOrder;
            NewOrder = newOrder;
        }
    }

    public class PlayerChangedEventArgs : EventArgs
    {
        public IPlayer? OldPlayer { get; }
        public IPlayer? NewPlayer { get; }

        public PlayerChangedEventArgs(IPlayer? oldPlayer, IPlayer? newPlayer)
        {
            OldPlayer = oldPlayer;
            NewPlayer = newPlayer;
        }
    }

    public class PlayerStateChangedEventArgs : EventArgs
    {
        public IPlayer Player { get; }
        public object? OldState { get; }
        public object? NewState { get; }

        public PlayerStateChangedEventArgs(IPlayer player, object? oldState, object? newState)
        {
            Player = player;
            OldState = oldState;
            NewState = newState;
        }
    }

    public class StageChangedEventArgs : EventArgs
    {
        public int OldStageNumber { get; }
        public int NewStageNumber { get; }
        public IStageData? NewStage { get; }

        public StageChangedEventArgs(int oldStageNumber, int newStageNumber, IStageData? newStage)
        {
            OldStageNumber = oldStageNumber;
            NewStageNumber = newStageNumber;
            NewStage = newStage;
        }
    }

    public class GoalReachedEventArgs : EventArgs
    {
        public IPlayer Player { get; }
        public IGoal Goal { get; }
        public int StageNumber { get; }

        public GoalReachedEventArgs(IPlayer player, IGoal goal, int stageNumber)
        {
            Player = player;
            Goal = goal;
            StageNumber = stageNumber;
        }
    }

    public class StageCompletedEventArgs : EventArgs
    {
        public int CompletedStageNumber { get; }
        public TimeSpan CompletionTime { get; }
        public bool IsLastStage { get; }

        public StageCompletedEventArgs(int completedStageNumber, TimeSpan completionTime, bool isLastStage)
        {
            CompletedStageNumber = completedStageNumber;
            CompletionTime = completionTime;
            IsLastStage = isLastStage;
        }
    }

    public class RenderFrameEventArgs : EventArgs
    {
        public Graphics Graphics { get; }
        public TimeSpan RenderTime { get; }
        public int DrawnObjects { get; }

        public RenderFrameEventArgs(Graphics graphics, TimeSpan renderTime, int drawnObjects)
        {
            Graphics = graphics;
            RenderTime = renderTime;
            DrawnObjects = drawnObjects;
        }
    }

    public class LayerChangedEventArgs : EventArgs
    {
        public int Layer { get; }
        public bool IsVisible { get; }
        public int ObjectCount { get; }

        public LayerChangedEventArgs(int layer, bool isVisible, int objectCount)
        {
            Layer = layer;
            IsVisible = isVisible;
            ObjectCount = objectCount;
        }
    }

    public class NotificationEventArgs : EventArgs
    {
        public string Message { get; }
        public TimeSpan Duration { get; }
        public NotificationType Type { get; }

        public NotificationEventArgs(string message, TimeSpan duration, NotificationType type)
        {
            Message = message;
            Duration = duration;
            Type = type;
        }
    }

    public class DialogEventArgs : EventArgs
    {
        public Form Dialog { get; }
        public DialogResult? Result { get; }

        public DialogEventArgs(Form dialog, DialogResult? result = null)
        {
            Dialog = dialog;
            Result = result;
        }
    }

    public enum NotificationType
    {
        Info,
        Warning,
        Error,
        Success
    }
}