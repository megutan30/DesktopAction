using MultiWindowActionGame;

public class NoEntryZoneManager
{
    private static readonly Lazy<NoEntryZoneManager> lazy =
        new Lazy<NoEntryZoneManager>(() => new NoEntryZoneManager());

    public static NoEntryZoneManager Instance { get { return lazy.Value; } }

    private List<NoEntryZone> zones = new List<NoEntryZone>();
    public IReadOnlyList<NoEntryZone> Zones => zones.AsReadOnly();

    private NoEntryZoneManager() { }

    public void AddZone(Point location, Size size)
    {
        var zone = new NoEntryZone(location, size);
        zones.Add(zone);
        zone.Show();
    }

    public void RemoveZone(NoEntryZone zone)
    {
        if (zones.Contains(zone))
        {
            zones.Remove(zone);
            zone.Close();
        }
    }

    public void ClearZones()
    {
        foreach (var zone in zones.ToList())
        {
            zone.Close();
        }
        zones.Clear();
    }

    // 指定された矩形が不可侵領域と重なるかチェック
    public bool IntersectsWithAnyZone(Rectangle bounds)
    {
        return zones.Any(zone => zone.Bounds.IntersectsWith(bounds));
    }

    public Rectangle GetValidPosition(Rectangle currentBounds, Rectangle proposedBounds)
    {
        Rectangle adjustedBounds = proposedBounds;

        foreach (var zone in zones)
        {
            // X軸方向の移動をチェック
            Rectangle xMovement = new Rectangle(
                proposedBounds.X,
                currentBounds.Y,
                proposedBounds.Width,
                currentBounds.Height
            );

            if (xMovement.IntersectsWith(zone.Bounds))
            {
                // 不可侵領域との位置関係に基づいて調整
                if (currentBounds.X + currentBounds.Width <= zone.Bounds.X)
                {
                    // 左から右への移動時
                    adjustedBounds.X = zone.Bounds.X - adjustedBounds.Width;
                }
                else if (currentBounds.X >= zone.Bounds.X + zone.Bounds.Width)
                {
                    // 右から左への移動時
                    adjustedBounds.X = zone.Bounds.X + zone.Bounds.Width;
                }
                else
                {
                    adjustedBounds.X = currentBounds.X;
                }
            }

            // Y軸方向の移動をチェック
            Rectangle yMovement = new Rectangle(
                adjustedBounds.X,
                proposedBounds.Y,
                adjustedBounds.Width,
                proposedBounds.Height
            );

            if (yMovement.IntersectsWith(zone.Bounds))
            {
                // 不可侵領域との位置関係に基づいて調整
                if (currentBounds.Y + currentBounds.Height <= zone.Bounds.Y)
                {
                    // 上から下への移動時
                    adjustedBounds.Y = zone.Bounds.Y - adjustedBounds.Height;
                }
                else if (currentBounds.Y >= zone.Bounds.Y + zone.Bounds.Height)
                {
                    // 下から上への移動時
                    adjustedBounds.Y = zone.Bounds.Y + zone.Bounds.Height;
                }
                else
                {
                    adjustedBounds.Y = currentBounds.Y;
                }
            }
        }

        return adjustedBounds;
    }
    public Size GetValidSize(Rectangle currentBounds, Size proposedSize)
    {
        Size adjustedSize = proposedSize;

        // リサイズ方向を判定
        bool isGrowingWidth = proposedSize.Width > currentBounds.Width;
        bool isGrowingHeight = proposedSize.Height > currentBounds.Height;

        foreach (var zone in zones)
        {
            // X方向の拡大をチェック
            if (isGrowingWidth)
            {
                Rectangle xResize = new Rectangle(
                    currentBounds.X,
                    currentBounds.Y,
                    proposedSize.Width,
                    currentBounds.Height
                );

                if (xResize.IntersectsWith(zone.Bounds))
                {
                    if (currentBounds.X < zone.Bounds.X)
                    {
                        adjustedSize.Width = zone.Bounds.X - currentBounds.X;
                    }
                }
            }

            // Y方向の拡大をチェック
            if (isGrowingHeight)
            {
                Rectangle yResize = new Rectangle(
                    currentBounds.X,
                    currentBounds.Y,
                    currentBounds.Width,
                    proposedSize.Height
                );

                if (yResize.IntersectsWith(zone.Bounds))
                {
                    if (currentBounds.Y < zone.Bounds.Y)
                    {
                        adjustedSize.Height = zone.Bounds.Y - currentBounds.Y;
                    }
                }
            }
        }

        return adjustedSize;
    }
    public void Draw(Graphics g)
    {
        if (!MainGame.IsDebugMode) return;

        foreach (var zone in zones)
        {
            zone.Draw(g);
        }
    }
}