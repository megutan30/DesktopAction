// Core/Factories/EntityFactories.cs
using MultiWindowActionGame.Core.Interfaces;
using MultiWindowActionGame.Core.Entities;
using MultiWindowActionGame.Core.Constants;
using MultiWindowActionGame.Core.Services.Interfaces;

namespace MultiWindowActionGame.Core.Factories
{
    /// <summary>
    /// エンティティファクトリーの基底クラス
    /// </summary>
    public abstract class BaseEntityFactory
    {
        protected readonly IServiceContainer ServiceContainer;

        protected BaseEntityFactory(IServiceContainer serviceContainer)
        {
            ServiceContainer = serviceContainer ?? throw new ArgumentNullException(nameof(serviceContainer));
        }

        /// <summary>
        /// 設定サービスを取得
        /// </summary>
        protected ISettingsService GetSettingsService()
        {
            return ServiceContainer.GetRequiredService<ISettingsService>();
        }

        /// <summary>
        /// イベントサービスを取得
        /// </summary>
        protected IEventService GetEventService()
        {
            return ServiceContainer.GetRequiredService<IEventService>();
        }
    }

    /// <summary>
    /// ウィンドウファクトリー
    /// </summary>
    public interface IWindowFactory
    {
        IGameWindow CreateWindow(WindowStrategyType strategyType, Point location, Size size, string? text = null, Color? backColor = null);
        IGameWindow CreateWindow(WindowCreationData creationData);
        void RegisterWindowCreated(IGameWindow window);
    }

    public class WindowFactory : BaseEntityFactory, IWindowFactory
    {
        private readonly IWindowManagerService _windowManager;
        private readonly IZOrderService _zOrderService;

        public WindowFactory(IServiceContainer serviceContainer) : base(serviceContainer)
        {
            _windowManager = ServiceContainer.GetRequiredService<IWindowManagerService>();
            _zOrderService = ServiceContainer.GetRequiredService<IZOrderService>();
        }

        public IGameWindow CreateWindow(WindowStrategyType strategyType, Point location, Size size, string? text = null, Color? backColor = null)
        {
            var creationData = new WindowCreationData
            {
                StrategyType = strategyType,
                Location = location,
                Size = size,
                Text = text,
                BackColor = backColor
            };

            return CreateWindow(creationData);
        }

        public IGameWindow CreateWindow(WindowCreationData creationData)
        {
            // バリデーション
            ValidateCreationData(creationData);

            // ウィンドウの作成
            var window = CreateWindowInstance(creationData);

            // プロパティの設定
            ConfigureWindow(window, creationData);

            // サービスへの登録
            RegisterWindowCreated(window);

            // イベント発行
            var eventService = GetEventService();
            eventService.Publish(new GameEvents.WindowCreatedEvent
            {
                Window = window,
                StrategyType = creationData.StrategyType
            });

            return window;
        }

        public void RegisterWindowCreated(IGameWindow window)
        {
            // ウィンドウマネージャーに登録
            _windowManager.RegisterWindow(window);

            // Z-Orderサービスに登録
            _zOrderService.RegisterWindow(window);
        }

        private void ValidateCreationData(WindowCreationData data)
        {
            if (data.Size.Width < GameConstants.Window.MINIMUM_WIDTH)
                throw new ArgumentException($"Window width must be at least {GameConstants.Window.MINIMUM_WIDTH}");

            if (data.Size.Height < GameConstants.Window.MINIMUM_HEIGHT)
                throw new ArgumentException($"Window height must be at least {GameConstants.Window.MINIMUM_HEIGHT}");
        }

        private IGameWindow CreateWindowInstance(WindowCreationData data)
        {
            return data.StrategyType switch
            {
                WindowStrategyType.Normal => new GameWindow(data.Location, data.Size, new NormalWindowStrategy()),
                WindowStrategyType.Movable => new GameWindow(data.Location, data.Size, new MovableWindowStrategy()),
                WindowStrategyType.Resizable => new GameWindow(data.Location, data.Size, new ResizableWindowStrategy()),
                WindowStrategyType.Minimizable => new GameWindow(data.Location, data.Size, new MinimizableWindowStrategy()),
                WindowStrategyType.Deletable => new GameWindow(data.Location, data.Size, new DeletableWindowStrategy()),
                WindowStrategyType.TextDisplay => new GameWindow(data.Location, data.Size, new TextDisplayWindowStrategy(data.Text ?? "")),
                _ => throw new ArgumentException($"Unknown window strategy type: {data.StrategyType}")
            };
        }

