using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiWindowActionGame
{
    // StartButtonの実装
    public class StartButton : GameButton
    {
        public StartButton(Point location) : base(location, new Size(150, 40))
        {
        }

        protected override void OnButtonClick()
        {
            StageManager.Instance.StartNextStage();  // 次のステージへ
        }

        protected override void DrawButtonContent(Graphics g)
        {
            using (Font font = new Font(CustomFonts.PressStart.FontFamily, 14, FontStyle.Bold))  // フォントサイズを少し大きく
            {
                string text = "Start";
                SizeF textSize = g.MeasureString(text, font);
                g.DrawString(text, font, Brushes.Black,
                    (Width - textSize.Width) / 2,
                    (Height - textSize.Height) / 2);
            }
        }
    }
}
