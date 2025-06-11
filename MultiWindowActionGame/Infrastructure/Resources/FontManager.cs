// Infrastructure/Resources/FontManager.cs
using System.Drawing.Text;
using MultiWindowActionGame.Core.Constants;

namespace MultiWindowActionGame.Infrastructure.Resources
{
    /// <summary>
    /// フォント管理クラス - シングルトンパターンからサービスパターンに変更
    /// </summary>
    public interface IFontManager : IDisposable
    {
        Font PressStartFont { get; }
        Font GetFont(string fontName, float size, FontStyle style = FontStyle.Regular);
        Font GetSystemFont(float size, FontStyle style = FontStyle.Regular);
        bool IsFontLoaded(string fontName);
        void LoadFont(string fontPath, string fontName);
    }

    public class FontManager : IFontManager
    {
        private readonly PrivateFontCollection _privateFonts;
        private readonly Dictionary<string, FontFamily> _loadedFonts;
        private readonly Dictionary<string, Font> _fontCache;
        private bool _disposed = false;

        public Font PressStartFont { get; private set; }

        public FontManager()
        {
            _privateFonts = new PrivateFontCollection();
            _loadedFonts = new Dictionary<string, FontFamily>();
            _fontCache = new Dictionary<string, Font>();

            InitializeDefaultFonts();
        }

        private void InitializeDefaultFonts()
        {
            try
            {
                // Press Start 2P フォントの読み込み
                var fontPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    GameConstants.Paths.FONT_DIRECTORY,
                    GameConstants.Paths.FONT_FILE_NAME);

                if (File.Exists(fontPath))
                {
                    LoadFont(fontPath, "PressStart2P");
                    PressStartFont = GetFont("PressStart2P", 12f);
                }
                else
                {
                    // フォールバック: システムフォントを使用
                    System.Diagnostics.Debug.WriteLine($"Font file not found: {fontPath}. Using fallback font.");
                    PressStartFont = GetSystemFont(12f, FontStyle.Bold);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load fonts: {ex.Message}");
                PressStartFont = GetSystemFont(12f, FontStyle.Bold);
            }
        }

        public Font GetFont(string fontName, float size, FontStyle style = FontStyle.Regular)
        {
            ThrowIfDisposed();

            var cacheKey = $"{fontName}_{size}_{style}";
            if (_fontCache.TryGetValue(cacheKey, out var cachedFont))
            {
                return cachedFont;
            }

            Font font;
            if (_loadedFonts.TryGetValue(fontName, out var fontFamily))
            {
                try
                {
                    font = new Font(fontFamily, size, style);
                }
                catch (ArgumentException)
                {
                    // フォントファミリーが指定されたスタイルをサポートしていない場合
                    font = new Font(fontFamily, size, FontStyle.Regular);
                }
            }
            else
            {
                // 指定されたフォントが見つからない場合はシステムフォントを使用
                font = GetSystemFont(size, style);
            }

            _fontCache[cacheKey] = font;
            return font;
        }

        public Font GetSystemFont(float size, FontStyle style = FontStyle.Regular)
        {
            ThrowIfDisposed();

            var cacheKey = $"System_{size}_{style}";
            if (_fontCache.TryGetValue(cacheKey, out var cachedFont))
            {
                return cachedFont;
            }

            var font = new Font(SystemFonts.DefaultFont.FontFamily, size, style);
            _fontCache[cacheKey] = font;
            return font;
        }

        public bool IsFontLoaded(string fontName)
        {
            ThrowIfDisposed();
            return _loadedFonts.ContainsKey(fontName);
        }

        public void LoadFont(string fontPath, string fontName)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(fontPath))
                throw new ArgumentException("Font path cannot be null or empty", nameof(fontPath));

            if (string.IsNullOrEmpty(fontName))
                throw new ArgumentException("Font name cannot be null or empty", nameof(fontName));

            if (!File.Exists(fontPath))
                throw new FileNotFoundException($"Font file not found: {fontPath}");

            try
            {
                _privateFonts.AddFontFile(fontPath);
                
                // 最後に追加されたフォントファミリーを取得
                var fontFamily = _privateFonts.Families.LastOrDefault();
                if (fontFamily != null)
                {
                    _loadedFonts[fontName] = fontFamily;
                    System.Diagnostics.Debug.WriteLine($"Font loaded successfully: {fontName} from {fontPath}");
                }
                else
                {
                    throw new InvalidOperationException($"Failed to load font family from {fontPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load font {fontName}: {ex.Message}");
                throw;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FontManager));
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // キャッシュされたフォントを破棄
                foreach (var font in _fontCache.Values)
                {
                    font?.Dispose();
                }
                _fontCache.Clear();

                // プライベートフォントコレクションを破棄
                _privateFonts?.Dispose();
                _loadedFonts.Clear();

