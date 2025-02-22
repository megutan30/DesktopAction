using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiWindowActionGame
{
    public class WindowEffectManager
    {
        private static readonly Lazy<WindowEffectManager> lazy =
            new Lazy<WindowEffectManager>(() => new WindowEffectManager());

        public static WindowEffectManager Instance => lazy.Value;

        private readonly List<IWindowEffect> activeEffects = new();

        public void AddEffect(IWindowEffect effect)
        {
            if (!activeEffects.Contains(effect))
            {
                activeEffects.Add(effect);
            }
        }

        public void RemoveEffect(IWindowEffect effect)
        {
            activeEffects.Remove(effect);
        }

        public void ApplyEffects(IEffectTarget target)
        {
            foreach (var effect in activeEffects.Where(e => e.IsActive))
            {
                effect.Apply(target);
            }
        }

        public void ClearEffects()
        {
            activeEffects.Clear();
        }
    }
}
