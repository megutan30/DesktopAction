using MultiWindowActionGame;
using System.Runtime.InteropServices;

public class NoEntryZone : Form, IEffectTarget
{
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
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private Rectangle bounds;
    private readonly System.Windows.Forms.Timer animationTimer;
    private int animationOffset = 0;
    private const int STRIPE_WIDTH = 20;  // 縞模様の幅

    public Rectangle Bounds => bounds;
    public GameWindow? Parent { get; private set; }
    public ICollection<IEffectTarget> Children { get; } = new HashSet<IEffectTarget>();
    public bool IsMinimized { get; private set; }

    public NoEntryZone(Point location, Size size)
    {
        bounds = new Rectangle(location, size);
        InitializeZone();

        // アニメーション用タイマー設定
        animationTimer = new System.Windows.Forms.Timer();
        animationTimer.Interval = 50;  // 20fps
        animationTimer.Tick += AnimationTimer_Tick;
        animationTimer.Start();
    }

    private void InitializeZone()
    {
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.Manual;
        this.Location = bounds.Location;
        this.Size = bounds.Size;
        this.BackColor = Color.Magenta;
        this.TransparencyKey = Color.Magenta;
        this.ShowInTaskbar = false;
        this.Load += NoEntryZone_Load;
        this.Paint += NoEntryZone_Paint;
    }

    private void NoEntryZone_Load(object? sender, EventArgs e)
    {
        SetWindowProperties();
    }

    private void SetWindowProperties()
    {
        int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
        exStyle |= WS_EX_LAYERED;
        exStyle |= WS_EX_TRANSPARENT;
        exStyle |= WS_EX_TOPMOST;
        SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle);
        SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        animationOffset = (animationOffset + 2) % STRIPE_WIDTH;
        this.Invalidate();  // 再描画を要求
    }

    private void NoEntryZone_Paint(object? sender, PaintEventArgs e)
    {
        using (Brush redBrush = new SolidBrush(Color.FromArgb(180, Color.Red)))
        using (Brush blackBrush = new SolidBrush(Color.FromArgb(180, Color.Black)))
        {
            int y = -STRIPE_WIDTH + animationOffset;
            while (y < this.Height)
            {
                // 赤と黒の縞模様を描画
                e.Graphics.FillRectangle(redBrush, 0, y, this.Width, STRIPE_WIDTH);
                e.Graphics.FillRectangle(blackBrush, 0, y + STRIPE_WIDTH, this.Width, STRIPE_WIDTH);
                y += STRIPE_WIDTH * 2;
            }
        }
    }

    // IEffectTarget の実装
    public void UpdateTargetPosition(Point newPosition)
    {
        bounds.Location = newPosition;
        this.Location = newPosition;
    }

    public void UpdateTargetSize(Size newSize)
    {
        bounds.Size = newSize;
        this.Size = newSize;
    }

    public void AddChild(IEffectTarget child) => Children.Add(child);
    public void RemoveChild(IEffectTarget child) => Children.Remove(child);
    public bool CanReceiveEffect(IWindowEffect effect) => false;
    public void ApplyEffect(IWindowEffect effect) { }
    public void OnMinimize() { }
    public void OnRestore() { }

    public async Task UpdateAsync(float deltaTime)
    {
        // 必要に応じて更新ロジックを実装
    }

    public void Draw(Graphics g)
    {
        if (MainGame.IsDebugMode)
        {
            // デバッグ情報の表示
            g.DrawRectangle(new Pen(Color.Yellow, 2), bounds);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            animationTimer.Stop();
            animationTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}