        private void ConfigureWindow(IGameWindow window, WindowCreationData data)
        {
            // 基本プロパティの設定
            window.CanEnter = data.CanEnter;
            window.CanExit = data.CanExit;

            if (data.BackColor.HasValue)
            {
                window.BackColor = data.BackColor.Value;
            }
            else
            {
                // デフォルト色の設定
                window.BackColor = GetDefaultBackColor(data.StrategyType);
            }

            if (!string.IsNullOrEmpty(data.Text))
            {
                window.DisplayText = data.Text;
            }
        }

        private Color GetDefaultBackColor(WindowStrategyType strategyType)
        {
            return strategyType switch
            {
                WindowStrategyType.Normal => GameConstants.Colors.WINDOW_WHITE,
                WindowStrategyType.Movable => GameConstants.Colors.WINDOW_LIGHT_BLUE,
                WindowStrategyType.Resizable => GameConstants.Colors.WINDOW_LIGHT_GREEN,
                WindowStrategyType.Minimizable => GameConstants.Colors.WINDOW_LIGHT_PINK,
                WindowStrategyType.Deletable => GameConstants.Colors.WINDOW_LIGHT_PINK,
                WindowStrategyType.TextDisplay => GameConstants.Colors.WINDOW_BLACK,
                _ => GameConstants.Colors.WINDOW_WHITE
            };
        }
    }

    /// <summary>
    /// プレイヤーファクトリー
    /// </summary>
    public interface IPlayerFactory
    {
        IPlayer CreatePlayer(Point startPosition);
        IPlayer CreatePlayer(Point startPosition, Size size);
        void RegisterPlayerCreated(IPlayer player);
    }

    public class PlayerFactory : BaseEntityFactory, IPlayerFactory
    {
        private readonly IPlayerService _playerService;

        public PlayerFactory(IServiceContainer serviceContainer) : base(serviceContainer)
        {
            _playerService = ServiceContainer.GetRequiredService<IPlayerService>();
        }

        public IPlayer CreatePlayer(Point startPosition)
        {
            var settings = GetSettingsService();
            var defaultSize = new Size(
                settings.GetSetting("Player.DefaultSize.Width", GameConstants.Player.DEFAULT_WIDTH),
                settings.GetSetting("Player.DefaultSize.Height", GameConstants.Player.DEFAULT_HEIGHT)
            );

            return CreatePlayer(startPosition, defaultSize);
        }

        public IPlayer CreatePlayer(Point startPosition, Size size)
        {
            // プレイヤーインスタンスの作成
            var player = new Player(startPosition, size);

            // サービスへの登録
            RegisterPlayerCreated(player);

            // イベント発行
            var eventService = GetEventService();
            eventService.Publish(new GameEvents.PlayerCreatedEvent
            {
                Player = player,
                StartPosition = startPosition
            });

            return player;
        }

        public void RegisterPlayerCreated(IPlayer player)
        {
            // プレイヤーサービスに登録
            _playerService.SetPlayer(player);
        }
    }

    /// <summary>
    /// ゴールファクトリー
    /// </summary>
    public interface IGoalFactory
    {
        IGoal CreateGoal(Point position, bool isInFront = true);
        IGoal CreateGoal(Point position, Size size, bool isInFront = true);
        void RegisterGoalCreated(IGoal goal);
    }

    public class GoalFactory : BaseEntityFactory, IGoalFactory
    {
        public GoalFactory(IServiceContainer serviceContainer) : base(serviceContainer) { }

        public IGoal CreateGoal(Point position, bool isInFront = true)
        {
            return CreateGoal(position, GameConstants.Sizes.DEFAULT_GOAL_SIZE, isInFront);
        }

