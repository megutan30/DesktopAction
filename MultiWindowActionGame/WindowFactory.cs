﻿using System;
using System.Drawing;
using static System.Net.Mime.MediaTypeNames;

namespace MultiWindowActionGame
{
    public class WindowFactory
    {
        public static GameWindow CreateWindow(WindowType type, Point location, Size size, string? text = null)
        {
            IWindowStrategy strategy = type switch
            {
                WindowType.Normal => new NormalWindowStrategy(),
                WindowType.Resizable => new ResizableWindowStrategy(),
                WindowType.Movable => new MovableWindowStrategy(),
                WindowType.Deletable => new DeletableWindowStrategy(),
                WindowType.Minimizable => new MinimizableWindowStrategy(),
                WindowType.TextDisplay => new TextDisplayWindowStrategy(text ?? "NULL"),
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
                WindowType.TextDisplay => Color.Black,
                _ => Color.White
            };

            if (type == WindowType.TextDisplay)
            {
                window.Paint += (sender, e) =>
                {
                    if (strategy is TextDisplayWindowStrategy textStrategy)
                    {
                        using (Font font = new Font(CustomFonts.PressStart.FontFamily, 12))
                        {
                            string text = textStrategy.GetDisplayText();
                            SizeF textSize = e.Graphics.MeasureString(text, font);
                            float x = (window.ClientSize.Width - textSize.Width) / 2;
                            float y = (window.ClientSize.Height - textSize.Height) / 2;
                            e.Graphics.DrawString(text, font, Brushes.White, x, y);
                        }
                    }
                };
            }

            return window;
        }
    }

    public enum WindowType
    {
        Normal,
        Resizable,
        Movable,
        Deletable,
        Minimizable,
        TextDisplay
    }
}