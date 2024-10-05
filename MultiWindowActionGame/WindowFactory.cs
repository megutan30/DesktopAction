﻿using System;
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

            return new GameWindow(location, size, strategy);
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