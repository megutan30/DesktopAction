// ボタンの基底クラス
using System.Runtime.InteropServices;

public abstract class GameButton : Form
{
    protected Rectangle bounds;
    protected bool isHovered;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOPMOST = 0x8;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    public Rectangle Bounds => bounds;

    protected GameButton(Point location, Size size)
    {
        bounds = new Rectangle(location, size);
        InitializeButton();
        WindowManager.Instance.RegisterFormOrder(this, WindowManager.ZOrderPriority.Button);
    }
    private void GameButton_Load(object? sender, EventArgs e)
    {
        SetWindowProperties();
    }
    private void SetWindowProperties()
    {
        int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
        exStyle |= WS_EX_TRANSPARENT;
        exStyle |= WS_EX_TOPMOST;
        SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle);
        SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
    }
    private void InitializeButton()
    {
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.Manual;
        this.Location = bounds.Location;
        this.Size = bounds.Size;
        this.ShowInTaskbar = false;

        // マウスイベントの設定
        this.MouseEnter += (s, e) => { isHovered = true; this.Invalidate(); };
        this.MouseLeave += (s, e) => { isHovered = false; this.Invalidate(); };
        this.MouseClick += OnMouseClick;

        this.Paint += Button_Paint;
    }

    protected abstract void OnButtonClick();
    protected abstract void DrawButtonContent(Graphics g);

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            OnButtonClick();
        }
    }

    private void Button_Paint(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // ボタンの背景
        using (SolidBrush brush = new SolidBrush(
            isHovered ? Color.FromArgb(230, 230, 230): Color.FromArgb(200, 200, 200)))
        {
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        // ボタンの枠
        using (Pen pen = new Pen(Color.FromArgb(100, 100, 100), 2))
        {
            e.Graphics.DrawRectangle(pen, 1, 1, Width - 2, Height - 2);
        }

        DrawButtonContent(e.Graphics);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // フォームが破棄される前にWindowManagerから登録解除
            if (!IsDisposed)
            {
                WindowManager.Instance.UnregisterFormOrder(this);
            }
        }
        base.Dispose(disposing);
    }
}
