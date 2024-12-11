using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MultiWindowActionGame
{
    public class MainGame
    {
        private static MainGame? instance;
        private Player? player;
        private WindowManager windowManager = WindowManager.Instance;
        private BufferedGraphics? graphicsBuffer;
        public static bool IsDebugMode { get; private set; } = true;
        public void Initialize()
        {
            instance = this;
            WindowManager.Instance.Initialize();

            // プレイヤーの生成を StageManager の初期化後に移動
            windowManager = WindowManager.Instance;

            InitializeGraphicsBuffer();
            if (Program.mainForm != null)
            {
                Program.mainForm.Resize += MainForm_Resize;
            }

            GameTime.Start();

            // プレイヤーの生成（初期ステージの開始位置を取得）
            var initialStage = StageManager.Instance.GetStage(0);
            player = new Player(initialStage.PlayerStartPosition);
            windowManager.SetPlayer(player);

            // ステージ1から開始
            StageManager.Instance.StartStage(0);
        }
        public static Player? GetPlayer()
        {
            return instance?.player;
        }

        private void InitializeGraphicsBuffer()
        {
            if (Program.mainForm != null && Program.mainForm.ClientSize.Width > 0 && Program.mainForm.ClientSize.Height > 0)
            {
                BufferedGraphicsContext context = BufferedGraphicsManager.Current;
                graphicsBuffer = context.Allocate(Program.mainForm.CreateGraphics(),
                Program.mainForm.ClientRectangle);
            }
        }

        private void MainForm_Resize(object? sender, EventArgs e)
        {
            InitializeGraphicsBuffer();
        }

        public async Task RunGameLoopAsync()
        {
            const int targetFps = 60;
            const int targetFrameTime = 1000 / targetFps;

            while (Program.mainForm != null && !Program.mainForm.IsDisposed)
            {
                int startTime = Environment.TickCount;

                GameTime.Update();
                await UpdateAsync();
                Render();
                Program.EnsureTopMost();

                int elapsedTime = Environment.TickCount - startTime;
                int sleepTime = targetFrameTime - elapsedTime;
                if (Input.IsKeyDown(Keys.F3)) // F3キーでデバッグモードを切り替え
                {
                    IsDebugMode = !IsDebugMode;
                    await Task.Delay(200); // キーの連続入力を防ぐための遅延
                }
                if (sleepTime > 0)
                {
                    await Task.Delay(sleepTime);
                }
            }
        }

        private async Task UpdateAsync()
        {
            await player.UpdateAsync(GameTime.DeltaTime);
            await windowManager.UpdateAsync(GameTime.DeltaTime);
            GameWindow? currentWindow = windowManager.GetWindowAt(player.Bounds);
            StageManager.Instance.CurrentGoal?.EnsureZOrder();
            // ゴールチェック
            if (StageManager.Instance.CheckGoal(player))
            {
                // ゴール時の演出などを追加
                System.Diagnostics.Debug.WriteLine("Goal!");
            }
        }

        private void Render()
        {
            if (graphicsBuffer == null) return;

            Graphics g = graphicsBuffer.Graphics;
            g.Clear(Color.Transparent);

            windowManager.Draw(g);
            player.Draw(g);
            StageManager.Instance.CurrentGoal?.Draw(g);
            // デバッグ情報の描画
            if (IsDebugMode) // デバッグモードフラグを追加
            {
                windowManager.DrawDebugInfo(g, player.Bounds);
                DrawDebugInfo(g);
                player.DrawDebugInfo(g);
            }

            graphicsBuffer.Render();
        }
        private void DrawDebugInfo(Graphics g)
        {
            // プレイヤーの位置情報を描画
            g.DrawString($"Player Position: {player.Bounds.Location}", SystemFonts.DefaultFont, Brushes.White, new PointF(10, 10));
        }

    }
}