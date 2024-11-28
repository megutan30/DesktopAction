using System.Collections.Generic;
using System.Drawing;
using System.Numerics;

namespace MultiWindowActionGame
{
    public interface IEffectTarget : IDrawable, IUpdatable
    {
        Rectangle Bounds { get; }
        GameWindow? Parent { get; }
        ICollection<IEffectTarget> Children { get; }
        void ApplyEffect(IWindowEffect effect);
        bool CanReceiveEffect(IWindowEffect effect);
        void AddChild(IEffectTarget child);
        void RemoveChild(IEffectTarget child);
    }

    public interface IWindowEffect
    {
        EffectType Type { get; }
        bool IsActive { get; }
        void Apply(IEffectTarget target);
        //void PropagateToChildren(IEffectTarget target);
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
        private Point sourceWindowLocation;
        private Dictionary<IEffectTarget, Point> initialRelativePositions = new Dictionary<IEffectTarget, Point>();
        public Vector2 CurrentMovement => movement;
        public EffectType Type => EffectType.Movement;
        public bool IsActive { get; private set; }

        public void UpdateMovement(Vector2 newMovement)
        {
            movement = newMovement;
            IsActive = movement != Vector2.Zero;
        }

        public void Apply(IEffectTarget target)
        {
            if (!IsActive || !target.CanReceiveEffect(this)) return;

            // 対象自身に効果を適用
            ApplyMovementToTarget(target);
            foreach (var child in target.Children)
            {
                ApplyMovementToTarget(child);
            }
        }

        private void ApplyMovementToTarget(IEffectTarget target)
        {
            if (target is GameWindow window)
            {
                window.Location = new Point(
                    window.Location.X + (int)movement.X,
                    window.Location.Y + (int)movement.Y
                );
            }
            else if (target is Player player)
            {
                player.UpdatePosition(new Point(
                    player.Bounds.X + (int)movement.X,
                    player.Bounds.Y + (int)movement.Y
                ));
            }
        }
    }

    public class ResizeEffect : IWindowEffect
    {
        private Dictionary<IEffectTarget, SizeF> targetScales = new Dictionary<IEffectTarget, SizeF>();
        private Dictionary<IEffectTarget, Size> referenceSize = new Dictionary<IEffectTarget, Size>();

        public EffectType Type => EffectType.Resize;
        public bool IsActive { get; private set; }

        public void UpdateScale(IEffectTarget target, SizeF newScale)
        {
            if (!targetScales.ContainsKey(target))
            {
                // 初期参照サイズを保存
                referenceSize[target] = target is GameWindow window ? window.Size :
                                      target is Player player ? player.Bounds.Size :
                                      Size.Empty;
            }
            targetScales[target] = newScale;
            IsActive = true;
        }

        public void Apply(IEffectTarget target)
        {
            if (!IsActive || !target.CanReceiveEffect(this)) return;

            // 対象自身に効果を適用
            ApplyResizeToTarget(target);
            foreach (var child in target.Children)
            {
                ApplyResizeToTarget(child);
            }
        }

        private void ApplyResizeToTarget(IEffectTarget target)
        {
            if (!referenceSize.ContainsKey(target)) return;

            var baseSize = referenceSize[target];
            var scale = targetScales.GetValueOrDefault(target, new SizeF(1.0f, 1.0f));

            if (target is GameWindow window)
            {
                Size newSize = new Size(
                    (int)(baseSize.Width * scale.Width),
                    (int)(baseSize.Height * scale.Height)
                );
                window.Size = newSize;
            }
            else if (target is Player player)
            {
                Size newSize = new Size(
                    (int)(baseSize.Width * scale.Width),
                    (int)(baseSize.Height * scale.Height)
                );
                player.UpdateSize(newSize);
            }
        }
        public SizeF GetCurrentScale(IEffectTarget target)
        {
            return targetScales.GetValueOrDefault(target, new SizeF(1.0f, 1.0f));
        }
        public void ResetAll()
        {
            targetScales.Clear();
            referenceSize.Clear();
            IsActive = false;
        }
        public void Reset(IEffectTarget target)
        {
            if (target == null) return; // null チェックを追加

            targetScales.Remove(target);
            referenceSize.Remove(target);
            IsActive = targetScales.Count > 0;
        }
    }
}