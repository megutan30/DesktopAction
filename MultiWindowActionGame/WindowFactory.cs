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
                window.FormBorderStyle = FormBorderStyle.None;
                window.BackColor = Color.LightBlue; // または他の色を使用して移動可能なウィンドウを識別しやすくする
            }

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