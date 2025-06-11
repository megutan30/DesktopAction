// Core/Services/SettingsService.cs
using MultiWindowActionGame.Core.Services.Interfaces;
using MultiWindowActionGame.Core.Constants;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MultiWindowActionGame.Core.Services
{
    /// <summary>
    /// 設定管理サービスの実装
    /// </summary>
    public class SettingsService : ISettingsService, IDisposable
    {
        private readonly Dictionary<string, object> _settings = new();
        private readonly FileSystemWatcher? _fileWatcher;
        private readonly string _settingsPath;
        private readonly object _lock = new();
        private bool _disposed = false;

        public event EventHandler<SettingChangedEventArgs>? SettingChanged;

        public SettingsService(string? settingsPath = null)
        {
            _settingsPath = settingsPath ?? GameConstants.Paths.DEFAULT_SETTINGS_PATH;
            
            LoadDefaultSettings();
            LoadSettings();
            InitializeFileWatcher();
        }

        public T GetSetting<T>(string key, T defaultValue = default!)
        {
            ThrowIfDisposed();
            ArgumentException.ThrowIfNullOrEmpty(key);

            lock (_lock)
            {
                if (_settings.TryGetValue(key, out var value))
                {
                    try
                    {
                        if (value is JsonElement jsonElement)
                        {
                            return JsonSerializer.Deserialize<T>(jsonElement.GetRawText()) ?? defaultValue;
                        }
                        else if (value is T directValue)
                        {
                            return directValue;
                        }
                        else if (value != null)
                        {
                            return (T)Convert.ChangeType(value, typeof(T)) ?? defaultValue;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error converting setting '{key}': {ex.Message}");
                    }
                }

                return defaultValue;
            }
        }

        public void SetSetting<T>(string key, T value)
        {
            ThrowIfDisposed();
            ArgumentException.ThrowIfNullOrEmpty(key);

            lock (_lock)
            {
                var oldValue = _settings.TryGetValue(key, out var existing) ? existing : null;
                _settings[key] = value!;

                // イベントを発行
                SettingChanged?.Invoke(this, new SettingChangedEventArgs(key, oldValue, value));
            }
        }

        public void SaveSettings()
        {
            ThrowIfDisposed();

            try
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new JsonStringEnumConverter() }
                };

                var settingsData = CreateSettingsData();
                var json = JsonSerializer.Serialize(settingsData, options);

                // ファイルウォッチャーを一時的に無効にして自己更新を防ぐ
                var wasWatcherEnabled = _fileWatcher?.EnableRaisingEvents ?? false;
                if (_fileWatcher != null)
                {
                    _fileWatcher.EnableRaisingEvents = false;
                }

                try
                {
                    File.WriteAllText(_settingsPath, json);
                    System.Diagnostics.Debug.WriteLine($"Settings saved to: {_settingsPath}");
                }
                finally
                {
                    if (_fileWatcher != null)
                    {
                        _fileWatcher.EnableRaisingEvents = wasWatcherEnabled;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
                throw new InvalidOperationException($"Failed to save settings: {ex.Message}", ex);
            }
        }

        public void LoadSettings()
        {
            ThrowIfDisposed();

            try
            {
                if (!File.Exists(_settingsPath))
                {
                    System.Diagnostics.Debug.WriteLine($"Settings file not found: {_settingsPath}. Using defaults.");
                    return;
                }

                var json = File.ReadAllText(_settingsPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new JsonStringEnumConverter() }
                };

                var settingsData = JsonSerializer.Deserialize<GameSettingsData>(json, options);
                if (settingsData != null)
                {
                    ApplySettingsData(settingsData);
                    System.Diagnostics.Debug.WriteLine($"Settings loaded from: {_settingsPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}. Using defaults.");
                LoadDefaultSettings();
            }
        }

        public void ResetToDefaults()
        {
            ThrowIfDisposed();

            lock (_lock)
            {
                var oldSettings = new Dictionary<string, object>(_settings);
                _settings.Clear();
                LoadDefaultSettings();

                // 変更イベントを発行
                foreach (var kvp in oldSettings)
                {
                    var newValue = _settings.TryGetValue(kvp.Key, out var value) ? value : null;
                    if (!Equals(kvp.Value, newValue))
                    {
                        SettingChanged?.Invoke(this, new SettingChangedEventArgs(kvp.Key, kvp.Value, newValue));
                    }
                }
            }
        }

        public bool HasSetting(string key)
        {
            ThrowIfDisposed();
            ArgumentException.ThrowIfNullOrEmpty(key);

            lock (_lock)
            {
                return _settings.ContainsKey(key);
            }
        }

        public void RemoveSetting(string key)
        {
            ThrowIfDisposed();
            ArgumentException.ThrowIfNullOrEmpty(key);

            lock (_lock)
            {
                if (_settings.TryGetValue(key, out var oldValue))
                {
                    _settings.Remove(key);
                    SettingChanged?.Invoke(this, new SettingChangedEventArgs(key, oldValue, null));
                }
            }
        }

        public IReadOnlyDictionary<string, object> GetAllSettings()
        {
            ThrowIfDisposed();

            lock (_lock)
            {
                return new Dictionary<string, object>(_settings);
            }
        }

        private void LoadDefaultSettings()
        {
            // プレイヤー設定
            SetSetting("Player.MovementSpeed", GameConstants.Player.DEFAULT_MOVEMENT_SPEED);
            SetSetting("Player.Gravity", GameConstants.Player.DEFAULT_GRAVITY);
            SetSetting("Player.JumpForce", GameConstants.Player.DEFAULT_JUMP_FORCE);
            SetSetting("Player.DefaultSize.Width", GameConstants.Player.DEFAULT_WIDTH);
            SetSetting("Player.DefaultSize.Height", GameConstants.Player.DEFAULT_HEIGHT);
            SetSetting("Player.GroundCheckHeight", GameConstants.Player.GROUND_CHECK_HEIGHT);

            // ウィンドウ設定
            SetSetting("Window.MinimumSize.Width", GameConstants.Window.MINIMUM_WIDTH);
            SetSetting("Window.MinimumSize.Height", GameConstants.Window.MINIMUM_HEIGHT);

            // ゲームプレイ設定
            SetSetting("Gameplay.TargetFPS", GameConstants.Gameplay.DEFAULT_TARGET_FPS);
            SetSetting("Gameplay.WindowSnapDistance", GameConstants.Physics.DEFAULT_WINDOW_SNAP_DISTANCE);

            // UI設定
            SetSetting("UI.DebugMode", false);
            SetSetting("UI.ShowPerformanceInfo", false);
            SetSetting("UI.FontSize", GameConstants.UI.BUTTON_FONT_SIZE);

            // 音量設定
            SetSetting("Audio.MasterVolume", 1.0f);
            SetSetting("Audio.SoundVolume", 1.0f);
            SetSetting("Audio.MusicVolume", 0.7f);
            SetSetting("Audio.Enabled", true);
        }

        private GameSettingsData CreateSettingsData()
        {
            lock (_lock)
            {
                return new GameSettingsData
                {
                    Player = new PlayerSettingsData
                    {
                        MovementSpeed = GetSetting<float>("Player.MovementSpeed"),
                        Gravity = GetSetting<float>("Player.Gravity"),
                        JumpForce = GetSetting<float>("Player.JumpForce"),
                        DefaultSize = new SizeData
                        {
                            Width = GetSetting<int>("Player.DefaultSize.Width"),
                            Height = GetSetting<int>("Player.DefaultSize.Height")
                        },
                        GroundCheckHeight = GetSetting<int>("Player.GroundCheckHeight")
                    },
                    Window = new WindowSettingsData
                    {
                        MinimumSize = new SizeData
                        {
                            Width = GetSetting<int>("Window.MinimumSize.Width"),
                            Height = GetSetting<int>("Window.MinimumSize.Height")
                        }
                    },
                    Gameplay = new GameplaySettingsData
                    {
                        TargetFPS = GetSetting<int>("Gameplay.TargetFPS"),
                        WindowSnapDistance = GetSetting<float>("Gameplay.WindowSnapDistance")
                    },
                    UI = new UISettingsData
                    {
                        DebugMode = GetSetting<bool>("UI.DebugMode"),
                        ShowPerformanceInfo = GetSetting<bool>("UI.ShowPerformanceInfo"),
                        FontSize = GetSetting<int>("UI.FontSize")
                    },
                    Audio = new AudioSettingsData
                    {
                        MasterVolume = GetSetting<float>("Audio.MasterVolume"),
                        SoundVolume = GetSetting<float>("Audio.SoundVolume"),
                        MusicVolume = GetSetting<float>("Audio.MusicVolume"),
                        Enabled = GetSetting<bool>("Audio.Enabled")
                    }
                };
            }
        }

        private void ApplySettingsData(GameSettingsData data)
        {
            lock (_lock)
            {
                // プレイヤー設定
                if (data.Player != null)
                {
                    SetSetting("Player.MovementSpeed", data.Player.MovementSpeed);
                    SetSetting("Player.Gravity", data.Player.Gravity);
                    SetSetting("Player.JumpForce", data.Player.JumpForce);
                    SetSetting("Player.DefaultSize.Width", data.Player.DefaultSize.Width);
                    SetSetting("Player.DefaultSize.Height", data.Player.DefaultSize.Height);
                    SetSetting("Player.GroundCheckHeight", data.Player.GroundCheckHeight);
                }

                // ウィンドウ設定
                if (data.Window != null)
                {
                    SetSetting("Window.MinimumSize.Width", data.Window.MinimumSize.Width);
                    SetSetting("Window.MinimumSize.Height", data.Window.MinimumSize.Height);
                }

                // ゲームプレイ設定
                if (data.Gameplay != null)
                {
                    SetSetting("Gameplay.TargetFPS", data.Gameplay.TargetFPS);
                    SetSetting("Gameplay.WindowSnapDistance", data.Gameplay.WindowSnapDistance);
                }

                // UI設定
                if (data.UI != null)
                {
                    SetSetting("UI.DebugMode", data.UI.DebugMode);
                    SetSetting("UI.ShowPerformanceInfo", data.UI.ShowPerformanceInfo);
                    SetSetting("UI.FontSize", data.UI.FontSize);
                }

                // 音声設定
                if (data.Audio != null)
                {
                    SetSetting("Audio.MasterVolume", data.Audio.MasterVolume);
                    SetSetting("Audio.SoundVolume", data.Audio.SoundVolume);
                    SetSetting("Audio.MusicVolume", data.Audio.MusicVolume);
                    SetSetting("Audio.Enabled", data.Audio.Enabled);
                }
            }
        }

        private void InitializeFileWatcher()
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                var filename = Path.GetFileName(_settingsPath);

                if (!string.IsNullOrEmpty(directory) && !string.IsNullOrEmpty(filename))
                {
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var watcher = new FileSystemWatcher(directory, filename)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                        EnableRaisingEvents = true
                    };

                    watcher.Changed += OnSettingsFileChanged;
                    
                    // フィールドに代入（ここで初期化完了）
                    _fileWatcher = watcher;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize file watcher: {ex.Message}");
                // ファイルウォッチャーが失敗しても設定サービス自体は動作する
            }
        }

        private void OnSettingsFileChanged(object sender, FileSystemEventArgs e)
        {
            if (_disposed) return;

            // ファイル変更の重複イベントを避けるため少し待機
            Task.Delay(100).ContinueWith(_ =>
            {
                if (!_disposed)
                {
                    try
                    {
                        LoadSettings();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to reload settings: {ex.Message}");
                    }
                }
            });
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SettingsService));
        }

        public void Dispose()
        {
            if (_disposed) return;

            _fileWatcher?.Dispose();
            
            lock (_lock)
            {
                _settings.Clear();
            }
            
            _disposed = true;
        }
    }

    // ===== データクラス（JSON シリアライゼーション用） =====

    public class GameSettingsData
    {
        public PlayerSettingsData Player { get; set; } = new();
        public WindowSettingsData Window { get; set; } = new();
        public GameplaySettingsData Gameplay { get; set; } = new();
        public UISettingsData UI { get; set; } = new();
        public AudioSettingsData Audio { get; set; } = new();
    }

    public class PlayerSettingsData
    {
        public float MovementSpeed { get; set; }
        public float Gravity { get; set; }
        public float JumpForce { get; set; }
        public SizeData DefaultSize { get; set; } = new();
        public int GroundCheckHeight { get; set; }
    }

    public class WindowSettingsData
    {
        public SizeData MinimumSize { get; set; } = new();
    }

    public class UISettingsData
    {
        public bool DebugMode { get; set; }
        public bool ShowPerformanceInfo { get; set; }
        public int FontSize { get; set; }
    }

    public class AudioSettingsData
    {
        public float MasterVolume { get; set; }
        public float SoundVolume { get; set; }
        public float MusicVolume { get; set; }
        public bool Enabled { get; set; }
    }

    public class SizeData
    {
        public int Width { get; set; }
        public int Height { get; set; }

        public Size ToSize() => new(Width, Height);
        public static SizeData FromSize(Size size) => new() { Width = size.Width, Height = size.Height };
    }

    /// <summary>
    /// 設定検証ユーティリティ
    /// </summary>
    public static class SettingsValidator
    {
        /// <summary>
        /// プレイヤー設定を検証する
        /// </summary>
        public static ValidationResult ValidatePlayerSettings(PlayerSettingsData settings)
        {
            var errors = new List<string>();

            if (settings.MovementSpeed <= 0 || settings.MovementSpeed > 2000)
                errors.Add("Movement speed must be between 1 and 2000");

            if (settings.Gravity <= 0 || settings.Gravity > 5000)
                errors.Add("Gravity must be between 1 and 5000");

            if (settings.JumpForce <= 0 || settings.JumpForce > 2000)
                errors.Add("Jump force must be between 1 and 2000");

            if (settings.DefaultSize.Width < 5 || settings.DefaultSize.Width > 200)
                errors.Add("Default width must be between 5 and 200");

            if (settings.DefaultSize.Height < 5 || settings.DefaultSize.Height > 200)
                errors.Add("Default height must be between 5 and 200");

            if (settings.GroundCheckHeight < 1 || settings.GroundCheckHeight > 50)
                errors.Add("Ground check height must be between 1 and 50");

            return new ValidationResult { IsValid = errors.Count == 0, Errors = errors };
        }

        /// <summary>
        /// ウィンドウ設定を検証する
        /// </summary>
        public static ValidationResult ValidateWindowSettings(WindowSettingsData settings)
        {
            var errors = new List<string>();

            if (settings.MinimumSize.Width < 50 || settings.MinimumSize.Width > 1000)
                errors.Add("Minimum window width must be between 50 and 1000");

            if (settings.MinimumSize.Height < 50 || settings.MinimumSize.Height > 1000)
                errors.Add("Minimum window height must be between 50 and 1000");

            return new ValidationResult { IsValid = errors.Count == 0, Errors = errors };
        }

        /// <summary>
        /// ゲームプレイ設定を検証する
        /// </summary>
        public static ValidationResult ValidateGameplaySettings(GameplaySettingsData settings)
        {
            var errors = new List<string>();

            if (settings.TargetFPS < 10 || settings.TargetFPS > 240)
                errors.Add("Target FPS must be between 10 and 240");

            if (settings.WindowSnapDistance < 0 || settings.WindowSnapDistance > 100)
                errors.Add("Window snap distance must be between 0 and 100");

            return new ValidationResult { IsValid = errors.Count == 0, Errors = errors };
        }

        /// <summary>
        /// 音声設定を検証する
        /// </summary>
        public static ValidationResult ValidateAudioSettings(AudioSettingsData settings)
        {
            var errors = new List<string>();

            if (settings.MasterVolume < 0 || settings.MasterVolume > 1)
                errors.Add("Master volume must be between 0 and 1");

            if (settings.SoundVolume < 0 || settings.SoundVolume > 1)
                errors.Add("Sound volume must be between 0 and 1");

            if (settings.MusicVolume < 0 || settings.MusicVolume > 1)
                errors.Add("Music volume must be between 0 and 1");

            return new ValidationResult { IsValid = errors.Count == 0, Errors = errors };
        }

        /// <summary>
        /// すべての設定を検証する
        /// </summary>
        public static ValidationResult ValidateAllSettings(GameSettingsData settings)
        {
            var allErrors = new List<string>();

            var playerResult = ValidatePlayerSettings(settings.Player);
            if (!playerResult.IsValid)
                allErrors.AddRange(playerResult.Errors.Select(e => $"Player: {e}"));

            var windowResult = ValidateWindowSettings(settings.Window);
            if (!windowResult.IsValid)
                allErrors.AddRange(windowResult.Errors.Select(e => $"Window: {e}"));

            var gameplayResult = ValidateGameplaySettings(settings.Gameplay);
            if (!gameplayResult.IsValid)
                allErrors.AddRange(gameplayResult.Errors.Select(e => $"Gameplay: {e}"));

            var audioResult = ValidateAudioSettings(settings.Audio);
            if (!audioResult.IsValid)
                allErrors.AddRange(audioResult.Errors.Select(e => $"Audio: {e}"));

            return new ValidationResult { IsValid = allErrors.Count == 0, Errors = allErrors };
        }
    }

    /// <summary>
    /// 検証結果
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public IReadOnlyList<string> Errors { get; set; } = new List<string>();

        public string GetErrorMessage()
        {
            return string.Join(Environment.NewLine, Errors);
        }
    }

    /// <summary>
    /// 設定変更監視クラス
    /// </summary>
    public class SettingsWatcher : IDisposable
    {
        private readonly ISettingsService _settingsService;
        private readonly Dictionary<string, Action<object?>> _watchers = new();
        private bool _disposed = false;

        public SettingsWatcher(ISettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _settingsService.SettingChanged += OnSettingChanged;
        }

        /// <summary>
        /// 特定の設定の変更を監視する
        /// </summary>
        public void Watch<T>(string key, Action<T> callback)
        {
            ThrowIfDisposed();
            ArgumentException.ThrowIfNullOrEmpty(key);
            ArgumentNullException.ThrowIfNull(callback);

            _watchers[key] = value =>
            {
                if (value is T typedValue)
                {
                    callback(typedValue);
                }
                else if (value != null)
                {
                    try
                    {
                        var convertedValue = (T)Convert.ChangeType(value, typeof(T));
                        callback(convertedValue);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to convert setting value for {key}: {ex.Message}");
                    }
                }
            };
        }

        /// <summary>
        /// 設定の監視を停止する
        /// </summary>
        public void Unwatch(string key)
        {
            ThrowIfDisposed();
            _watchers.Remove(key);
        }

        /// <summary>
        /// すべての監視を停止する
        /// </summary>
        public void UnwatchAll()
        {
            ThrowIfDisposed();
            _watchers.Clear();
        }

        private void OnSettingChanged(object? sender, SettingChangedEventArgs e)
        {
            if (_watchers.TryGetValue(e.Key, out var callback))
            {
                try
                {
                    callback(e.NewValue);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in settings watcher for {e.Key}: {ex.Message}");
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SettingsWatcher));
        }

        public void Dispose()
        {
            if (_disposed) return;

            _settingsService.SettingChanged -= OnSettingChanged;
            _watchers.Clear();
            _disposed = true;
        }
    }

    /// <summary>
    /// 設定プリセット管理
    /// </summary>
    public class SettingsPresetManager
    {
        private readonly ISettingsService _settingsService;
        private readonly Dictionary<string, GameSettingsData> _presets = new();

        public SettingsPresetManager(ISettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            LoadBuiltInPresets();
        }

        /// <summary>
        /// プリセットを保存する
        /// </summary>
        public void SavePreset(string name, GameSettingsData settings)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(settings);

            var validationResult = SettingsValidator.ValidateAllSettings(settings);
            if (!validationResult.IsValid)
            {
                throw new ArgumentException($"Invalid settings: {validationResult.GetErrorMessage()}");
            }

            _presets[name] = settings;
        }

        /// <summary>
        /// プリセットを読み込む
        /// </summary>
        public void LoadPreset(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            if (!_presets.TryGetValue(name, out var preset))
            {
                throw new ArgumentException($"Preset '{name}' not found");
            }

            ApplyPreset(preset);
        }

        /// <summary>
        /// 利用可能なプリセット名を取得する
        /// </summary>
        public IReadOnlyList<string> GetPresetNames()
        {
            return _presets.Keys.ToList();
        }

        /// <summary>
        /// プリセットを削除する
        /// </summary>
        public bool RemovePreset(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            return _presets.Remove(name);
        }

        /// <summary>
        /// 現在の設定からプリセットを作成する
        /// </summary>
        public GameSettingsData CreatePresetFromCurrentSettings()
        {
            var allSettings = _settingsService.GetAllSettings();
            
            // 現在の設定からGameSettingsDataを構築
            return new GameSettingsData
            {
                Player = new PlayerSettingsData
                {
                    MovementSpeed = GetSettingValue<float>(allSettings, "Player.MovementSpeed", GameConstants.Player.DEFAULT_MOVEMENT_SPEED),
                    Gravity = GetSettingValue<float>(allSettings, "Player.Gravity", GameConstants.Player.DEFAULT_GRAVITY),
                    JumpForce = GetSettingValue<float>(allSettings, "Player.JumpForce", GameConstants.Player.DEFAULT_JUMP_FORCE),
                    DefaultSize = new SizeData
                    {
                        Width = GetSettingValue<int>(allSettings, "Player.DefaultSize.Width", GameConstants.Player.DEFAULT_WIDTH),
                        Height = GetSettingValue<int>(allSettings, "Player.DefaultSize.Height", GameConstants.Player.DEFAULT_HEIGHT)
                    },
                    GroundCheckHeight = GetSettingValue<int>(allSettings, "Player.GroundCheckHeight", GameConstants.Player.GROUND_CHECK_HEIGHT)
                },
                Window = new WindowSettingsData
                {
                    MinimumSize = new SizeData
                    {
                        Width = GetSettingValue<int>(allSettings, "Window.MinimumSize.Width", GameConstants.Window.MINIMUM_WIDTH),
                        Height = GetSettingValue<int>(allSettings, "Window.MinimumSize.Height", GameConstants.Window.MINIMUM_HEIGHT)
                    }
                },
                Gameplay = new GameplaySettingsData
                {
                    TargetFPS = GetSettingValue<int>(allSettings, "Gameplay.TargetFPS", GameConstants.Gameplay.DEFAULT_TARGET_FPS),
                    WindowSnapDistance = GetSettingValue<float>(allSettings, "Gameplay.WindowSnapDistance", GameConstants.Physics.DEFAULT_WINDOW_SNAP_DISTANCE)
                },
                UI = new UISettingsData
                {
                    DebugMode = GetSettingValue<bool>(allSettings, "UI.DebugMode", false),
                    ShowPerformanceInfo = GetSettingValue<bool>(allSettings, "UI.ShowPerformanceInfo", false),
                    FontSize = GetSettingValue<int>(allSettings, "UI.FontSize", GameConstants.UI.BUTTON_FONT_SIZE)
                },
                Audio = new AudioSettingsData
                {
                    MasterVolume = GetSettingValue<float>(allSettings, "Audio.MasterVolume", 1.0f),
                    SoundVolume = GetSettingValue<float>(allSettings, "Audio.SoundVolume", 1.0f),
                    MusicVolume = GetSettingValue<float>(allSettings, "Audio.MusicVolume", 0.7f),
                    Enabled = GetSettingValue<bool>(allSettings, "Audio.Enabled", true)
                }
            };
        }

        private void LoadBuiltInPresets()
        {
            // デフォルトプリセット
            _presets["Default"] = new GameSettingsData
            {
                Player = new PlayerSettingsData
                {
                    MovementSpeed = GameConstants.Player.DEFAULT_MOVEMENT_SPEED,
                    Gravity = GameConstants.Player.DEFAULT_GRAVITY,
                    JumpForce = GameConstants.Player.DEFAULT_JUMP_FORCE,
                    DefaultSize = new SizeData { Width = GameConstants.Player.DEFAULT_WIDTH, Height = GameConstants.Player.DEFAULT_HEIGHT },
                    GroundCheckHeight = GameConstants.Player.GROUND_CHECK_HEIGHT
                },
                Window = new WindowSettingsData
                {
                    MinimumSize = new SizeData { Width = GameConstants.Window.MINIMUM_WIDTH, Height = GameConstants.Window.MINIMUM_HEIGHT }
                },
                Gameplay = new GameplaySettingsData
                {
                    TargetFPS = GameConstants.Gameplay.DEFAULT_TARGET_FPS,
                    WindowSnapDistance = GameConstants.Physics.DEFAULT_WINDOW_SNAP_DISTANCE
                },
                UI = new UISettingsData
                {
                    DebugMode = false,
                    ShowPerformanceInfo = false,
                    FontSize = GameConstants.UI.BUTTON_FONT_SIZE
                },
                Audio = new AudioSettingsData
                {
                    MasterVolume = 1.0f,
                    SoundVolume = 1.0f,
                    MusicVolume = 0.7f,
                    Enabled = true
                }
            };

            // パフォーマンス優先プリセット
            _presets["Performance"] = new GameSettingsData
            {
                Player = new PlayerSettingsData
                {
                    MovementSpeed = 300.0f,
                    Gravity = 800.0f,
                    JumpForce = 500.0f,
                    DefaultSize = new SizeData { Width = 30, Height = 30 },
                    GroundCheckHeight = 10
                },
                Window = new WindowSettingsData
                {
                    MinimumSize = new SizeData { Width = 80, Height = 80 }
                },
                Gameplay = new GameplaySettingsData
                {
                    TargetFPS = 120,
                    WindowSnapDistance = 15.0f
                },
                UI = new UISettingsData
                {
                    DebugMode = false,
                    ShowPerformanceInfo = true,
                    FontSize = 10
                },
                Audio = new AudioSettingsData
                {
                    MasterVolume = 0.8f,
                    SoundVolume = 0.6f,
                    MusicVolume = 0.4f,
                    Enabled = true
                }
            };

            // 易しい難易度プリセット
            _presets["Easy"] = new GameSettingsData
            {
                Player = new PlayerSettingsData
                {
                    MovementSpeed = 500.0f,
                    Gravity = 600.0f,
                    JumpForce = 800.0f,
                    DefaultSize = new SizeData { Width = 50, Height = 50 },
                    GroundCheckHeight = 20
                },
                Window = new WindowSettingsData
                {
                    MinimumSize = new SizeData { Width = 120, Height = 120 }
                },
                Gameplay = new GameplaySettingsData
                {
                    TargetFPS = 60,
                    WindowSnapDistance = 30.0f
                },
                UI = new UISettingsData
                {
                    DebugMode = true,
                    ShowPerformanceInfo = false,
                    FontSize = 14
                },
                Audio = new AudioSettingsData
                {
                    MasterVolume = 1.0f,
                    SoundVolume = 1.0f,
                    MusicVolume = 0.8f,
                    Enabled = true
                }
            };
        }

        private void ApplyPreset(GameSettingsData preset)
        {
            // プレイヤー設定
            _settingsService.SetSetting("Player.MovementSpeed", preset.Player.MovementSpeed);
            _settingsService.SetSetting("Player.Gravity", preset.Player.Gravity);
            _settingsService.SetSetting("Player.JumpForce", preset.Player.JumpForce);
            _settingsService.SetSetting("Player.DefaultSize.Width", preset.Player.DefaultSize.Width);
            _settingsService.SetSetting("Player.DefaultSize.Height", preset.Player.DefaultSize.Height);
            _settingsService.SetSetting("Player.GroundCheckHeight", preset.Player.GroundCheckHeight);

            // ウィンドウ設定
            _settingsService.SetSetting("Window.MinimumSize.Width", preset.Window.MinimumSize.Width);
            _settingsService.SetSetting("Window.MinimumSize.Height", preset.Window.MinimumSize.Height);

            // ゲームプレイ設定
            _settingsService.SetSetting("Gameplay.TargetFPS", preset.Gameplay.TargetFPS);
            _settingsService.SetSetting("Gameplay.WindowSnapDistance", preset.Gameplay.WindowSnapDistance);

            // UI設定
            _settingsService.SetSetting("UI.DebugMode", preset.UI.DebugMode);
            _settingsService.SetSetting("UI.ShowPerformanceInfo", preset.UI.ShowPerformanceInfo);
            _settingsService.SetSetting("UI.FontSize", preset.UI.FontSize);

            // 音声設定
            _settingsService.SetSetting("Audio.MasterVolume", preset.Audio.MasterVolume);
            _settingsService.SetSetting("Audio.SoundVolume", preset.Audio.SoundVolume);
            _settingsService.SetSetting("Audio.MusicVolume", preset.Audio.MusicVolume);
            _settingsService.SetSetting("Audio.Enabled", preset.Audio.Enabled);
        }

        private T GetSettingValue<T>(IReadOnlyDictionary<string, object> settings, string key, T defaultValue)
        {
            if (settings.TryGetValue(key, out var value))
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T)) ?? defaultValue;
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }
    }
}