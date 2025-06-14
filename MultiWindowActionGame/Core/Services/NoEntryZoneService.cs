// Core/Services/NoEntryZoneService.cs
using MultiWindowActionGame.Core.Services.Interfaces;
using MultiWindowActionGame.Core.Constants;
using System.Collections.Concurrent;

namespace MultiWindowActionGame.Core.Services
{
    /// <summary>
    /// 不可侵領域管理サービスの実装
    /// </summary>
    public class NoEntryZoneService : INoEntryZoneService, IDisposable
    {
        private readonly ConcurrentBag<Rectangle> _zones = new();
        private readonly IEventService _eventService;
        private readonly object _lock = new();
        private bool _disposed = false;

        public NoEntryZoneService(IEventService eventService)
        {
            _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
        }

        public void AddZone(Rectangle bounds)
        {
            ThrowIfDisposed();
            
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"Invalid zone bounds: {bounds}");
                return;
            }

            _zones.Add(bounds);
            
            _eventService.Publish(new NoEntryZoneAddedEvent
            {
                ZoneBounds = bounds,
                TotalZones = _zones.Count
            });

            System.Diagnostics.Debug.WriteLine($"Added no-entry zone: {bounds}");
        }

        public void AddZone(Point location, Size size)
        {
            ThrowIfDisposed();
            AddZone(new Rectangle(location, size));
        }

        public void RemoveZone(Rectangle bounds)
        {
            ThrowIfDisposed();

            lock (_lock)
            {
                var zonesToKeep = _zones.Where(z => z != bounds).ToList();
                _zones.Clear();
                
                foreach (var zone in zonesToKeep)
                {
                    _zones.Add(zone);
                }
            }

            _eventService.Publish(new NoEntryZoneRemovedEvent
            {
                ZoneBounds = bounds,
                TotalZones = _zones.Count
            });

            System.Diagnostics.Debug.WriteLine($"Removed no-entry zone: {bounds}");
        }

        public void ClearZones()
        {
            ThrowIfDisposed();

            var oldCount = _zones.Count;
            
            lock (_lock)
            {
                _zones.Clear();
            }

            _eventService.Publish(new NoEntryZonesClearedEvent
            {
                ClearedZoneCount = oldCount
            });

            System.Diagnostics.Debug.WriteLine($"Cleared {oldCount} no-entry zones");
        }

        public bool IntersectsWithAnyZone(Rectangle bounds)
        {
            ThrowIfDisposed();

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return false;

            return _zones.Any(zone => zone.IntersectsWith(bounds));
        }

        public Rectangle GetValidPosition(Rectangle currentBounds, Rectangle proposedBounds)
        {
            ThrowIfDisposed();

            if (!IntersectsWithAnyZone(proposedBounds))
                return proposedBounds;

            var adjustedBounds = proposedBounds;

            foreach (var zone in _zones)
            {
                adjustedBounds = AdjustPositionForZone(currentBounds, adjustedBounds, zone);
            }

            return adjustedBounds;
        }

        public Size GetValidSize(Rectangle currentBounds, Size proposedSize)
        {
            ThrowIfDisposed();

            var adjustedSize = proposedSize;
            bool isGrowingWidth = proposedSize.Width > currentBounds.Width;
            bool isGrowingHeight = proposedSize.Height > currentBounds.Height;

            foreach (var zone in _zones)
            {
                if (isGrowingWidth)
                {
                    var xResize = new Rectangle(
                        currentBounds.X,
                        currentBounds.Y,
                        proposedSize.Width,
                        currentBounds.Height
                    );

                    if (xResize.IntersectsWith(zone) && currentBounds.X < zone.X)
                    {
                        adjustedSize.Width = Math.Max(
                            GameConstants.Window.MINIMUM_WIDTH,
                            zone.X - currentBounds.X
                        );
                    }
                }

                if (isGrowingHeight)
                {
                    var yResize = new Rectangle(
                        currentBounds.X,
                        currentBounds.Y,
                        currentBounds.Width,
                        proposedSize.Height
                    );

                    if (yResize.IntersectsWith(zone) && currentBounds.Y < zone.Y)
                    {
                        adjustedSize.Height = Math.Max(
                            GameConstants.Window.MINIMUM_HEIGHT,
                            zone.Y - currentBounds.Y
                        );
                    }
                }
            }

            return adjustedSize;
        }

        public IReadOnlyList<Rectangle> GetZones()
        {
            ThrowIfDisposed();
            return _zones.ToList();
        }

        public IReadOnlyList<Rectangle> GetIntersectingZones(Rectangle bounds)
        {
            ThrowIfDisposed();

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return new List<Rectangle>();

            return _zones.Where(zone => zone.IntersectsWith(bounds)).ToList();
        }

        private Rectangle AdjustPositionForZone(Rectangle currentBounds, Rectangle proposedBounds, Rectangle zone)
        {
            var adjustedBounds = proposedBounds;

            // X軸方向の調整
            var xMovement = new Rectangle(
                proposedBounds.X,
                currentBounds.Y,
                proposedBounds.Width,
                currentBounds.Height
            );

            if (xMovement.IntersectsWith(zone))
            {
                if (currentBounds.Right <= zone.Left)
                {
                    // 左から右への移動時
                    adjustedBounds.X = zone.Left - adjustedBounds.Width;
                }
                else if (currentBounds.Left >= zone.Right)
                {
                    // 右から左への移動時
                    adjustedBounds.X = zone.Right;
                }
                else
                {
                    adjustedBounds.X = currentBounds.X;
                }
            }

            // Y軸方向の調整
            var yMovement = new Rectangle(
                adjustedBounds.X,
                proposedBounds.Y,
                adjustedBounds.Width,
                proposedBounds.Height
            );

            if (yMovement.IntersectsWith(zone))
            {
                if (currentBounds.Bottom <= zone.Top)
                {
                    // 上から下への移動時
                    adjustedBounds.Y = zone.Top - adjustedBounds.Height;
                }
                else if (currentBounds.Top >= zone.Bottom)
                {
                    // 下から上への移動時
                    adjustedBounds.Y = zone.Bottom;
                }
                else
                {
                    adjustedBounds.Y = currentBounds.Y;
                }
            }

            return adjustedBounds;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NoEntryZoneService));
        }

        public void Dispose()
        {
            if (_disposed) return;

            lock (_lock)
            {
                _zones.Clear();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// 不可侵領域関連のイベント
    /// </summary>
    public class NoEntryZoneAddedEvent
    {
        public Rectangle ZoneBounds { get; set; }
        public int TotalZones { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class NoEntryZoneRemovedEvent
    {
        public Rectangle ZoneBounds { get; set; }
        public int TotalZones { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class NoEntryZonesClearedEvent
    {
        public int ClearedZoneCount { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 不可侵領域関連のユーティリティメソッド
    /// </summary>
    public static class NoEntryZoneHelper
    {
        /// <summary>
        /// 複数の矩形から合成された不可侵領域を作成
        /// </summary>
        public static IReadOnlyList<Rectangle> CreateZonesFromRectangles(IEnumerable<Rectangle> rectangles)
        {
            return rectangles
                .Where(r => r.Width > 0 && r.Height > 0)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// グリッドベースの不可侵領域を作成
        /// </summary>
        public static IReadOnlyList<Rectangle> CreateGridZones(Rectangle area, int gridWidth, int gridHeight, bool[,] blockedCells)
        {
            var zones = new List<Rectangle>();
            int cellWidth = area.Width / gridWidth;
            int cellHeight = area.Height / gridHeight;

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (x < blockedCells.GetLength(0) && y < blockedCells.GetLength(1) && blockedCells[x, y])
                    {
                        zones.Add(new Rectangle(
                            area.X + x * cellWidth,
                            area.Y + y * cellHeight,
                            cellWidth,
                            cellHeight
                        ));
                    }
                }
            }

            return zones;
        }

        /// <summary>
        /// 円形の不可侵領域を矩形の集合で近似
        /// </summary>
        public static IReadOnlyList<Rectangle> CreateCircularZone(Point center, int radius, int approximationLevel = 8)
        {
            var zones = new List<Rectangle>();
            var cellSize = Math.Max(1, radius / approximationLevel);

            for (int x = center.X - radius; x <= center.X + radius; x += cellSize)
            {
                for (int y = center.Y - radius; y <= center.Y + radius; y += cellSize)
                {
                    var distance = Math.Sqrt(Math.Pow(x - center.X, 2) + Math.Pow(y - center.Y, 2));
                    if (distance <= radius)
                    {
                        zones.Add(new Rectangle(x, y, cellSize, cellSize));
                    }
                }
            }

            return zones;
        }

        /// <summary>
        /// 2つの領域間の最短距離を計算
        /// </summary>
        public static float CalculateDistance(Rectangle rect1, Rectangle rect2)
        {
            if (rect1.IntersectsWith(rect2))
                return 0f;

            float dx = Math.Max(0, Math.Max(rect1.Left - rect2.Right, rect2.Left - rect1.Right));
            float dy = Math.Max(0, Math.Max(rect1.Top - rect2.Bottom, rect2.Top - rect1.Bottom));
            
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 不可侵領域の統計情報を計算
        /// </summary>
        public static NoEntryZoneStatistics CalculateStatistics(IReadOnlyList<Rectangle> zones)
        {
            if (!zones.Any())
            {
                return new NoEntryZoneStatistics();
            }

            var totalArea = zones.Sum(z => z.Width * z.Height);
            var averageArea = totalArea / zones.Count;
            var minArea = zones.Min(z => z.Width * z.Height);
            var maxArea = zones.Max(z => z.Width * z.Height);

            var bounds = zones.Aggregate((r1, r2) => Rectangle.Union(r1, r2));
            var coverage = (double)totalArea / (bounds.Width * bounds.Height);

            return new NoEntryZoneStatistics
            {
                ZoneCount = zones.Count,
                TotalArea = totalArea,
                AverageArea = averageArea,
                MinArea = minArea,
                MaxArea = maxArea,
                BoundingBox = bounds,
                CoveragePercentage = coverage * 100
            };
        }

        /// <summary>
        /// 重複する領域を統合
        /// </summary>
        public static IReadOnlyList<Rectangle> MergeOverlappingZones(IEnumerable<Rectangle> zones)
        {
            var zoneList = zones.ToList();
            var merged = new List<Rectangle>();

            foreach (var zone in zoneList)
            {
                var overlapping = merged.Where(m => m.IntersectsWith(zone)).ToList();
                
                if (!overlapping.Any())
                {
                    merged.Add(zone);
                }
                else
                {
                    // 重複する領域を削除し、統合された領域を追加
                    foreach (var overlap in overlapping)
                    {
                        merged.Remove(overlap);
                    }
                    
                    var unionRect = overlapping.Aggregate(zone, Rectangle.Union);
                    merged.Add(unionRect);
                }
            }

            return merged;
        }

        /// <summary>
        /// 指定された方向への移動が可能かチェック
        /// </summary>
        public static bool CanMoveInDirection(Rectangle currentBounds, Rectangle[] zones, Direction direction, int distance)
        {
            var newBounds = direction switch
            {
                Direction.Up => new Rectangle(currentBounds.X, currentBounds.Y - distance, currentBounds.Width, currentBounds.Height),
                Direction.Down => new Rectangle(currentBounds.X, currentBounds.Y + distance, currentBounds.Width, currentBounds.Height),
                Direction.Left => new Rectangle(currentBounds.X - distance, currentBounds.Y, currentBounds.Width, currentBounds.Height),
                Direction.Right => new Rectangle(currentBounds.X + distance, currentBounds.Y, currentBounds.Width, currentBounds.Height),
                _ => currentBounds
            };

            return !zones.Any(zone => zone.IntersectsWith(newBounds));
        }
    }

    /// <summary>
    /// 不可侵領域の統計情報
    /// </summary>
    public class NoEntryZoneStatistics
    {
        public int ZoneCount { get; set; }
        public int TotalArea { get; set; }
        public double AverageArea { get; set; }
        public int MinArea { get; set; }
        public int MaxArea { get; set; }
        public Rectangle BoundingBox { get; set; }
        public double CoveragePercentage { get; set; }

        public string GenerateReport()
        {
            return $@"No-Entry Zone Statistics:
Zone Count: {ZoneCount}
Total Area: {TotalArea} pixels
Average Area: {AverageArea:F1} pixels
Min/Max Area: {MinArea} / {MaxArea} pixels
Bounding Box: {BoundingBox}
Coverage: {CoveragePercentage:F1}%";
        }
    }

    /// <summary>
    /// 移動方向の列挙型
    /// </summary>
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }
}