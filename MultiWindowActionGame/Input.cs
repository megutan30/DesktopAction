using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace MultiWindowActionGame
{
    public static class Input
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(Keys vKey);

        public static bool IsKeyDown(Keys key)
        {
            return (GetAsyncKeyState(key) & 0x8000) != 0;
        }
    }
}