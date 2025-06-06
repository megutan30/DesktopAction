using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MultiWindowActionGame
{
    public class DesktopIconDebugInfo
    {
        private static readonly Font DebugFont = new Font("Consolas", 9);
        private const int PANEL_X = 600;
        private const int PANEL_Y = 10;
        private const int UPDATE_INTERVAL_MS = 1000; // 1秒間隔で更新

        private DateTime lastUpdate = DateTime.MinValue;
        private List<DesktopIconInfo> cachedIcons = new List<DesktopIconInfo>();

        public void Draw(Graphics g)
        {
            if (!MainGame.IsDebugMode) return;

            // 定期的にアイコン情報を更新
            UpdateIconsIfNeeded();

            var iconInfo = new List<string>
            {
                "=== Desktop Icons ===",
                $"Count: {cachedIcons.Count}",
                ""
            };

            // 最初の10個のアイコンを表示
            var displayIcons = cachedIcons.Take(10);
            foreach (var icon in displayIcons)
            {
                iconInfo.Add($"{icon.Name}");
                iconInfo.Add($"  Pos: ({icon.Position.X}, {icon.Position.Y})");
                iconInfo.Add($"  Size: {icon.Size.Width}x{icon.Size.Height}");
                iconInfo.Add("");
            }

            if (cachedIcons.Count > 10)
            {
                iconInfo.Add($"... and {cachedIcons.Count - 10} more");
            }

            DrawInfoPanel(g, iconInfo.ToArray(), new Point(PANEL_X, PANEL_Y));
            DrawIconBounds(g);
        }

        private void UpdateIconsIfNeeded()
        {
            var now = DateTime.Now;
            if ((now - lastUpdate).TotalMilliseconds >= UPDATE_INTERVAL_MS)
            {
                try
                {
                    cachedIcons = DesktopIconManager.Instance.GetDesktopIcons();
                    lastUpdate = now;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"アイコン情報更新エラー: {ex.Message}");
                }
            }
        }

        private void DrawInfoPanel(Graphics g, string[] lines, Point location)
        {
            var padding = 5;
            var lineHeight = DebugFont.Height + 2;
            var blockHeight = lines.Length * lineHeight + padding * 2;
            var blockWidth = lines.Max(l => TextRenderer.MeasureText(l, DebugFont).Width) + padding * 2;

            using (var brush = new SolidBrush(Color.FromArgb(180, Color.DarkSlateBlue)))
            {
                g.FillRectangle(brush, location.X, location.Y, blockWidth, blockHeight);
            }

            using (var brush = new SolidBrush(Color.White))
            {
                var y = location.Y + padding;
                foreach (var line in lines)
                {
                    g.DrawString(line, DebugFont, brush, location.X + padding, y);
                    y += lineHeight;
                }
            }
        }

        private void DrawIconBounds(Graphics g)
        {
            // デスクトップアイコンの境界を描画
            using (var pen = new Pen(Color.Cyan, 1))
            {
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

                foreach (var icon in cachedIcons)
                {
                    g.DrawRectangle(pen, icon.Bounds);

                    // アイコン名を表示（小さなフォントで）
                    using (var font = new Font("Arial", 8))
                    using (var brush = new SolidBrush(Color.Yellow))
                    {
                        var textSize = g.MeasureString(icon.Name, font);
                        var textX = icon.Bounds.X + (icon.Bounds.Width - textSize.Width) / 2;
                        var textY = icon.Bounds.Bottom + 2;

                        // 背景を描画
                        using (var bgBrush = new SolidBrush(Color.FromArgb(128, Color.Black)))
                        {
                            g.FillRectangle(bgBrush, textX - 2, textY, textSize.Width + 4, textSize.Height);
                        }

                        g.DrawString(icon.Name, font, brush, textX, textY);
                    }
                }
            }
        }

        /// <summary>
        /// プレイヤーと重なっているアイコンの情報を取得
        /// </summary>
        public List<DesktopIconInfo> GetIconsIntersectingWithPlayer()
        {
            var player = MainGame.GetPlayer();
            if (player == null) return new List<DesktopIconInfo>();

            return cachedIcons.Where(icon => icon.Bounds.IntersectsWith(player.Bounds)).ToList();
        }

        /// <summary>
        /// アイコンとプレイヤーの衝突を検出して描画
        /// </summary>
        public void DrawPlayerIconCollisions(Graphics g)
        {
            if (!MainGame.IsDebugMode) return;

            var intersectingIcons = GetIconsIntersectingWithPlayer();

            using (var pen = new Pen(Color.Red, 3))
            {
                foreach (var icon in intersectingIcons)
                {
                    g.DrawRectangle(pen, icon.Bounds);

                    // 衝突情報をテキストで表示
                    using (var font = new Font("Arial", 10, FontStyle.Bold))
                    using (var brush = new SolidBrush(Color.Red))
                    {
                        var text = $"COLLISION: {icon.Name}";
                        var textY = icon.Bounds.Y - 20;
                        g.DrawString(text, font, brush, icon.Bounds.X, textY);
                    }
                }
            }
        }
    }
}