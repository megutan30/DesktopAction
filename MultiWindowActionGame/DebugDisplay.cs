using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiWindowActionGame
{
    public class DebugDisplay
    {
        public static void DrawSettingsInfo(Graphics g, Point position)
        {
            if (!MainGame.IsDebugMode) return;

            var settings = GameSettings.Instance;
            var player = settings.Player;
            var window = settings.Window;
            var gameplay = settings.Gameplay;

            var debugInfo = new List<string>
            {
                "=== Settings ===",
                $"Player Speed: {player.MovementSpeed}",
                $"Gravity: {player.Gravity}",
                $"Jump Force: {player.JumpForce}",
                $"Window Min Size: {window.MinimumSize}",
                $"Target FPS: {gameplay.TargetFPS}"
            };

            using (var font = new Font("Arial", 10))
            using (var brush = new SolidBrush(Color.Yellow))
            {
                float y = position.Y;
                foreach (var line in debugInfo)
                {
                    g.DrawString(line, font, brush, position.X, y);
                    y += 15;
                }
            }
        }
    }
}
