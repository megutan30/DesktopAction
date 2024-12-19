using System;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;

namespace MultiWindowActionGame
{
    public class MainGame
    {
        private static MainGame? instance;
        private PlayerForm? player;
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
            StageManager.Instance.StartStage(0);
        }

        public void InitializePlayer(Point startPosition)
        {
            if (player == null)
            {
                player = new PlayerForm(startPosition);
                windowManager.SetPlayer(player);
                player.Show();
            }
            else
            {
                player.ResetPosition(startPosition);
            }
        }
        public static PlayerForm? GetPlayer()
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

                int elapsedTime = Environment.TickCount - startTime;
                int sleepTime = targetFrameTime - elapsedTime;

                if (Input.IsKeyDown(Keys.F3))
                {
                    IsDebugMode = !IsDebugMode;
                    await Task.Delay(200);
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
                Debug.WriteLine("Goal!");
                StageManager.Instance.StartNextStage();
            }
        }
        private void Render()
        {
            if (graphicsBuffer == null) return;

            Graphics g = graphicsBuffer.Graphics;
            g.Clear(Color.Transparent);

            windowManager.Draw(g);
            StageManager.Instance.CurrentGoal?.Draw(g);
            NoEntryZoneManager.Instance.Draw(g);

            if (IsDebugMode)
            {
                windowManager.DrawDebugInfo(g, player?.Bounds ?? Rectangle.Empty);
                DrawDebugInfo(g);
            }

            graphicsBuffer.Render();
            EnsureWindowsTopMost();
        }
        private void DrawDebugInfo(Graphics g)
        {
            if (player == null) return;

            g.DrawString(
                $"Player Position: {player.Bounds.Location}",
                SystemFonts.DefaultFont,
                Brushes.White,
                new PointF(10, 10)
            );
        }

        private void EnsureWindowsTopMost()
        {
            player?.BringToFront();
            //StageManager.Instance.EnsureButtonsTopMost();
        }
    }
}