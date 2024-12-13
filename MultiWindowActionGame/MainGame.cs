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
        public static MainGame Instance => instance ?? throw new InvalidOperationException("MainGame is not initialized");
        public static bool IsDebugMode { get; private set; } = false;
        public void Initialize()
        {
            instance = this;
            WindowManager.Instance.Initialize();

            windowManager = WindowManager.Instance;

            InitializeGraphicsBuffer();
            if (Program.mainForm != null)
            {
                Program.mainForm.Resize += MainForm_Resize;
            }

            GameTime.Start();

            // ステージ1から開始
            StageManager.Instance.StartStage(0);
        }
        public void InitializePlayer(Point startPosition)
        {
            if (player == null)
            {
                // プレイヤーが存在しない場合のみ新規生成
                player = new Player(startPosition);
                windowManager.SetPlayer(player);
            }
            else
            {
                // 既に存在する場合は位置のリセットのみ
                player.ResetPosition(startPosition);
            }
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
            if (player != null)
            {
                await player.UpdateAsync(GameTime.DeltaTime);
            }
            await windowManager.UpdateAsync(GameTime.DeltaTime);
            StageManager.Instance.CurrentGoal?.EnsureZOrder();
            if (player != null && StageManager.Instance.CheckGoal(player))
            {
                // ゴールした時の処理
                Console.WriteLine("Goal!");
            }
        }

        private void Render()
        {
            if (graphicsBuffer == null) return;

            Graphics g = graphicsBuffer.Graphics;
            g.Clear(Color.Transparent);

            windowManager.Draw(g);
            player?.Draw(g);

            StageManager.Instance.CurrentGoal?.Draw(g);
            NoEntryZoneManager.Instance.Draw(g);
            // デバッグ情報の描画
            if (IsDebugMode) // デバッグモードフラグを追加
            {
                windowManager.DrawDebugInfo(g, player?.Bounds ??Rectangle.Empty);
                DrawDebugInfo(g);
            }

            graphicsBuffer.Render();
            Program.EnsureTopMost();
            StageManager.Instance.EnsureButtonsTopMost();
        }
        private void DrawDebugInfo(Graphics g)
        {
            // プレイヤーの位置情報を描画
            if (player == null)return;
            g.DrawString($"Player Position: {player.Bounds.Location}", SystemFonts.DefaultFont, Brushes.White, new PointF(10, 10));
        }

    }
}