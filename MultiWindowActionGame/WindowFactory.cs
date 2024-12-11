using System;
using System.Drawing;

namespace MultiWindowActionGame
{
    public class WindowFactory
    {
        public static GameWindow CreateWindow(WindowType type, Point location, Size size)
        {
            IWindowStrategy strategy = type switch
            {
                WindowType.Normal => new NormalWindowStrategy(),
                WindowType.Resizable => new ResizableWindowStrategy(),
                WindowType.Movable => new MovableWindowStrategy(),
                WindowType.Deletable => new DeletableWindowStrategy(),
                WindowType.Minimizable => new MinimizableWindowStrategy(),
                _ => throw new ArgumentException("Invalid window type", nameof(type))
            };

            var window = new GameWindow(location, size, strategy);

            WindowManager.Instance.RegisterWindow(window);

            if (type == WindowType.Movable)
            {
                window.FormBorderStyle = FormBorderStyle.FixedSingle;
            }
            else if (type == WindowType.Resizable)
            {
                window.FormBorderStyle = FormBorderStyle.FixedSingle;
            }
            else
            {
                window.FormBorderStyle = FormBorderStyle.FixedSingle;
            }

            window.MinimizeBox = (type == WindowType.Deletable);
            window.MinimizeBox = (type == WindowType.Minimizable);

            window.BackColor = type switch
            {
                WindowType.Normal => Color.Black,
                WindowType.Resizable => Color.LightGreen,
                WindowType.Movable => Color.LightBlue,
                WindowType.Deletable => Color.LightPink,
                WindowType.Minimizable => Color.AliceBlue,
                _ => Color.White
            };

            return window;
        }
    }

    public enum WindowType
    {
        Normal,
        Resizable,
        Movable,
        Deletable,
        Minimizable
    }
}