                _disposed = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing FontManager: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// フォント関連のユーティリティメソッド
    /// </summary>
    public static class FontHelper
    {
        /// <summary>
        /// テキストのサイズを計算する
        /// </summary>
        public static SizeF MeasureText(string text, Font font, Graphics? graphics = null)
        {
            if (string.IsNullOrEmpty(text) || font == null)
                return SizeF.Empty;

            if (graphics != null)
            {
                return graphics.MeasureString(text, font);
            }

            // グラフィックスが提供されていない場合は一時的に作成
            using (var bitmap = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bitmap))
            {
                return g.MeasureString(text, font);
            }
        }

        /// <summary>
        /// 指定された領域にテキストを中央配置するための位置を計算する
        /// </summary>
        public static PointF GetCenteredTextPosition(string text, Font font, Rectangle bounds, Graphics? graphics = null)
        {
            var textSize = MeasureText(text, font, graphics);
            
            return new PointF(
                bounds.X + (bounds.Width - textSize.Width) / 2,
                bounds.Y + (bounds.Height - textSize.Height) / 2
            );
        }

        /// <summary>
        /// テキストが指定された幅に収まるフォントサイズを計算する
        /// </summary>
        public static float CalculateFontSizeToFit(string text, FontFamily fontFamily, int maxWidth, int maxHeight, Graphics? graphics = null)
        {
            if (string.IsNullOrEmpty(text) || fontFamily == null || maxWidth <= 0 || maxHeight <= 0)
                return 12f; // デフォルトサイズ

            bool useTemporaryGraphics = graphics == null;
            if (useTemporaryGraphics)
            {
                var bitmap = new Bitmap(1, 1);
                graphics = Graphics.FromImage(bitmap);
            }

            try
            {
                float fontSize = 72f; // 大きなサイズから開始
                const float minFontSize = 6f;
                const float step = 1f;

                while (fontSize >= minFontSize)
                {
                    using (var font = new Font(fontFamily, fontSize))
                    {
                        var size = graphics.MeasureString(text, font);
                        if (size.Width <= maxWidth && size.Height <= maxHeight)
                        {
                            return fontSize;
                        }
                    }
                    fontSize -= step;
                }

                return minFontSize;
            }
            finally
            {
                if (useTemporaryGraphics)
                {
                    graphics?.Dispose();
                }
            }
        }

        /// <summary>
        /// アウトライン付きテキストを描画する
        /// </summary>
        public static void DrawTextWithOutline(Graphics graphics, string text, Font font, Brush textBrush, Brush outlineBrush, PointF position, float outlineWidth = 1f)
        {
            ArgumentNullException.ThrowIfNull(graphics);
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(font);
            ArgumentNullException.ThrowIfNull(textBrush);
            ArgumentNullException.ThrowIfNull(outlineBrush);

            // アウトラインを描画 (8方向)
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x != 0 || y != 0)
                    {
                        graphics.DrawString(
                            text, 
                            font, 
                            outlineBrush,
                            position.X + x * outlineWidth,
                            position.Y + y * outlineWidth
                        );
                    }
                }
            }

            // メインテキストを描画
            graphics.DrawString(text, font, textBrush, position);
        }

        /// <summary>
        /// スケーラブルテキストを描画する（フォームのサイズに合わせて自動調整）
        /// </summary>
        public static void DrawScalableText(Graphics graphics, string text, FontFamily fontFamily, Rectangle bounds, Brush textBrush, Brush? outlineBrush = null)
        {
            ArgumentNullException.ThrowIfNull(graphics);
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(fontFamily);
            ArgumentNullException.ThrowIfNull(textBrush);

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            var fontSize = CalculateFontSizeToFit(text, fontFamily, bounds.Width, bounds.Height, graphics);
            
            using (var font = new Font(fontFamily, fontSize, FontStyle.Bold))
            {
                var textSize = graphics.MeasureString(text, font);
                
                // 変換行列を設定してスケーリング
                var state = graphics.Save();
                try
                {
                    graphics.TranslateTransform(bounds.Width / 2, bounds.Height / 2);
                    
                    float scaleX = bounds.Width / textSize.Width;
                    float scaleY = bounds.Height / textSize.Height;
                    graphics.ScaleTransform(scaleX, scaleY);
                    
                    graphics.TranslateTransform(-textSize.Width / 2, -textSize.Height / 2);

                    if (outlineBrush != null)
                    {
                        DrawTextWithOutline(graphics, text, font, textBrush, outlineBrush, PointF.Empty, GameConstants.Rendering.TEXT_OUTLINE_OFFSET);
                    }
                    else
                    {
                        graphics.DrawString(text, font, textBrush, PointF.Empty);
                    }
                }
                finally
                {
                    graphics.Restore(state);
                }
            }
        }
    }
}
            