// Application/Program.cs
using MultiWindowActionGame.Core.DependencyInjection;
using MultiWindowActionGame.Core.Services;
using MultiWindowActionGame.Core.Services.Interfaces;
using MultiWindowActionGame.Core.Factories;
using MultiWindowActionGame.Infrastructure.Resources;
using System.Runtime.InteropServices;
using MultiWindowActionGame.Core.Constants;

namespace MultiWindowActionGame.Application
{
    static class Program
    {
        #region Fields
        public static Form? mainForm;
        public static IServiceContainer? ServiceContainer { get; private set; }
        private static GameApplication? _gameApplication;
        private static readonly object _lock = new object();
        #endregion

        #region Win32 API
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        #endregion

        /// <summary>
        /// アプリケーションのメインエントリポイント
        /// </summary>
        [STAThread]
        static async Task Main(string[] args)
        {
            try
            {
                // Windows Forms の初期化
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetHighDpiMode(HighDpiMode.SystemAware);

                // 引数の解析
                var options = ParseCommandLineArgs(args);

                // サービスコンテナの初期化
                await InitializeServicesAsync(options);

                // メインフォームの作成
                CreateMainForm();

                // ゲームアプリケーションの初期化と実行
                await InitializeAndRunGameAsync();
            }
            catch (Exception ex)
            {
                HandleStartupError(ex);
            }
            finally
            {
                await CleanupAsync();
            }
        }

        #region Initialization Methods

        /// <summary>
        /// サービスコンテナとサービスの初期化
        /// </summary>
        private static async Task InitializeServicesAsync(CommandLineOptions options)
        {
            lock (_lock)
            {
                if (ServiceContainer != null) return;

                // サービスコンテナの作成
                ServiceContainer = new SimpleServiceContainer();

                // コアサービスの登録
                RegisterCoreServices(options);

                // ファクトリーの登録
                ServiceContainer.RegisterFactories();

                // インフラストラクチャサービスの登録
                RegisterInfrastructureServices();

                // アプリケーションサービスの登録
                RegisterApplicationServices();
            }

            // 非同期初期化が必要なサービスの処理
            await InitializeAsyncServicesAsync();

            System.Diagnostics.Debug.WriteLine("Services initialized successfully");
        }

        /// <summary>
        /// コアサービスの登録
        /// </summary>
        private static void RegisterCoreServices(CommandLineOptions options)
        {
            // 設定サービス
            var settingsPath = options.SettingsPath ?? GameConstants.Paths.DEFAULT_SETTINGS_PATH;
            ServiceContainer!.RegisterSingleton<ISettingsService>(
                new SettingsService(settingsPath));

            // イベントサービス
            ServiceContainer.RegisterSingleton<IEventService, EventService>();

            // パフォーマンスサービス
            ServiceContainer.RegisterSingleton<IPerformanceService, PerformanceService>();

            // 入力サービス
            ServiceContainer.RegisterSingleton<IInputService, InputService>();

            System.Diagnostics.Debug.WriteLine("Core services registered");
        }

        /// <summary>
        /// インフラストラクチャサービスの登録
        /// </summary>
        private static void RegisterInfrastructureServices()
        {
            // フォント管理
            ServiceContainer!.RegisterSingleton<IFontManager, FontManager>();

            System.Diagnostics.Debug.WriteLine("Infrastructure services registered");
        }

        /// <summary>
        /// アプリケーションサービスの登録
        /// </summary>
        private static void RegisterApplicationServices()
        {
            // ゲーム固有のサービス（実装が必要）
            // ServiceContainer!.RegisterSingleton<IWindowManagerService, WindowManagerService>();
            // ServiceContainer.RegisterSingleton<IZOrderService, ZOrderService>();
            // ServiceContainer.RegisterSingleton<IPlayerService, PlayerService>();
            // ServiceContainer.RegisterSingleton<IStageManagerService, StageManagerService>();
            // ServiceContainer.RegisterSingleton<INoEntryZoneService, NoEntryZoneService>();
            // ServiceContainer.RegisterSingleton<IRenderingService, RenderingService>();
            // ServiceContainer.RegisterSingleton<IUIService, UIService>();

            System.Diagnostics.Debug.WriteLine("Application services registered");
        }

        /// <summary>
        /// 非同期初期化が必要なサービスの処理
        /// </summary>
        private static async Task InitializeAsyncServicesAsync()
        {
            // 設定の読み込み
            var settingsService = ServiceContainer!.GetRequiredService<ISettingsService>();
            await Task.Run(() => settingsService.LoadSettings());

            // その他の非同期初期化
            await Task.CompletedTask;
        }

        /// <summary>
        /// メインフォームの作成
        /// </summary>
        private static void CreateMainForm()
        {
            if (mainForm != null) return;

            mainForm = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                WindowState = FormWindowState.Maximized,
                TopMost = true,
                ShowInTaskbar = false,
                BackColor = Color.Black,
                TransparencyKey = Color.Black,
                Text = GameConstants.Messages.GAME_TITLE
            };

            // ウィンドウプロパティの設定
            ConfigureMainForm();

            System.Diagnostics.Debug.WriteLine("Main form created");
        }

        /// <summary>
        /// メインフォームの詳細設定
        /// </summary>
        private static void ConfigureMainForm()
        {
            if (mainForm == null) return;

            // Z-Orderの設定（最背面）
            var zOrderService = ServiceContainer?.GetService<IZOrderService>();
            // TODO: ZOrderServiceの実装後に有効化
            // zOrderService?.RegisterWindow(mainForm, ZOrderPriority.Bottom);

            // フォームイベントの設定
            mainForm.FormClosing += MainForm_FormClosing;
            mainForm.Shown += MainForm_Shown;

            System.Diagnostics.Debug.WriteLine("Main form configured");
        }

