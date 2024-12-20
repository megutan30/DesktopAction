using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MultiWindowActionGame
{
    public class GameSettings
    {
        // シングルトンインスタンス（一時的な実装、後で依存性注入に移行）
        private static readonly Lazy<GameSettings> lazy =
            new Lazy<GameSettings>(() => new GameSettings());
        public static GameSettings Instance => lazy.Value;

        private const string DEFAULT_SETTINGS_PATH = "config/settings.json";
        private static string settingsPath = DEFAULT_SETTINGS_PATH;

        // プレイヤー設定
        public class PlayerSettings
        {
            public float MovementSpeed { get; set; }
            public float Gravity { get; set; }
            public float JumpForce { get; set; }
            public Size DefaultSize { get; set; }
            public int GroundCheckHeight { get; set; }
        }

        // ウィンドウ設定
        public class WindowSettings
        {
            public Size MinimumSize { get; set; }
            public int ResizeThreshold { get; set; }
            public int MovementThreshold { get; set; }
            public float AnimationSpeed { get; set; }
        }

        // ゲームプレイ設定
        public class GameplaySettings
        {
            public int TargetFPS { get; set; }
            public float NoEntryZoneBuffer { get; set; }
            public float WindowSnapDistance { get; set; }
        }

        public PlayerSettings Player { get; private set; }
        public WindowSettings Window { get; private set; }
        public GameplaySettings Gameplay { get; private set; }

        private GameSettings()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    var settings = JsonSerializer.Deserialize<GameSettingsData>(json);
                    if (settings != null)
                    {
                        Player = settings.Player;
                        Window = settings.Window;
                        Gameplay = settings.Gameplay;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }

            // 設定ファイルが存在しないか読み込みに失敗した場合はデフォルト値を使用
            LoadDefaultSettings();
        }
        private void LoadDefaultSettings()
        {
            Player = new PlayerSettings
            {
                MovementSpeed = 400.0f,
                Gravity = 1000.0f,
                JumpForce = 600.0f,
                DefaultSize = new Size(40, 40),
                GroundCheckHeight = 15
            };

            Window = new WindowSettings
            {
                MinimumSize = new Size(100, 100),
                ResizeThreshold = 5,
                MovementThreshold = 100,
                AnimationSpeed = 2.0f
            };

            Gameplay = new GameplaySettings
            {
                TargetFPS = 60,
                NoEntryZoneBuffer = 5.0f,
                WindowSnapDistance = 20.0f
            };
        }
        public void SaveSettings()
        {
            try
            {
                var settingsData = new GameSettingsData
                {
                    Player = Player,
                    Window = Window,
                    Gameplay = Gameplay
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(settingsData, options);
                var directory = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(settingsPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }
        public static void SetSettingsPath(string path)
        {
            settingsPath = path;
        }

        // シリアル化のためのデータクラス
        private class GameSettingsData
        {
            public PlayerSettings Player { get; set; }
            public WindowSettings Window { get; set; }
            public GameplaySettings Gameplay { get; set; }
        }
    }
}
