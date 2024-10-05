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
                _ => throw new ArgumentException("Invalid window type", nameof(type))
            };

            var window = new GameWindow(location, size, strategy);

            if (type == WindowType.Movable)
            {
                window.FormBorderStyle = FormBorderStyle.FixedSingle;
            }
            else if (type == WindowType.Resizable)
            {
                window.FormBorderStyle = FormBorderStyle.Sizable;
            }
            else
            {
                window.FormBorderStyle = FormBorderStyle.FixedSingle;
            }

            window.BackColor = type switch
            {
                WindowType.Normal => Color.White,
                WindowType.Resizable => Color.LightGreen,
                WindowType.Movable => Color.LightBlue,
                WindowType.Deletable => Color.LightPink,
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
        Deletable
    }
}