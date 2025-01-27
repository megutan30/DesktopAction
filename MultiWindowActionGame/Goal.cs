using MultiWindowActionGame;
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

        // "G"の描画
        using (Font font = new Font("Arial", Height * 0.8f, FontStyle.Bold))
        {
            string text = "G";

            // 文字列のサイズを測定
            SizeF textSize = e.Graphics.MeasureString(text, font);

            // 中央に配置するための位置を計算
            float x = (Bounds.Width - textSize.Width) / 2;
            float y = (Bounds.Height - textSize.Height) / 2;

            // 文字を描画
            e.Graphics.DrawString(text, font, Brushes.Gold, x, y);
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