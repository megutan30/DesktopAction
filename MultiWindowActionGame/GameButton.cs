// ボタンの基底クラス
public abstract class GameButton : Form
{
    protected Rectangle bounds;
    protected bool isHovered;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOPMOST = 0x8;

    public Rectangle Bounds => bounds;

    protected GameButton(Point location, Size size)
    {
        bounds = new Rectangle(location, size);
        InitializeButton();
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
            isHovered ? Color.FromArgb(200, 200, 200) : Color.FromArgb(230, 230, 230)))
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
}
