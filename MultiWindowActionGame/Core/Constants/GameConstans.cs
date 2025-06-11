// Core/Constants/GameConstants.cs
namespace MultiWindowActionGame.Core.Constants
{
    /// <summary>
    /// ゲーム全体で使用される定数を定義
    /// </summary>
    public static class GameConstants
    {
        // ===== プレイヤー関連 =====
        public static class Player
        {
            public const int DEFAULT_WIDTH = 40;
            public const int DEFAULT_HEIGHT = 40;
            public const int MINIMUM_SIZE = 5;
            public const float DEFAULT_MOVEMENT_SPEED = 400.0f;
            public const float DEFAULT_GRAVITY = 1000.0f;
            public const float DEFAULT_JUMP_FORCE = 600.0f;
            public const int GROUND_CHECK_HEIGHT = 15;
            public const int GROUND_CHECK_TOLERANCE = 10;
            public const float MAX_VERTICAL_STEP = 20.0f;
            public const int COLLISION_CHECK_SEGMENTS = 4;
        }

        // ===== ウィンドウ関連 =====
        public static class Window
        {
            public const int MINIMUM_WIDTH = 100;
            public const int MINIMUM_HEIGHT = 100;
            public const int MARGIN = 0;
            public const float OUTLINE_WIDTH = 5.0f;
            public const int BUTTON_MINIMUM_WIDTH = 150;
            public const int BUTTON_MINIMUM_HEIGHT = 40;
            public const int TITLE_BAR_HEIGHT = 30;
        }

        // ===== 描画関連 =====
        public static class Rendering
        {
            public const int STRATEGY_MARK_SIZE = 60;
            public const int ARROW_HEAD_SIZE = 10;
            public const int STRIPE_WIDTH = 20;
            public const int DEBUG_PANEL_PADDING = 5;
            public const int DEBUG_LINE_HEIGHT = 15;
            public const int DEBUG_FONT_SIZE = 10;
            public const float TEXT_OUTLINE_OFFSET = 1.0f;
        }

        // ===== UI関連 =====
        public static class UI
        {
            public const int BUTTON_FONT_SIZE = 12;
            public const int TITLE_FONT_SIZE = 14;
            public const int DEBUG_PANEL_X = 10;
            public const int DEBUG_PANEL_Y = 10;
            public const int PERFORMANCE_PANEL_Y = 400;
            public const int WINDOW_DEBUG_PANEL_MARGIN = 400;
        }

        // ===== アニメーション関連 =====
        public static class Animation
        {
            public const int ANIMATION_TIMER_INTERVAL = 50; // 20fps
            public const int STRIPE_WIDTH = 20; // 縞模様の幅
            public const int TOTAL_PATTERN_HEIGHT = STRIPE_WIDTH * 2;
            public const float NOTIFICATION_DISPLAY_DURATION = 3.0f;
            public const int ANIMATION_OFFSET_STEP = 2;
        }

        // ===== 物理演算関連 =====
        public static class Physics
        {
            public const float DEFAULT_WINDOW_SNAP_DISTANCE = 20.0f;
            public const float EXPONENTIAL_MOVING_AVERAGE = 0.95f;
            public const float FRAME_TIME_CONTRIBUTION = 0.05f;
            public const float GROUND_CHECK_TOLERANCE_Y = 5.0f;
            public const int MOVEMENT_STEP_SIZE = 1;
        }

        // ===== ゲームプレイ関連 =====
        public static class Gameplay
        {
            public const int DEFAULT_TARGET_FPS = 60;
            public const int PAUSE_DELAY_MS = 100;
            public const int DEBUG_TOGGLE_DELAY_MS = 200;
            public const int PERFORMANCE_HISTORY_SIZE = 60;
            public const int MAX_STAGE_NUMBER = 15;
        }

        // ===== キー入力関連 =====
        public static class Input
        {
            public static readonly Keys[] MOVEMENT_LEFT = { Keys.A, Keys.Left };
            public static readonly Keys[] MOVEMENT_RIGHT = { Keys.D, Keys.Right };
            public static readonly Keys[] JUMP_KEYS = { Keys.Space, Keys.Up, Keys.W };
            public static readonly Keys DEBUG_TOGGLE = Keys.F3;
            public static readonly Keys DELETE_KEY = Keys.Delete;
        }

