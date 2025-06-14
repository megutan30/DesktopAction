// Core/Services/ServiceRegistration.cs
using MultiWindowActionGame.Core.DependencyInjection;
using MultiWindowActionGame.Core.Services.Interfaces;
using MultiWindowActionGame.Infrastructure.Resources;

namespace MultiWindowActionGame.Core.Services
{
    /// <summary>
    /// ゲームサービスの依存性注入登録
    /// </summary>
    public static class ServiceRegistration
    {
        /// <summary>
        /// すべてのゲームサービスを依存性注入コンテナに登録
        /// </summary>
        public static IServiceContainer RegisterGameServices(this IServiceContainer container)
        {
            ArgumentNullException.ThrowIfNull(container);

            // 基本サービス（他のサービスに依存しない）
            container.RegisterSingleton<IEventService, EventService>();
            container.RegisterSingleton<IPerformanceService, PerformanceService>();
            container.RegisterSingleton<ISettingsService, SettingsService>();
            container.RegisterSingleton<IFontManager, FontManager>();

            // Z-Order管理（EventServiceに依存）
            container.RegisterSingleton<IZOrderService, ZOrderService>();

            // 不可侵領域管理（EventServiceに依存）
            container.RegisterSingleton<INoEntryZoneService, NoEntryZoneService>();

            // 衝突判定（NoEntryZoneService、EventServiceに依存）
            container.RegisterSingleton<IWindowCollisionService, WindowCollisionService>();

            // 階層管理（WindowCollisionService、EventServiceに依存）
            container.RegisterSingleton<IWindowHierarchyService, WindowHierarchyService>();

            // ウィンドウ管理統合サービス（上記すべてに依存）
            container.RegisterSingleton<IWindowManagerService, WindowManagerService>();

            // 入力管理
            container.RegisterSingleton<IInputService>(serviceProvider =>
                new InputService(serviceProvider.GetRequiredService<IEventService>()));

            // 描画管理
            container.RegisterSingleton<IRenderingService>(serviceProvider =>
                new RenderingService(serviceProvider.GetRequiredService<IEventService>()));

            // UI管理
            container.RegisterSingleton<IUIService>(serviceProvider =>
                new UIService(serviceProvider.GetRequiredService<IEventService>()));

            // プレイヤー管理
            container.RegisterSingleton<IPlayerService>(serviceProvider =>
                new PlayerService(
                    serviceProvider.GetRequiredService<IEventService>(),
                    serviceProvider.GetRequiredService<IWindowManagerService>()));

            // ステージ管理
            container.RegisterSingleton<IStageManagerService>(serviceProvider =>
                new StageManagerService(
                    serviceProvider.GetRequiredService<IEventService>(),
                    serviceProvider.GetRequiredService<IWindowManagerService>(),
                    serviceProvider.GetRequiredService<IPlayerService>(),
                    serviceProvider.GetRequiredService<INoEntryZoneService>()));

            return container;
        }

        /// <summary>
        /// 開発・デバッグ用サービスを追加登録
        /// </summary>
        public static IServiceContainer RegisterDebugServices(this IServiceContainer container)
        {
            ArgumentNullException.ThrowIfNull(container);

            // パフォーマンス分析器
            container.RegisterSingleton<PerformanceAnalyzer>(serviceProvider =>
                new PerformanceAnalyzer(
                    serviceProvider.GetRequiredService<IPerformanceService>(),
                    serviceProvider.GetRequiredService<IEventService>()));

            // プロファイラー
            container.RegisterSingleton<Profiler>();

            // 設定監視
            container.RegisterSingleton<SettingsWatcher>(serviceProvider =>
                new SettingsWatcher(serviceProvider.GetRequiredService<ISettingsService>()));

            // 設定プリセット管理
            container.RegisterSingleton<SettingsPresetManager>(serviceProvider =>
                new SettingsPresetManager(serviceProvider.GetRequiredService<ISettingsService>()));

            return container;
        }

        /// <summary>
        /// サービスの初期化を実行
        /// </summary>
        public static async Task InitializeServicesAsync(this IServiceContainer container)
        {
            ArgumentNullException.ThrowIfNull(container);

            // 設定サービスの初期化
            var settingsService = container.GetRequiredService<ISettingsService>();
            settingsService.LoadSettings();

            // フォントマネージャーの初期化
            var fontManager = container.GetRequiredService<IFontManager>();
            // フォントは既にコンストラクタで初期化済み

            // パフォーマンスサービスの初期化
            var performanceService = container.GetRequiredService<IPerformanceService>();
            performanceService.SetEnabled(true);

            // イベントサービスでシステムイベントをセットアップ
            var eventService = container.GetRequiredService<IEventService>();
            SetupSystemEvents(eventService);

            System.Diagnostics.Debug.WriteLine("All game services initialized successfully");
        }

