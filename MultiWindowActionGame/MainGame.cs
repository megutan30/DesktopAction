using System;
using System.Drawing;
using System.Windows.Forms;

namespace MultiWindowActionGame
{
    public class MainGame
    {
        private Player player = new Player();
        private WindowManager windowManager = WindowManager.Instance;
        private BufferedGraphics? graphicsBuffer;

        public void Initialize()
        {
            player = new Player();
            windowManager = WindowManager.Instance;

            InitializeGraphicsBuffer();
            if (Program.mainForm != null)
            {
                Program.mainForm.Resize += MainForm_Resize;
            }

            GameTime.Start();
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
        }

        private void Render()
        {
            if (graphicsBuffer == null) return;

            Graphics g = graphicsBuffer.Graphics;
            g.Clear(Color.Transparent);

            windowManager.Draw(g);
            player.Draw(g);

            graphicsBuffer.Render();
        }
    }
}