using MultiWindowActionGame;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

public class Goal : BaseEffectTarget
{
    private bool isInFront;
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
        int exStyle = WindowMessages.GetWindowLong(this.Handle, WindowMessages.GWL_EXSTYLE);
        exStyle |= WindowMessages.WS_EX_LAYERED;
        exStyle |= WindowMessages.WS_EX_TRANSPARENT;
        WindowMessages.SetWindowLong(this.Handle, WindowMessages.GWL_EXSTYLE, exStyle);
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
    public override void SetParent(GameWindow? newParent)
    {
        base.SetParent(newParent);

        // 親が変更されたら再描画を要求
        this.Invalidate();
    }
    private void Goal_Paint(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        // 背景を透明に
        using (var brush = new SolidBrush(BackColor))
        {
            e.Graphics.FillRectangle(brush, Bounds);
        }

        // フォームの縦横比に基づいてフォントサイズを計算
        float baseFontSize = Math.Min(Bounds.Width, Bounds.Height) * 1f;
        using (var font = new Font("Arial", baseFontSize, FontStyle.Bold))
        {
            var text = "G";
            var size = e.Graphics.MeasureString(text, font);

            // フォームの縦横比に合わせてスケーリング
            float scaleX = Bounds.Width / size.Width;
            float scaleY = Bounds.Height / size.Height;

            // 変換行列を設定
            e.Graphics.TranslateTransform(Bounds.Width / 2, Bounds.Height / 2);
            e.Graphics.ScaleTransform(scaleX, scaleY);
            e.Graphics.TranslateTransform(-size.Width / 2, -size.Height / 2);

            // 親ウィンドウに基づいてアウトライン色を設定
            Color outlineColor;
            if (Parent != null)
            {
                // 親の背景色の明るさを計算
                float brightness = (Parent.BackColor.R * 0.299f +
                                  Parent.BackColor.G * 0.587f +
                                  Parent.BackColor.B * 0.114f) / 255f;

                if (brightness < 0.5f)
                {
                    // 暗い背景の場合は明るい色のアウトライン
                    outlineColor = Color.FromArgb(
                        Math.Min(255, Parent.BackColor.R + 100),
                        Math.Min(255, Parent.BackColor.G + 100),
                        Math.Min(255, Parent.BackColor.B + 100)
                    );
                }
                else
                {
                    // 明るい背景の場合は暗い色のアウトライン
                    outlineColor = Color.FromArgb(
                        Math.Max(0, Parent.BackColor.R - 50),
                        Math.Max(0, Parent.BackColor.G - 50),
                        Math.Max(0, Parent.BackColor.B - 50)
                    );
                }
            }
            else
            {
                outlineColor = Color.Black;
            }

            // アウトラインの太さを調整（スケールを考慮）
            float offset = baseFontSize * 0.04f / Math.Max(scaleX, scaleY);

            // アウトラインを描画（8方向）
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x != 0 || y != 0)
                    {
                        e.Graphics.DrawString(text, font, new SolidBrush(outlineColor),
                            x * offset,
                            y * offset);
                    }
                }
            }

            // メインの文字を描画
            e.Graphics.DrawString(text, font, Brushes.Gold, 0, 0);

            // 変換をリセット
            e.Graphics.ResetTransform();
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
    public override Size GetOriginalSize() => Bounds.Size;
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