        /// <summary>
        /// サービスの健全性チェック
        /// </summary>
        public static ServiceHealthReport CheckServiceHealth(this IServiceContainer container)
        {
            var report = new ServiceHealthReport();

            try
            {
                // 必須サービスの健全性チェック
                var criticalServices = new[]
                {
                    typeof(IEventService),
                    typeof(ISettingsService),
                    typeof(IWindowManagerService),
                    typeof(IZOrderService),
                    typeof(INoEntryZoneService)
                };

                foreach (var serviceType in criticalServices)
                {
                    try
                    {
                        var service = container.GetRequiredService(serviceType);
                        if (service != null)
                        {
                            report.HealthyServices.Add(serviceType.Name);
                        }
                        else
                        {
                            report.UnhealthyServices.Add(serviceType.Name, "Service is null");
                        }
                    }
                    catch (Exception ex)
                    {
                        report.UnhealthyServices.Add(serviceType.Name, ex.Message);
                    }
                }

                // パフォーマンスサービスの詳細チェック
                try
                {
                    var performanceService = container.GetRequiredService<IPerformanceService>();
                    if (performanceService.CurrentFPS < 10)
                    {
                        report.Warnings.Add("Low FPS detected in performance service");
                    }
                }
                catch (Exception ex)
                {
                    report.Warnings.Add($"Performance service check failed: {ex.Message}");
                }

                report.IsHealthy = !report.UnhealthyServices.Any();
            }
            catch (Exception ex)
            {
                report.IsHealthy = false;
                report.UnhealthyServices.Add("ServiceContainer", ex.Message);
            }

            return report;
        }

        /// <summary>
        /// 登録されたサービスの概要を取得
        /// </summary>
        public static ServiceRegistrationSummary GetServiceSummary(this IServiceContainer container)
        {
            var summary = new ServiceRegistrationSummary
            {
                RegisteredServices = container.GetRegisteredServices().ToList(),
                TotalServices = container.GetRegisteredServices().Count(),
                GeneratedAt = DateTime.Now
            };

            // サービスの分類
            foreach (var serviceType in summary.RegisteredServices)
            {
                if (serviceType.Name.Contains("Service"))
                {
                    summary.CoreServices++;
                }
                else if (serviceType.Name.Contains("Manager"))
                {
                    summary.ManagerServices++;
                }
                else if (serviceType.Name.Contains("Debug") || serviceType.Name.Contains("Performance"))
                {
                    summary.DebugServices++;
                }
                else
                {
                    summary.UtilityServices++;
                }
            }

            return summary;
        }

        private static void SetupSystemEvents(IEventService eventService)
        {
            // ゲーム開始イベントの設定
            eventService.Subscribe<GameStartedEvent>(OnGameStarted);
            eventService.Subscribe<GameEndedEvent>(OnGameEnded);
            
            // パフォーマンス警告の設定
            eventService.Subscribe<PerformanceWarningEvent>(OnPerformanceWarning);
            
            // 設定変更の設定
            eventService.Subscribe<SettingChangedEventArgs>(OnSettingChanged);

            System.Diagnostics.Debug.WriteLine("System events configured");
        }

        private static void OnGameStarted(GameStartedEvent eventArgs)
        {
            System.Diagnostics.Debug.WriteLine($"Game started at {eventArgs.StartTime}");
        }

        private static void OnGameEnded(GameEndedEvent eventArgs)
        {
            System.Diagnostics.Debug.WriteLine($"Game ended at {eventArgs.EndTime}, total play time: {eventArgs.TotalPlayTime}");
        }

        private static void OnPerformanceWarning(PerformanceWarningEvent eventArgs)
        {
            System.Diagnostics.Debug.WriteLine($"Performance warning: {eventArgs.WarningType}, FPS: {eventArgs.CurrentFPS}");
        }

        private static void OnSettingChanged(SettingChangedEventArgs eventArgs)
        {
            System.Diagnostics.Debug.WriteLine($"Setting changed: {eventArgs.Key} = {eventArgs.NewValue}");
        }
    }

    /// <summary>
    /// サービスの健全性レポート
    /// </summary>
    public class ServiceHealthReport
    {
        public bool IsHealthy { get; set; }
        public List<string> HealthyServices { get; set; } = new();
        public Dictionary<string, string> UnhealthyServices { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public DateTime CheckedAt { get; set; } = DateTime.Now;

        public string GenerateReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"Service Health Report - {CheckedAt:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Overall Status: {(IsHealthy ? "HEALTHY" : "UNHEALTHY")}");
            report.AppendLine();

