using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiWindowActionGame
{
    public class RetryButton : GameButton
    {
        public RetryButton(Point location) : base(location, new Size(150, 40))
        {
        }

        protected override void OnButtonClick()
        {
            StageManager.Instance.RestartCurrentStage();
        }

        protected override void DrawButtonContent(Graphics g)
        {
            using (Font font = new Font(CustomFonts.PressStart.FontFamily, 12, FontStyle.Bold))
            {
                string text = "Retry";
                SizeF textSize = g.MeasureString(text, font);
                g.DrawString(text, font, Brushes.Black,
                    (Width - textSize.Width) / 2,
                    (Height - textSize.Height) / 2);
            }
        }
    }
}
