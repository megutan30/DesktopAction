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
        private DesktopIconDebugInfo desktopIconDebugInfo;

        public OverlayForm(WindowManager windowManager)
        {
            this.windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;

            this.SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint,
                true
            );

            if (Program.mainForm != null)
            {
                this.Size = Program.mainForm.Size;
                this.Location = Program.mainForm.Location;
            }

            // インスタンスを必ず初期化
            this.windowDebugInfo = new WindowDebugInfo(windowManager);
            this.performanceDebugInfo = new PerformanceDebugInfo();
            this.desktopIconDebugInfo = new DesktopIconDebugInfo();

            this.Paint += OverlayForm_Paint;
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
                windowDebugInfo.Draw(e.Graphics);
                performanceDebugInfo.Draw(e.Graphics);

                // デスクトップアイコン情報を描画 (追加)
                desktopIconDebugInfo.Draw(e.Graphics);
                desktopIconDebugInfo.DrawPlayerIconCollisions(e.Graphics);
            }
            finally
            {
                e.Graphics.TranslateTransform(0, titleBarHeight);
            }
        }


        public void UpdateOverlay()
        {
            this.Invalidate();
        }
    }
}
