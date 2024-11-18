using MultiWindowActionGame;

public class DebugDisplay
{
    public static void DrawEffectInfo(Graphics g, GameWindow window)
    {
        if (!MainGame.IsDebugMode) return;

        var containedTargets = WindowManager.Instance.GetAllComponents()
            .OfType<IEffectTarget>()
            .Where(t => t.IsCompletelyContained(window))
            .ToList();

        string info = $"Window ID: {window.Id}\n" +
                     $"Type: {window.Strategy.GetType().Name}\n" +
                     $"Contained Targets: {containedTargets.Count}";

        using (var brush = new SolidBrush(Color.FromArgb(128, Color.Black)))
        {
            g.FillRectangle(brush, new Rectangle(window.AdjustedBounds.Location, new Size(200, 60)));
        }

        g.DrawString(info, SystemFonts.DefaultFont, Brushes.White,
            window.AdjustedBounds.Location.X + 5,
            window.AdjustedBounds.Location.Y + 5);

        // 含まれているターゲットの境界を表示
        foreach (var target in containedTargets)
        {
            using (var pen = new Pen(Color.Yellow, 2))
            {
                g.DrawRectangle(pen, target.Bounds);
            }
        }
    }
}