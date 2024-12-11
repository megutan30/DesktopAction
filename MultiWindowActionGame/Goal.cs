using MultiWindowActionGame;
using System.Runtime.InteropServices;

public class Goal : Form, IEffectTarget
{
    private Rectangle bounds;
    private bool isInFront;  // 前面表示かどうか
    public Rectangle Bounds => bounds;
    public GameWindow? Parent { get; private set; }
    public ICollection<IEffectTarget> Children { get; } = new HashSet<IEffectTarget>();
    public bool IsMinimized { get; private set; }
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOPMOST = 0x8;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public Goal(Point location, bool isInFront)
    {
        this.isInFront = isInFront;
        InitializeGoal(location);
        this.Load += Goal_Load;
    }
    private void Goal_Load(object? sender, EventArgs e)
    {
        SetWindowProperties();
    }
    public void EnsureZOrder()
    {
        if (isInFront)
        {
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        }
        else
        {
            SetWindowPos(this.Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        }
    }
    private void SetWindowProperties()
    {
        if (isInFront)
        {
            int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
            exStyle |= WS_EX_LAYERED;
            exStyle |= WS_EX_TRANSPARENT;
            exStyle |= WS_EX_TOPMOST;
            SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle);
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        }
        else
        {
            SetWindowPos(this.Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        }
    }
    private void InitializeGoal(Point location)
    {
        // フォームの設定
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.Manual;
        this.Location = location;
        this.Size = new Size(64, 64);  // ゴールのサイズ
        this.TopMost = true;

        bounds = new Rectangle(location, this.Size);

        // 背景を透明に
        this.BackColor = Color.Magenta;
        this.TransparencyKey = Color.Magenta;

        // ゴールアイコンの描画
        this.Paint += Goal_Paint;
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

    // IEffectTarget実装
    public void ApplyEffect(IWindowEffect effect)
    {
        if (!CanReceiveEffect(effect)) return;
        effect.Apply(this);
    }

    public bool CanReceiveEffect(IWindowEffect effect) => true;

    public void AddChild(IEffectTarget child)
    {
        Children.Add(child);
    }

    public void RemoveChild(IEffectTarget child)
    {
        Children.Remove(child);
    }

    public void UpdateTargetSize(Size newSize)
    {
        this.Size = newSize;
        bounds.Size = newSize;
    }

    public void UpdateTargetPosition(Point newPosition)
    {
        this.Location = newPosition;
        bounds.Location = newPosition;
    }

    public void OnMinimize()
    {
        IsMinimized = true;
        Hide();
    }

    public void OnRestore()
    {
        IsMinimized = false;
        Show();
    }

    public async Task UpdateAsync(float deltaTime)
    {
        // 必要に応じて更新処理を実装
    }

    // Goal.cs
    public void Draw(Graphics g)
    {
        if (MainGame.IsDebugMode)
        {
            // 判定領域を表示
            g.DrawRectangle(new Pen(Color.Red, 2), Bounds);

            // 座標情報を表示
            g.DrawString($"Goal Bounds: {Bounds}",
                SystemFonts.DefaultFont,
                Brushes.Red,
                Bounds.X,
                Bounds.Y - 20);
        }
    }
}
public static class Win32
{
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);
}