        public IGoal CreateGoal(Point position, Size size, bool isInFront = true)
        {
            // ゴールインスタンスの作成
            var goal = new Goal(position, size, isInFront);

            // サービスへの登録
            RegisterGoalCreated(goal);

            return goal;
        }

        public void RegisterGoalCreated(IGoal goal)
        {
            // 必要に応じてサービスに登録
            // 現在はステージマネージャーが直接管理
        }
    }

    /// <summary>
    /// ボタンファクトリー
    /// </summary>
    public interface IButtonFactory
    {
        IGameButton CreateButton(ButtonType buttonType, Point location);
        IGameButton CreateButton(ButtonType buttonType, Point location, Size size);
        IGameButton CreateButton(ButtonCreationData creationData);
        void RegisterButtonCreated(IGameButton button);
    }

    public class ButtonFactory : BaseEntityFactory, IButtonFactory
    {
        private readonly IUIService _uiService;

        public ButtonFactory(IServiceContainer serviceContainer) : base(serviceContainer)
        {
            _uiService = ServiceContainer.GetRequiredService<IUIService>();
        }

        public IGameButton CreateButton(ButtonType buttonType, Point location)
        {
            return CreateButton(buttonType, location, GameConstants.Sizes.DEFAULT_BUTTON_SIZE);
        }

        public IGameButton CreateButton(ButtonType buttonType, Point location, Size size)
        {
            var creationData = new ButtonCreationData
            {
                ButtonType = buttonType,
                Location = location,
                Size = size
            };

            return CreateButton(creationData);
        }

        public IGameButton CreateButton(ButtonCreationData creationData)
        {
            // ボタンインスタンスの作成
            var button = CreateButtonInstance(creationData);

            // サービスへの登録
            RegisterButtonCreated(button);

            // イベント発行
            var eventService = GetEventService();
            eventService.Publish(new GameEvents.ButtonClickedEvent
            {
                Button = button,
                ButtonType = creationData.ButtonType,
                ClickPosition = creationData.Location
            });

            return button;
        }

        public void RegisterButtonCreated(IGameButton button)
        {
            // UIサービスに登録
            _uiService.RegisterButton(button);
        }

        private IGameButton CreateButtonInstance(ButtonCreationData data)
        {
            return data.ButtonType switch
            {
                ButtonType.Start => new StartButton(data.Location, data.Size),
                ButtonType.Retry => new RetryButton(data.Location, data.Size),
                ButtonType.ToTitle => new ToTitleButton(data.Location, data.Size),
                ButtonType.Exit => new ExitButton(data.Location, data.Size),
                ButtonType.Settings => new SettingsButton(data.Location, data.Size),
                _ => throw new ArgumentException($"Unknown button type: {data.ButtonType}")
            };
        }
    }

    /// <summary>
    /// 複合エンティティファクトリー - 複数のエンティティを組み合わせて作成
    /// </summary>
    public interface ICompositeEntityFactory
    {
        void CreateStageEntities(IStageData stageData);
        IGameWindow CreateWindowWithChildren(WindowCreationData parentData, IEnumerable<WindowCreationData> childrenData);
        void CreateUILayout(IEnumerable<ButtonCreationData> buttonsData);
    }

    public class CompositeEntityFactory : BaseEntityFactory, ICompositeEntityFactory
    {
        private readonly IWindowFactory _windowFactory;
        private readonly IPlayerFactory _playerFactory;
        private readonly IGoalFactory _goalFactory;
        private readonly IButtonFactory _buttonFactory;
        private readonly INoEntryZoneService _noEntryZoneService;

        public CompositeEntityFactory(
            IServiceContainer serviceContainer,
            IWindowFactory windowFactory,
            IPlayerFactory playerFactory,
            IGoalFactory goalFactory,
            IButtonFactory buttonFactory) : base(serviceContainer)
        {
            _windowFactory = windowFactory;
            _playerFactory = playerFactory;
            _goalFactory = goalFactory;
            _buttonFactory = buttonFactory;
            _noEntryZoneService = ServiceContainer.GetRequiredService<INoEntryZoneService>();
        }

