using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiWindowActionGame
{
    public abstract class BaseEffectTarget : Form,IEffectTarget
    {
        protected Rectangle bounds;
        public virtual Rectangle Bounds => bounds;
        private GameWindow? parent;
        public GameWindow? Parent
        {
            get => parent;
            protected set => parent = value;
        }
        public ICollection<IEffectTarget> Children { get; } = new HashSet<IEffectTarget>();
        public bool IsMinimized { get; protected set; }

        public virtual void SetParent(GameWindow? newParent)
        {
            if (Parent != null)
            {
                Parent.RemoveChild(this);
            }
            Parent = newParent;
            Parent?.AddChild(this);
        }

        public virtual void AddChild(IEffectTarget child)
        {
            Children.Add(child); 
            if (child is GameWindow window)
            {
                window.SetParent(Parent);
            }
        }

        public virtual void RemoveChild(IEffectTarget child)
        {
            Children.Remove(child);
        }

        public virtual bool CanReceiveEffect(IWindowEffect effect)
        {
            return Parent != null;
        }

        public virtual void ApplyEffect(IWindowEffect effect)
        {
            if (!CanReceiveEffect(effect)) return;

            if (effect is MovementEffect moveEffect)
            {
                var newPos = new Point(
                    bounds.X + (int)moveEffect.CurrentMovement.X,
                    bounds.Y + (int)moveEffect.CurrentMovement.Y
                );
                UpdateTargetPosition(newPos);
            }
            else if (effect is ResizeEffect resizeEffect)
            {
                var scale = resizeEffect.GetCurrentScale(this);
                var newSize = new Size(
                    (int)(bounds.Width * scale.Width),
                    (int)(bounds.Height * scale.Height)
                );
                UpdateTargetSize(newSize);
            }
        }

        public abstract void UpdateTargetPosition(Point newPosition);
        public abstract void UpdateTargetSize(Size newSize);
        public abstract void OnMinimize();
        public abstract void OnRestore();
        public abstract void Draw(Graphics g);
        public abstract Task UpdateAsync(float deltaTime);
        public abstract Size GetOriginalSize();
    }
}
