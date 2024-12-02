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
        void UpdateTargetSize(Size newSize);
        void UpdateTargetPosition(Point newPositon);
        void Restore();
        bool IsMinimized { get; }
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
        Delete,
        Minimize
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

            // 直接の子のみに効果を適用（孫は親から効果を受け取る）
            foreach (var directChild in GetDirectChildren(target))
            {
                Apply(directChild);
            }
        }
        private IEnumerable<IEffectTarget> GetDirectChildren(IEffectTarget parent)
        {
            // 子の中で、自身が親であるもののみを返す（孫は除外）
            return parent.Children.Where(child =>
            {
                if (child is GameWindow childWindow)
                {
                    return childWindow.Parent == parent;
                }
                return true; // Player等の非ウィンドウ要素は常に直接の子として扱う
            });
        }
        private void ApplyMovementToTarget(IEffectTarget target)
        {
            if (!target.CanReceiveEffect(this)) return;

            var newPosition = target is GameWindow window
                ? new Point(
                    window.Location.X + (int)movement.X,
                    window.Location.Y + (int)movement.Y)
                : new Point(
                    target.Bounds.X + (int)movement.X,
                    target.Bounds.Y + (int)movement.Y);

            target.UpdateTargetPosition(newPosition);
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

            ApplyResizeToTarget(target);

            foreach (var directChild in GetDirectChildren(target))
            {
                Apply(directChild);
            }
        }
        private IEnumerable<IEffectTarget> GetDirectChildren(IEffectTarget parent)
        {
            return parent.Children.Where(child =>
            {
                if (child is GameWindow childWindow)
                {
                    return childWindow.Parent == parent;
                }
                return true;
            });
        }
        private void ApplyResizeToTarget(IEffectTarget target)
        {
            if (!referenceSize.ContainsKey(target)) return;

            var baseSize = referenceSize[target];
            var scale = targetScales.GetValueOrDefault(target, new SizeF(1.0f, 1.0f));

            Size newSize = new Size(
                (int)(baseSize.Width * scale.Width),
                (int)(baseSize.Height * scale.Height)
            );

            target.UpdateTargetSize(newSize);
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
    public class MinimizeEffect : IWindowEffect
    {
        public EffectType Type => EffectType.Minimize;
        public bool IsActive { get; private set; }

        public void Apply(IEffectTarget target)
        {
            if (!target.CanReceiveEffect(this)) return;

            if (target is GameWindow window)
            {
                window.WindowState = FormWindowState.Minimized;
            }
            else if (target is Player player)
            {
                player.Hide();
            }
        }
    }
}