        public void CreateStageEntities(IStageData stageData)
        {
            // 並列実行で効率化
            var tasks = new List<Task>
            {
                Task.Run(() => CreateWindows(stageData.Windows)),
                Task.Run(() => CreateButtons(stageData.Buttons)),
                Task.Run(() => CreateNoEntryZones(stageData.NoEntryZones))
            };

            // プレイヤーとゴールは順次作成（依存関係があるため）
            Task.WaitAll(tasks.ToArray());

            CreatePlayer(stageData);
            CreateGoal(stageData);
        }

        public IGameWindow CreateWindowWithChildren(WindowCreationData parentData, IEnumerable<WindowCreationData> childrenData)
        {
            // 親ウィンドウを作成
            var parentWindow = _windowFactory.CreateWindow(parentData);

            // 子ウィンドウを作成して親子関係を設定
            foreach (var childData in childrenData)
            {
                var childWindow = _windowFactory.CreateWindow(childData);
                parentWindow.AddChild(childWindow);
            }

            return parentWindow;
        }

        public void CreateUILayout(IEnumerable<ButtonCreationData> buttonsData)
        {
            foreach (var buttonData in buttonsData)
            {
                _buttonFactory.CreateButton(buttonData);
            }
        }

        private void CreateWindows(IReadOnlyList<WindowCreationData> windowsData)
        {
            foreach (var windowData in windowsData)
            {
                _windowFactory.CreateWindow(windowData);
            }
        }

        private void CreateButtons(IReadOnlyList<ButtonCreationData> buttonsData)
        {
            foreach (var buttonData in buttonsData)
            {
                _buttonFactory.CreateButton(buttonData);
            }
        }

        private void CreateNoEntryZones(IReadOnlyList<Rectangle> zones)
        {
            foreach (var zone in zones)
            {
                _noEntryZoneService.AddZone(zone);
            }
        }

        private void CreatePlayer(IStageData stageData)
        {
            _playerFactory.CreatePlayer(stageData.PlayerStartPosition);
        }

        private void CreateGoal(IStageData stageData)
        {
            if (stageData.GoalPosition.HasValue)
            {
                _goalFactory.CreateGoal(stageData.GoalPosition.Value, stageData.IsGoalInFront);
            }
        }
    }

    /// <summary>
    /// ファクトリー登録用の拡張メソッド
    /// </summary>
    public static class FactoryServiceExtensions
    {
        /// <summary>
        /// すべてのファクトリーをサービスコンテナに登録
        /// </summary>
        public static IServiceContainer RegisterFactories(this IServiceContainer container)
        {
            // ファクトリーの登録
            container.RegisterSingleton<IWindowFactory, WindowFactory>();
            container.RegisterSingleton<IPlayerFactory, PlayerFactory>();
            container.RegisterSingleton<IGoalFactory, GoalFactory>();
            container.RegisterSingleton<IButtonFactory, ButtonFactory>();
            container.RegisterSingleton<ICompositeEntityFactory, CompositeEntityFactory>();

            return container;
        }
    }
}

// ===== 作成データクラス =====

namespace MultiWindowActionGame.Core.Services.Interfaces
{
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

        /// <summary>
        /// 作成データを検証
        /// </summary>
        public bool IsValid()
        {
            return Size.Width > 0 && Size.Height > 0;
        }

        /// <summary>
        /// デフォルト設定でウィンドウ作成データを生成
        /// </summary>
        public static WindowCreationData CreateDefault(WindowStrategyType strategyType, Point location, Size size)
        {
            return new WindowCreationData
            {
                StrategyType = strategyType,
                Location = location,
                Size = size,
                CanEnter = true,
                CanExit = true
            };
        }
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
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 作成データを検証
        /// </summary>
        public bool IsValid()
        {
            return Size.Width > 0 && Size.Height > 0;
        }

        /// <summary>
        /// デフォルト設定でボタン作成データを生成
        /// </summary>
        public static ButtonCreationData CreateDefault(ButtonType buttonType, Point location)
        {
            return new ButtonCreationData
            {
                ButtonType = buttonType,
                Location = location,
                Size = GameConstants.Sizes.DEFAULT_BUTTON_SIZE,
                IsEnabled = true
            };
        }
    }
}