        // ===== ファイルパス関連 =====
        public static class Paths
        {
            public const string DEFAULT_SETTINGS_PATH = "config/settings.json";
            public const string FONT_DIRECTORY = "Resources/Fonts";
            public const string FONT_FILE_NAME = "prstart.ttf";
            public const string CONFIG_DIRECTORY = "config";
        }

        // ===== Win32 API関連 =====
        public static class Win32
        {
            public const int HWND_TOP = 0;
            public const int HWND_TOPMOST = -1;
            public const int HWND_NOTOPMOST = -2;
            public const int HWND_BOTTOM = 1;

            public const uint SWP_NOMOVE = 0x0002;
            public const uint SWP_NOSIZE = 0x0001;
            public const uint SWP_NOACTIVATE = 0x0010;
            public const uint SWP_SHOWWINDOW = 0x0040;

            public const int GWL_EXSTYLE = -20;
            public const int WS_EX_LAYERED = 0x80000;
            public const int WS_EX_TRANSPARENT = 0x20;
            public const int WS_EX_TOPMOST = 0x8;

            // ウィンドウメッセージ
            public const int WM_ACTIVATE = 0x0006;
            public const int WM_MOUSEMOVE = 0x0200;
            public const int WM_LBUTTONDOWN = 0x0201;
            public const int WM_LBUTTONUP = 0x0202;
            public const int WM_MOUSEACTIVATE = 0x0021;
            public const int WM_NCHITTEST = 0x0084;
            public const int WM_SYSCOMMAND = 0x0112;
            public const int WM_NCLBUTTONDOWN = 0x00A1;
            public const int WM_NCMOUSEMOVE = 0x00A0;
            public const int WM_SETCURSOR = 0x0020;

            public const int MA_NOACTIVATE = 3;
            public const int HTCAPTION = 2;
            public const int HTCLIENT = 1;

            public const int SC_CLOSE = 0xF060;
            public const int SC_MINIMIZE = 0xF020;
            public const int SC_MAXIMIZE = 0xF030;
            public const int SC_RESTORE = 0xF120;
            public const int SC_MOVE = 0xF010;

            public const uint MF_BYCOMMAND = 0x00000000;
            public const uint MF_GRAYED = 0x00000001;

            public const int DWMWA_CAPTION_COLOR = 35;
        }

        // ===== 色定義 =====
        public static class Colors
        {
            // デバッグ用色
            public static readonly Color DEBUG_BACKGROUND = Color.FromArgb(180, Color.Black);
            public static readonly Color DEBUG_PERFORMANCE = Color.FromArgb(180, Color.DarkRed);
            public static readonly Color DEBUG_WINDOW = Color.FromArgb(180, Color.DarkBlue);
            public static readonly Color DEBUG_ACTIVE_EFFECTS = Color.FromArgb(180, Color.DarkGreen);

            // ゲームエンティティ色
            public static readonly Color PLAYER_BLUE = Color.Blue;
            public static readonly Color GOAL_GOLD = Color.Gold;

            // 不可侵領域色
            public static readonly Color NO_ENTRY_RED = Color.FromArgb(180, Color.Red);
            public static readonly Color NO_ENTRY_BLACK = Color.FromArgb(180, Color.Black);

            // ボタン色
            public static readonly Color BUTTON_NORMAL = Color.FromArgb(200, 200, 200);
            public static readonly Color BUTTON_HOVERED = Color.FromArgb(230, 230, 230);
            public static readonly Color BUTTON_BORDER = Color.FromArgb(100, 100, 100);

            // マーク色
            public static readonly Color MARK_NORMAL = Color.FromArgb(128, 128, 128);
            public static readonly Color MARK_HOVERED = Color.White;
            public static readonly Color MARK_YELLOW = Color.Yellow;

