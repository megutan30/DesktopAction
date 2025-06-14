using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace MultiWindowActionGame
{
    static class Program
    {
        public static Form? mainForm;

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [STAThread]
        static async Task Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            mainForm = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                WindowState = FormWindowState.Maximized,
                TopMost = true,
                ShowInTaskbar =false,
                BackColor = System.Drawing.Color.Black,
                TransparencyKey = System.Drawing.Color.Black,
                Text ="Game"
            };

            WindowManager.Instance.RegisterFormOrder(mainForm, WindowManager.ZOrderPriority.Bottom);

            MainGame game = new MainGame();
            game.Initialize();

            Task gameLoopTask = game.RunGameLoopAsync();

            Application.Run(mainForm);

            await gameLoopTask; 
        }
    }
}