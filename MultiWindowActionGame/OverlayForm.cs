using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiWindowActionGame
{
    public class OverlayForm : Form
    {
        private readonly WindowManager windowManager;
        private PlayerDebugInfo? playerDebugInfo;
        private WindowDebugInfo? windowDebugInfo;
        private PerformanceDebugInfo performanceDebugInfo;

        public OverlayForm(WindowManager windowManager)
        {
            this.windowManager = windowManager;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;

            if (Program.mainForm != null)
            {
                this.Size = Program.mainForm.Size;
                this.Location = Program.mainForm.Location;
            }

            windowDebugInfo = new WindowDebugInfo(windowManager);
            this.Paint += OverlayForm_Paint;
            performanceDebugInfo = new PerformanceDebugInfo();
        }

        private void OverlayForm_Paint(object? sender, PaintEventArgs e)
        {

            int titleBarHeight = 30;
            e.Graphics.TranslateTransform(0, -titleBarHeight);
            windowManager.DrawMarks(e.Graphics);
            if (!MainGame.IsDebugMode) return;
            try
            {
                var player = MainGame.GetPlayer();
                if (player != null)
                {
                    playerDebugInfo ??= new PlayerDebugInfo(player);
                    playerDebugInfo.Draw(e.Graphics);
                }

                windowDebugInfo?.Draw(e.Graphics);
            }
            finally
            {
                e.Graphics.TranslateTransform(0, titleBarHeight);
            }
            performanceDebugInfo.Draw(e.Graphics);
        }

        public void UpdateOverlay()
        {
            this.Invalidate();
        }
    }
}