        /// <summary>
        /// ゲームアプリケーションの初期化と実行
        /// </summary>
        private static async Task InitializeAndRunGameAsync()
        {
            if (ServiceContainer == null || mainForm == null)
            {
                throw new InvalidOperationException("Services or main form not initialized");
            }

            // ゲームアプリケーションの作成
            _gameApplication = new GameApplication(ServiceContainer);

            // ゲームの初期化
            await _gameApplication.InitializeAsync();

            // メインフォームの表示
            mainForm.Show();

            // イベントサービスでゲーム開始を通知
            var eventService = ServiceContainer.GetRequiredService<IEventService>();
            eventService.Publish(new GameEvents.GameStartedEvent());

            // ゲームループの開始（非同期）
            var gameLoopTask = _gameApplication.RunGameLoopAsync();

            // Windows Forms メッセージループの実行
            Application.Run(mainForm);

            // ゲームループの完了を待機
            await gameLoopTask;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// メインフォーム表示イベント
        /// </summary>
        private static async void MainForm_Shown(object? sender, EventArgs e)
        {
            if (_gameApplication != null)
            {
                await _gameApplication.OnMainFormShownAsync();
            }
        }

        /// <summary>
        /// メインフォーム終了イベント
        /// </summary>
        private static async void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // ゲームの終了処理
            if (_gameApplication != null)
            {
                await _gameApplication.ShutdownAsync();
            }

            // イベント通知
            var eventService = ServiceContainer?.GetService<IEventService>();
            eventService?.Publish(new GameEvents.GameEndedEvent());
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// コマンドライン引数の解析
        /// </summary>
        private static CommandLineOptions ParseCommandLineArgs(string[] args)
        {
            var options = new CommandLineOptions();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--debug":
                    case "-d":
                        options.IsDebugMode = true;
                        break;

                    case "--settings":
                    case "-s":
                        if (i + 1 < args.Length)
                        {
                            options.SettingsPath = args[++i];
                        }
                        break;

                    case "--stage":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var stageNumber))
                        {
                            options.StartStage = stageNumber;
                        }
                        break;

                    case "--windowed":
                    case "-w":
                        options.IsFullscreen = false;
                        break;

                    case "--help":
                    case "-h":
                        ShowHelp();
                        Environment.Exit(0);
                        break;
                }
            }

            return options;
        }

        /// <summary>
        /// ヘルプメッセージの表示
        /// </summary>
        private static void ShowHelp()
        {
            var helpText = $@"
{GameConstants.Messages.GAME_TITLE}

Usage: MultiWindowActionGame.exe [options]

Options:
  --debug, -d          Enable debug mode
  --settings, -s PATH  Use custom settings file path
  --stage NUMBER       Start from specific stage number
  --windowed, -w       Run in windowed mode
  --help, -h           Show this help message

Examples:
  MultiWindowActionGame.exe --debug --stage 5
  MultiWindowActionGame.exe --settings custom_settings.json
";

            MessageBox.Show(helpText, "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 起動エラーの処理
        /// </summary>
        private static void HandleStartupError(Exception ex)
        {
            var errorMessage = $"Failed to start the game:\n\n{ex.Message}";

            System.Diagnostics.Debug.WriteLine($"Startup error: {ex}");

            MessageBox.Show(
                errorMessage,
                "Startup Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            Environment.Exit(1);
        }

        /// <summary>
        /// アプリケーション終了時のクリーンアップ
        /// </summary>
        private static async Task CleanupAsync()
        {
            try
            {
                // ゲームアプリケーションの破棄
                if (_gameApplication != null)
                {
                    await _gameApplication.DisposeAsync();
                    _gameApplication = null;
                }

                // サービスコンテナの破棄
                ServiceContainer?.Dispose();
                ServiceContainer = null;

                // メインフォームの破棄
                mainForm?.Dispose();
                mainForm = null;

                System.Diagnostics.Debug.WriteLine("Cleanup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// アプリケーションの緊急停止
        /// </summary>
        public static async Task EmergencyShutdownAsync()
        {
            try
            {
                await CleanupAsync();
            }
            finally
            {
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// サービスの取得（null安全）
        /// </summary>
        public static T? GetService<T>() where T : class
        {
            return ServiceContainer?.GetService<T>();
        }

        /// <summary>
        /// 必須サービスの取得
        /// </summary>
        public static T GetRequiredService<T>() where T : class
        {
            return ServiceContainer?.GetRequiredService<T>()
                ?? throw new InvalidOperationException($"Service {typeof(T).Name} is not registered");
        }

        /// <summary>
        /// デバッグモードの設定
        /// </summary>
        public static void SetDebugMode(bool enabled)
        {
            MainGame.IsDebugMode = enabled;

            var eventService = ServiceContainer?.GetService<IEventService>();
            eventService?.Publish(new GameEvents.DebugModeChangedEvent { IsDebugMode = enabled });
        }

        #endregion
    }

    /// <summary>
    /// コマンドライン引数のオプション
    /// </summary>
    public class CommandLineOptions
    {
        public bool IsDebugMode { get; set; } = false;
        public string? SettingsPath { get; set; }
        public int? StartStage { get; set; }
        public bool IsFullscreen { get; set; } = true;
    }
}