            // ウィンドウ背景色
            public static readonly Color WINDOW_BLACK = Color.Black;
            public static readonly Color WINDOW_WHITE = Color.White;
            public static readonly Color WINDOW_LIGHT_GREEN = Color.LightGreen;
            public static readonly Color WINDOW_LIGHT_BLUE = Color.LightBlue;
            public static readonly Color WINDOW_LIGHT_PINK = Color.LightPink;

            // システム色
            public static readonly Color TRANSPARENT_MAGENTA = Color.Magenta;
        }

        // ===== ゲームサイズ定義 =====
        public static class Sizes
        {
            // ゴール
            public static readonly Size DEFAULT_GOAL_SIZE = new(64, 64);

            // プレイヤー
            public static readonly Size DEFAULT_PLAYER_SIZE = new(Player.DEFAULT_WIDTH, Player.DEFAULT_HEIGHT);

            // ボタン
            public static readonly Size DEFAULT_BUTTON_SIZE = new(Window.BUTTON_MINIMUM_WIDTH, Window.BUTTON_MINIMUM_HEIGHT);

            // ウィンドウ最小サイズ
            public static readonly Size MINIMUM_WINDOW_SIZE = new(Window.MINIMUM_WIDTH, Window.MINIMUM_HEIGHT);

            // プレイヤー最小サイズ
            public static readonly Size MINIMUM_PLAYER_SIZE = new(Player.MINIMUM_SIZE, Player.MINIMUM_SIZE);
        }

        // ===== Z-Order優先度 =====
        public static class ZOrder
        {
            public const int BOTTOM = 1;
            public const int WINDOW = 2;
            public const int WINDOW_MARK = 3;
            public const int BUTTON = 4;
            public const int GOAL = 5;
            public const int PLAYER = 6;
            public const int DEBUG_LAYER = 7;
        }

        // ===== エラーとメッセージ =====
        public static class Messages
        {
            // エラーメッセージ
            public const string MAIN_GAME_NOT_INITIALIZED = "MainGame is not initialized";
            public const string INVALID_WINDOW_TYPE = "Invalid window type";
            public const string FAILED_TO_LOAD_SETTINGS = "Failed to load settings";
            public const string FAILED_TO_SAVE_SETTINGS = "Failed to save settings";
            public const string FAILED_TO_RELOAD_SETTINGS = "Failed to reload settings";
            public const string STAGE_NUMBER_OUT_OF_RANGE = "Stage number is out of range";

            // 成功メッセージ
            public const string SETTINGS_APPLIED = "Settings applied successfully!";
            public const string SETTINGS_SAVED = "Settings saved successfully!";
            public const string GOAL_REACHED = "Goal!";

            // UI テキスト
            public const string BUTTON_START = "Start";
            public const string BUTTON_RETRY = "Retry";
            public const string BUTTON_TITLE = "Title";
            public const string BUTTON_EXIT = "Exit";
            public const string BUTTON_SETTINGS = "Settings";

            // ゲーム情報
            public const string GAME_TITLE = "Window Action Game";
            public const string THANK_YOU_MESSAGE = "Thank you!!";
            public const string NULL_TEXT = "NULL";
        }

        // ===== 設定デフォルト値 =====
        public static class Defaults
        {
            // プレイヤー設定のデフォルト値
            public static class PlayerDefaults
            {
                public const float MovementSpeed = Player.DEFAULT_MOVEMENT_SPEED;
                public const float Gravity = Player.DEFAULT_GRAVITY;
                public const float JumpForce = Player.DEFAULT_JUMP_FORCE;
                public static readonly Size DefaultSize = Sizes.DEFAULT_PLAYER_SIZE;
                public const int GroundCheckHeight = Player.GROUND_CHECK_HEIGHT;
            }

            // ウィンドウ設定のデフォルト値
            public static class WindowDefaults
            {
                public static readonly Size MinimumSize = Sizes.MINIMUM_WINDOW_SIZE;
            }

            // ゲームプレイ設定のデフォルト値
            public static class GameplayDefaults
            {
                public const int TargetFPS = Gameplay.DEFAULT_TARGET_FPS;
                public const float WindowSnapDistance = Physics.DEFAULT_WINDOW_SNAP_DISTANCE;
            }
        }
    }
}