            if (HealthyServices.Any())
            {
                report.AppendLine("Healthy Services:");
                foreach (var service in HealthyServices)
                {
                    report.AppendLine($"  ✓ {service}");
                }
                report.AppendLine();
            }

            if (UnhealthyServices.Any())
            {
                report.AppendLine("Unhealthy Services:");
                foreach (var kvp in UnhealthyServices)
                {
                    report.AppendLine($"  ✗ {kvp.Key}: {kvp.Value}");
                }
                report.AppendLine();
            }

            if (Warnings.Any())
            {
                report.AppendLine("Warnings:");
                foreach (var warning in Warnings)
                {
                    report.AppendLine($"  ⚠ {warning}");
                }
            }

            return report.ToString();
        }
    }

    /// <summary>
    /// サービス登録概要
    /// </summary>
    public class ServiceRegistrationSummary
    {
        public List<Type> RegisteredServices { get; set; } = new();
        public int TotalServices { get; set; }
        public int CoreServices { get; set; }
        public int ManagerServices { get; set; }
        public int DebugServices { get; set; }
        public int UtilityServices { get; set; }
        public DateTime GeneratedAt { get; set; }

        public string GenerateReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"Service Registration Summary - {GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Total Services: {TotalServices}");
            report.AppendLine($"Core Services: {CoreServices}");
            report.AppendLine($"Manager Services: {ManagerServices}");
            report.AppendLine($"Debug Services: {DebugServices}");
            report.AppendLine($"Utility Services: {UtilityServices}");
            report.AppendLine();

            report.AppendLine("Registered Service Types:");
            foreach (var serviceType in RegisteredServices.OrderBy(t => t.Name))
            {
                report.AppendLine($"  - {serviceType.Name}");
            }

            return report.ToString();
        }
    }

    /// <summary>
    /// サービス登録の拡張メソッド
    /// </summary>
    public static class ServiceContainerExtensions
    {
        /// <summary>
        /// サービスをオプショナルで取得（見つからない場合はnullを返す）
        /// </summary>
        public static T? GetOptionalService<T>(this IServiceContainer container) where T : class
        {
            try
            {
                return container.GetService<T>();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 複数のサービスを一度に取得
        /// </summary>
        public static (T1, T2) GetServices<T1, T2>(this IServiceContainer container)
            where T1 : class
            where T2 : class
        {
            return (container.GetRequiredService<T1>(), container.GetRequiredService<T2>());
        }

        /// <summary>
        /// 複数のサービスを一度に取得（3つ）
        /// </summary>
        public static (T1, T2, T3) GetServices<T1, T2, T3>(this IServiceContainer container)
            where T1 : class
            where T2 : class
            where T3 : class
        {
            return (
                container.GetRequiredService<T1>(),
                container.GetRequiredService<T2>(),
                container.GetRequiredService<T3>()
            );
        }

        /// <summary>
        /// サービスが登録されているかチェックして取得
        /// </summary>
        public static bool TryGetService<T>(this IServiceContainer container, out T? service) where T : class
        {
            try
            {
                service = container.GetService<T>();
                return service != null;
            }
            catch
            {
                service = null;
                return false;
            }
        }

        /// <summary>
        /// 条件付きサービス登録
        /// </summary>
        public static IServiceContainer RegisterIf<TInterface, TImplementation>(
            this IServiceContainer container,
            Func<bool> condition)
            where TImplementation : class, TInterface
        {
            if (condition())
            {
                container.RegisterSingleton<TInterface, TImplementation>();
            }
            return container;
        }

        /// <summary>
        /// 環境依存サービス登録
        /// </summary>
        public static IServiceContainer RegisterForEnvironment<TInterface, TImplementation>(
            this IServiceContainer container,
            string environment)
            where TImplementation : class, TInterface
        {
            var currentEnvironment = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "Production";
            
            if (string.Equals(currentEnvironment, environment, StringComparison.OrdinalIgnoreCase))
            {
                container.RegisterSingleton<TInterface, TImplementation>();
            }
            
            return container;
        }
    }

    // ===== イベントクラス（参照用） =====

    public class GameStartedEvent
    {
        public DateTime StartTime { get; set; } = DateTime.Now;
    }

    public class GameEndedEvent
    {
        public DateTime EndTime { get; set; } = DateTime.Now;
        public TimeSpan TotalPlayTime { get; set; }
    }

    public class PerformanceWarningEvent
    {
        public string WarningType { get; set; } = "";
        public float CurrentFPS { get; set; }
        public TimeSpan FrameTime { get; set; }
    }
}