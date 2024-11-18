using System.Collections.Generic;
using System.Drawing;
using System.Numerics;

namespace MultiWindowActionGame
{
    public interface IWindowComponent : IDrawable, IUpdatable
    {
        void AddChild(IWindowComponent child);
        void RemoveChild(IWindowComponent child);
        IWindowComponent? GetChild(int index);
        int ChildCount { get; }
    }
    public interface IEffectTarget
    {
        Rectangle Bounds { get; }
        bool CanReceiveEffect(IWindowEffect effect);
        void ApplyEffect(IWindowEffect effect);
        bool IsCompletelyContained(GameWindow container);
    }

    public interface IWindowEffect
    {
        EffectType Type { get; }
        void Apply(IEffectTarget target);
        bool IsActive { get; }
    }

    public enum EffectType
    {
        Movement,
        Resize,
        Delete
    }

    public class MovementEffect : IWindowEffect
    {
        private Vector2 movement;
        public EffectType Type => EffectType.Movement;
        public bool IsActive { get; private set; }

        public void UpdateMovement(Vector2 newMovement)
        {
            movement = newMovement;
            IsActive = movement != Vector2.Zero;
        }

        public void Apply(IEffectTarget target)
        {
            if (!IsActive) return;

            // 移動量に基づいて対象を移動
            if (target is GameWindow window)
            {
                Point newLocation = new Point(
                    window.Location.X + (int)movement.X,
                    window.Location.Y + (int)movement.Y
                );
                window.Location = newLocation;
            }
            else if (target is Player player)
            {
                player.ApplyExternalMovement(movement);
                //player.UpdatePosition(new Point(
                //    player.Bounds.X + (int)movement.X,
                //    player.Bounds.Y + (int)movement.Y
                //));
            }
        }
    }

    public class ResizeEffect : IWindowEffect
    {
        private SizeF scale;
        public EffectType Type => EffectType.Resize;
        public bool IsActive { get; private set; }

        public void UpdateScale(SizeF newScale)
        {
            scale = newScale;
            IsActive = scale != new SizeF(1.0f, 1.0f);
        }

        public void Apply(IEffectTarget target)
        {
            if (!IsActive) return;

            if (target is GameWindow window)
            {
                Size newSize = new Size(
                    (int)(window.OriginalSize.Width * scale.Width),
                    (int)(window.OriginalSize.Height * scale.Height)
                );
                window.Size = newSize;
            }
            else if (target is Player player)
            {
                player.ApplyScale(scale);
            //    Size newSize = new Size(
            //        (int)(player.OriginalSize.Width * scale.Width),
            //        (int)(player.OriginalSize.Height * scale.Height)
            //    );

            //    player.UpdateSize(newSize);
            }
        }
    }
    public class WindowComposite : IWindowComponent
    {
        private List<IWindowComponent> children = new List<IWindowComponent>();

        public int ChildCount => children.Count;

        public void AddChild(IWindowComponent child)
        {
            children.Add(child);
        }

        public void RemoveChild(IWindowComponent child)
        {
            children.Remove(child);
        }

        public IWindowComponent? GetChild(int index)
        {
            return index >= 0 && index < children.Count ? children[index] : null;
        }

        public async Task UpdateAsync(float deltaTime)
        {
            foreach (var child in children.ToArray()) // ToArray() to avoid collection modified exception
            {
                await child.UpdateAsync(deltaTime);
            }
        }

        public void Draw(Graphics g)
        {
            foreach (var child in children.ToArray()) // ToArray() to avoid collection modified exception
            {
                child.Draw(g);
            }
        }
    }
}