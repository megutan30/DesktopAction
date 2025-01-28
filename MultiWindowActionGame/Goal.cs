using MultiWindowActionGame;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

public class Goal : BaseEffectTarget
{
    private bool isInFront;
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    private GameWindow? lastValidParent;
    public Goal(Point location, bool isInFront)
    {
        this.isInFront = isInFront;
        InitializeGoal(location);
        WindowManager.Instance.RegisterFormOrder(this,
            isInFront ? WindowManager.ZOrderPriority.Goal : WindowManager.ZOrderPriority.Bottom);
    }
    private void Goal_Load(object? sender, EventArgs e)
    {
        SetWindowProperties();
        WindowManager.Instance.UpdateFormZOrder(this,
            isInFront ? WindowManager.ZOrderPriority.Goal : WindowManager.ZOrderPriority.Bottom);
    }
    private void SetWindowProperties()
    {
        int exStyle = GetWindowLong(this.Handle, WindowMessages.GWL_EXSTYLE);
        exStyle |= WindowMessages.WS_EX_LAYERED;
        exStyle |= WindowMessages.WS_EX_TRANSPARENT;
        SetWindowLong(this.Handle, WindowMessages.GWL_EXSTYLE, exStyle);
    }
    private void InitializeGoal(Point location)
    {
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.Manual;
        this.Location = location;
        this.Size = new Size(64, 64);
        this.TopMost = true;
        bounds = new Rectangle(location, this.Size);
        this.BackColor = Color.Magenta;
        this.TransparencyKey = Color.Magenta;
        this.Paint += Goal_Paint;
        this.Load += Goal_Load;

        var parentWindow = WindowManager.Instance.GetTopWindowAt(bounds, null);
        if (parentWindow != null)
        {
            SetParent(parentWindow);
        }
    }

    private void Goal_Paint(object? sender, PaintEventArgs e)
    {
        // 背景を塗りつぶす
        using (SolidBrush brush = new SolidBrush(BackColor))
        {
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        string text = "G";
        // フォームの縦横比に合わせてアスペクト比を調整
        using (var matrix = new Matrix())
        {
            // フォームのサイズに基づいて初期フォントサイズを計算（幅を基準に）
            float fontSize = Bounds.Width * 0.8f;
            using (Font font = new Font("Arial", fontSize, FontStyle.Bold))
            {
                // テキストのサイズを取得
                SizeF textSize = e.Graphics.MeasureString(text, font);

                // 拡大縮小率を計算
                float scaleX = Bounds.Width / textSize.Width;
                float scaleY = Bounds.Height / textSize.Height;

                // グラフィックスの変換を設定
                e.Graphics.TranslateTransform(Bounds.Width / 2, Bounds.Height / 2);
                e.Graphics.ScaleTransform(scaleX, scaleY);
                e.Graphics.TranslateTransform(-textSize.Width / 2, -textSize.Height / 2);

                // 描画
                e.Graphics.DrawString(text, font, Brushes.Gold, 0, 0);
            }
        }
    }
    private void UpdateParentIfNeeded()
    {
        // 自身の位置とサイズに変更があった場合のみチェック
        if (lastCheckedBounds != bounds)
        {
            var potentialParent = WindowManager.Instance.GetWindowFullyContaining(bounds);
            if (potentialParent != Parent)
            {
                SetParent(potentialParent);
            }
            lastCheckedBounds = bounds;
        }
    }
    private Rectangle lastCheckedBounds;
    public override void UpdateTargetPosition(Point newPosition)
    {
        this.Location = newPosition;
        bounds.Location = newPosition;
        UpdateParentIfNeeded();
    }

    public override void UpdateTargetSize(Size newSize)
    {
        // 最小サイズを設定
        var validSize = new Size(
            Math.Max(newSize.Width, 20),  // 最小幅20px
            Math.Max(newSize.Height, 20)  // 最小高さ20px
        );

        this.Size = validSize;
        bounds.Size = validSize;
        UpdateParentIfNeeded();
    }
    public override void OnMinimize()
    {
        IsMinimized = true;
        this.WindowState = FormWindowState.Minimized;

        if (Parent != null)
        {
            lastValidParent = Parent;
            Parent.RemoveChild(this);
            Parent = null;
        }
    }
    public override void OnRestore()
    {
        IsMinimized = false;
        this.WindowState = FormWindowState.Normal;
        this.BringToFront();

        if (lastValidParent != null &&
            !lastValidParent.IsMinimized &&
            lastValidParent.AdjustedBounds.IntersectsWith(bounds))
        {
            SetParent(lastValidParent);
        }
        else
        {
            var newParent = WindowManager.Instance.GetTopWindowAt(bounds, null);
            SetParent(newParent);
        }
    }
    public override async Task UpdateAsync(float deltaTime)
    {
        CheckParentWindow();
    }

    private void CheckParentWindow()
    {
        // 自身の領域を完全に含むウィンドウを探す
        var potentialParent = WindowManager.Instance.GetWindowFullyContaining(bounds);
        if (potentialParent != Parent)
        {
            SetParent(potentialParent);
        }
    }
    public override void Draw(Graphics g)
    {
        if (MainGame.IsDebugMode)
        {
            g.DrawRectangle(new Pen(Color.Red, 2), Bounds);
            g.DrawString($"Goal Bounds: {Bounds}",
                SystemFonts.DefaultFont,
                Brushes.Red,
                Bounds.X,
                Bounds.Y - 20);
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            WindowManager.Instance.UnregisterFormOrder(this);
        }
        base.Dispose(disposing);
    }
}