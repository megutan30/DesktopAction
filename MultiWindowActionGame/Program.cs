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

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOPMOST = 0x8;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;

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

            //mainForm.Activated += MainForm_Activated;

            SetWindowProperties(mainForm);

            MainGame game = new MainGame();
            game.Initialize();

            Task gameLoopTask = game.RunGameLoopAsync();

            Application.Run(mainForm);

            await gameLoopTask; 
        }
     
        private static void SetWindowProperties(Form form)
        {
            int exStyle = GetWindowLong(form.Handle, GWL_EXSTYLE);
            exStyle |= WS_EX_LAYERED;
            exStyle |= WS_EX_TRANSPARENT;
            exStyle |= WS_EX_TOPMOST;
            SetWindowLong(form.Handle, GWL_EXSTYLE, exStyle);
            SetWindowPos(form.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        }

        public static void EnsureTopMost()
        {
            if (mainForm != null && !mainForm.IsDisposed)
            {
                if (mainForm.InvokeRequired)
                {
                    mainForm.Invoke(new Action(EnsureTopMost));
                }
                else
                {
                    if (mainForm.Handle != IntPtr.Zero)
                    {
                        SetWindowPos(mainForm.Handle, HWND_TOPMOST, 0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE);
                    }
                }
            }
        }
    }
}