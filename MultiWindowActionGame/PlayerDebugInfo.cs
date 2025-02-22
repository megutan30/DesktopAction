using MultiWindowActionGame;

public class PlayerDebugInfo
{
    private readonly PlayerForm player;
    private const int DEBUG_PANEL_X = 10;
    private const int DEBUG_PANEL_Y = 10;
    private static readonly Font DebugFont = new Font("Consolas", 10);

    public PlayerDebugInfo(PlayerForm player)
    {
        this.player = player;
    }

    public void Draw(Graphics g)
    {
        if (!MainGame.IsDebugMode) return;

        // プレイヤーに関する情報をデスクトップの左上に表示
        var stateInfo = new[]
        {
            "=== Player Debug ===",
            $"State: {player.GetCurrentState().GetType().Name}",
            $"Position: ({player.Location.X}, {player.Location.Y})",
            $"Size: {player.Size.Width}x{player.Size.Height}",
            $"Grounded: {player.IsGrounded}",
            $"Velocity: {player.VerticalVelocity:F2}",
            $"Parent Window: {(player.Parent != null ? player.Parent.Id.ToString() : "None")}",
            $"Last Valid Parent: {(player.LastValidParent != null ? player.LastValidParent.Id.ToString() : "None")}"
        };

        DrawInfoPanel(g, stateInfo, new Point(DEBUG_PANEL_X, DEBUG_PANEL_Y));
        DrawMovableRegion(g);
        DrawPlayerConnections(g);
    }

    private void DrawInfoPanel(Graphics g, string[] lines, Point location)
    {
        // 半透明の背景パネル
        var padding = 5;
        var lineHeight = DebugFont.Height + 2;
        var blockHeight = lines.Length * lineHeight + padding * 2;
        var blockWidth = lines.Max(l => TextRenderer.MeasureText(l, DebugFont).Width) + padding * 2;

        using (var brush = new SolidBrush(Color.FromArgb(180, Color.Black)))
        {
            g.FillRectangle(brush, location.X, location.Y, blockWidth, blockHeight);
        }

        // テキストの描画
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

    private void DrawMovableRegion(Graphics g)
    {
        using (var pen = new Pen(Color.Yellow, 1))
        {
            pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            // 移動可能領域
            var region = player.GetMovableRegion();
            using (var path = region.GetRegionPath())
            {
                g.DrawPath(pen, path);
            }

            // 接地判定領域
            var groundArea = player.GetGroundCheckArea();
            using (var groundPen = new Pen(Color.Red, 1))
            {
                g.DrawRectangle(groundPen, groundArea);
            }
        }
    }

    private void DrawPlayerConnections(Graphics g)
    {
        if (player.Parent == null) return;

        using (var pen = new Pen(Color.Yellow, 1))
        {
            pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

            var playerCenter = new Point(
                player.Bounds.X + player.Bounds.Width / 2,
                player.Bounds.Y + player.Bounds.Height / 2
            );

            var parentCenter = new Point(
                player.Parent.Bounds.X + player.Parent.Bounds.Width / 2,
                player.Parent.Bounds.Y + player.Parent.Bounds.Height / 2
            );

            g.DrawLine(pen, playerCenter, parentCenter);
        }
    }
}