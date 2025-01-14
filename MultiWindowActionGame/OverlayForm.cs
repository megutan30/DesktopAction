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

        public OverlayForm(WindowManager windowManager)
        {
            this.windowManager = windowManager;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.BackColor = Color.Magenta;  // または別の色
            this.TransparencyKey = Color.Magenta;

            // Program.mainFormと同じサイズ・位置に設定
            if (Program.mainForm != null)
            {
                this.Size = Program.mainForm.Size;
                this.Location = Program.mainForm.Location;
            }

            this.Paint += OverlayForm_Paint;
        }

        private void OverlayForm_Paint(object? sender, PaintEventArgs e)
        {
            // タイトルバーの高さを取得
            int titleBarHeight = 30;
            e.Graphics.TranslateTransform(0, -titleBarHeight);
            try
            {
                if (MainGame.IsDebugMode)
                {
                    windowManager.DrawDebugInfo(e.Graphics, MainGame.GetPlayer()?.Bounds ?? Rectangle.Empty);
                }
                windowManager.DrawMarks(e.Graphics);
            }
            finally
            {
                // 変換をリセット
                e.Graphics.TranslateTransform(0, titleBarHeight);
            }
        }

        public void UpdateOverlay()
        {
            this.Invalidate();
        }
    }
}
