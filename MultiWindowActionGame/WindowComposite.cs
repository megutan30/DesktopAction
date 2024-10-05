using System.Collections.Generic;
using System.Drawing;

namespace MultiWindowActionGame
{
    public interface IWindowComponent : IDrawable, IUpdatable
    {
        void AddChild(IWindowComponent child);
        void RemoveChild(IWindowComponent child);
        IWindowComponent? GetChild(int index);
        int ChildCount { get; }
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