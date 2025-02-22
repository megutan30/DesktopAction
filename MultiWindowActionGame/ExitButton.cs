using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiWindowActionGame
{
    public class ExitButton : GameButton
    {
        public ExitButton(Point location) : base(location, new Size(150, 40))
        {
        }

        protected override void OnButtonClick()
        {
            // リソースを適切に解放してから終了
            if (Program.mainForm != null)
            {
                // メインフォームに終了を通知
                Program.mainForm.BeginInvoke(new Action(() =>
                {
                    WindowManager.Instance.ClearWindows();
                    Program.mainForm.Close();
                    Application.Exit();
                    Environment.Exit(0);  // プロセスを完全に終了
                }));
            }
        }

        protected override void DrawButtonContent(Graphics g)
        {
            using (Font font = new Font(CustomFonts.PressStart.FontFamily, 14, FontStyle.Bold))
            {
                string text = "Exit";
                SizeF textSize = g.MeasureString(text, font);
                g.DrawString(text, font, Brushes.Black,
                    (Width - textSize.Width) / 2,
                    (Height - textSize.Height) / 2);
            }
        }
    }
}
