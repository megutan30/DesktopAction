// Core/Services/WindowCollisionService.cs
using MultiWindowActionGame.Core.Services.Interfaces;
using MultiWindowActionGame.Core.Interfaces;
using MultiWindowActionGame.Core.Constants;
using System.Numerics;

namespace MultiWindowActionGame.Core.Services
{
    /// <summary>
    /// ウィンドウ衝突判定サービスの実装
    /// </summary>
    public class WindowCollisionService : IWindowCollisionService, IDisposable
    {
        private readonly INoEntryZoneService _noEntryZoneService;
        private readonly IEventService _eventService;
        private bool _disposed = false;

        public WindowCollisionService(INoEntryZoneService noEntryZoneService, IEventService eventService)
        {
            _noEntryZoneService = noEntryZoneService ?? throw new ArgumentNullException(nameof(noEntryZoneService));
            _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
        }

        public bool IsFullyContained(Rectangle inner, Rectangle outer)
        {
            ThrowIfDisposed();
            return outer.Contains(inner);
        }

        public bool Intersects(Rectangle rect1, Rectangle rect2)
        {
            ThrowIfDisposed();
            return rect1.IntersectsWith(rect2);
        }

        public IGameWindow? GetTopWindowAt(Rectangle bounds, IReadOnlyList<IGameWindow> windows)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(windows);

            return windows
                .Where(w => w.Bounds.IntersectsWith(bounds))
                .OrderByDescending(w => GetWindowPriority(w))
                .FirstOrDefault();
        }

        public IReadOnlyList<IGameWindow> GetIntersectingWindows(Rectangle bounds, IReadOnlyList<IGameWindow> windows)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(windows);

            return windows
                .Where(w => w.Bounds.IntersectsWith(bounds))
                .OrderByDescending(w => GetWindowPriority(w))
                .ToList();
        }

        public Rectangle GetValidPosition(Rectangle currentBounds, Rectangle proposedBounds, IReadOnlyList<IGameWindow> obstacles)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(obstacles);

            // まず不可侵領域との衝突をチェック
            var adjustedBounds = _noEntryZoneService.GetValidPosition(currentBounds, proposedBounds);

            // 次にウィンドウとの衝突をチェック
            foreach (var obstacle in obstacles)
            {
                if (obstacle.Bounds.IntersectsWith(adjustedBounds))
                {
                    adjustedBounds = AdjustPositionForObstacle(currentBounds, adjustedBounds, obstacle.Bounds);
                }
            }

            return adjustedBounds;
        }

        public Size GetValidSize(Rectangle currentBounds, Size proposedSize, IReadOnlyList<IGameWindow> obstacles)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(obstacles);

            // まず不可侵領域との衝突をチェック
            var adjustedSize = _noEntryZoneService.GetValidSize(currentBounds, proposedSize);

            // 次にウィンドウとの衝突をチェック
            var proposedBounds = new Rectangle(currentBounds.Location, adjustedSize);
            
            foreach (var obstacle in obstacles)
            {
                if (obstacle.Bounds.IntersectsWith(proposedBounds))
                {
                    adjustedSize = AdjustSizeForObstacle(currentBounds, adjustedSize, obstacle.Bounds);
                }
            }

            // 最小サイズを保証
            adjustedSize.Width = Math.Max(adjustedSize.Width, GameConstants.Window.MINIMUM_WIDTH);
            adjustedSize.Height = Math.Max(adjustedSize.Height, GameConstants.Window.MINIMUM_HEIGHT);

            return adjustedSize;
        }

        public CollisionResult CheckCollision(ICollidable obj1, ICollidable obj2)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(obj1);
            ArgumentNullException.ThrowIfNull(obj2);

            var bounds1 = obj1.CollisionBounds;
            var bounds2 = obj2.CollisionBounds;
            
            var hasCollision = bounds1.IntersectsWith(bounds2);
            var intersectionArea = hasCollision ? Rectangle.Intersect(bounds1, bounds2) : Rectangle.Empty;
            var separationVector = CalculateSeparationVector(bounds1, bounds2);

            return new CollisionResult
            {
                HasCollision = hasCollision,
                IntersectionArea = intersectionArea,
                SeparationVector = separationVector,
                CollisionType = DetermineCollisionType(obj1, obj2, hasCollision)
            };
        }

        public IReadOnlyList<ICollidable> GetCollisions(ICollidable target, IReadOnlyList<ICollidable> candidates)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(candidates);

            var collisions = new List<ICollidable>();

            foreach (var candidate in candidates)
            {
                if (candidate != target && target.CheckCollision(candidate))
                {
                    collisions.Add(candidate);
                }
            }

            return collisions;
        }

        private Rectangle AdjustPositionForObstacle(Rectangle currentBounds, Rectangle proposedBounds, Rectangle obstacle)
        {
            var adjustedBounds = proposedBounds;

            // 水平方向の調整
            if (currentBounds.Right <= obstacle.Left && proposedBounds.Right > obstacle.Left)
            {
                // 左から右への移動で障害物に衝突
                adjustedBounds.X = obstacle.Left - adjustedBounds.Width;
            }
            else if (currentBounds.Left >= obstacle.Right && proposedBounds.Left < obstacle.Right)
            {
                // 右から左への移動で障害物に衝突
                adjustedBounds.X = obstacle.Right;
            }

            // 垂直方向の調整
            if (currentBounds.Bottom <= obstacle.Top && proposedBounds.Bottom > obstacle.Top)
            {
                // 上から下への移動で障害物に衝突
                adjustedBounds.Y = obstacle.Top - adjustedBounds.Height;
            }
            else if (currentBounds.Top >= obstacle.Bottom && proposedBounds.Top < obstacle.Bottom)
            {
                // 下から上への移動で障害物に衝突
                adjustedBounds.Y = obstacle.Bottom;
            }

            return adjustedBounds;
        }

        private Size AdjustSizeForObstacle(Rectangle currentBounds, Size proposedSize, Rectangle obstacle)
        {
            var adjustedSize = proposedSize;

            // リサイズ方向を判定
            bool isGrowingWidth = proposedSize.Width > currentBounds.Width;
            bool isGrowingHeight = proposedSize.Height > currentBounds.Height;

            if (isGrowingWidth && currentBounds.X < obstacle.X)
            {
                var maxWidth = obstacle.X - currentBounds.X;
                adjustedSize.Width = Math.Min(adjustedSize.Width, maxWidth);
            }

            if (isGrowingHeight && currentBounds.Y < obstacle.Y)
            {
                var maxHeight = obstacle.Y - currentBounds.Y;
                adjustedSize.Height = Math.Min(adjustedSize.Height, maxHeight);
            }

            return adjustedSize;
        }

        private Vector2 CalculateSeparationVector(Rectangle bounds1, Rectangle bounds2)
        {
            if (!bounds1.IntersectsWith(bounds2))
                return Vector2.Zero;

            var center1 = new Vector2(bounds1.X + bounds1.Width / 2f, bounds1.Y + bounds1.Height / 2f);
            var center2 = new Vector2(bounds2.X + bounds2.Width / 2f, bounds2.Y + bounds2.Height / 2f);

            var direction = center1 - center2;
            if (direction.LengthSquared() == 0)
                return new Vector2(1, 0); // デフォルト方向

            direction = Vector2.Normalize(direction);

            // 重複の深さを計算
            var intersection = Rectangle.Intersect(bounds1, bounds2);
            var separationDistance = Math.Min(intersection.Width, intersection.Height);

            return direction * separationDistance;
        }

        private CollisionType DetermineCollisionType(ICollidable obj1, ICollidable obj2, bool hasCollision)
        {
            // 簡単な実装：実際のゲームロジックに応じて拡張可能
            return hasCollision ? CollisionType.Enter : CollisionType.Exit;
        }

        private int GetWindowPriority(IGameWindow window)
        {
            // ウィンドウの優先度を返す（Z-orderに基づく）
            if (window is IZOrderable zOrderable)
            {
                return (int)zOrderable.Priority * 1000 + zOrderable.ZOrder;
            }
            return 0;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WindowCollisionService));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }

    /// <summary>
    /// 衝突判定関連のユーティリティメソッド
    /// </summary>
    public static class CollisionHelper
    {
        /// <summary>
        /// AABBベースの衝突判定
        /// </summary>
        public static bool AABB(Rectangle rect1, Rectangle rect2)
        {
            return rect1.Left < rect2.Right &&
                   rect1.Right > rect2.Left &&
                   rect1.Top < rect2.Bottom &&
                   rect1.Bottom > rect2.Top;
        }

        /// <summary>
        /// 点と矩形の衝突判定
        /// </summary>
        public static bool PointInRectangle(Point point, Rectangle rectangle)
        {
            return point.X >= rectangle.Left &&
                   point.X < rectangle.Right &&
                   point.Y >= rectangle.Top &&
                   point.Y < rectangle.Bottom;
        }

        /// <summary>
        /// 円と矩形の衝突判定
        /// </summary>
        public static bool CircleRectangle(Point circleCenter, float radius, Rectangle rectangle)
        {
            var closestX = Math.Max(rectangle.Left, Math.Min(circleCenter.X, rectangle.Right));
            var closestY = Math.Max(rectangle.Top, Math.Min(circleCenter.Y, rectangle.Bottom));

            var distanceSquared = Math.Pow(circleCenter.X - closestX, 2) + Math.Pow(circleCenter.Y - closestY, 2);
            return distanceSquared < radius * radius;
        }

        /// <summary>
        /// 2つの円の衝突判定
        /// </summary>
        public static bool CircleCircle(Point center1, float radius1, Point center2, float radius2)
        {
            var distanceSquared = Math.Pow(center1.X - center2.X, 2) + Math.Pow(center1.Y - center2.Y, 2);
            var combinedRadius = radius1 + radius2;
            return distanceSquared < combinedRadius * combinedRadius;
        }

        /// <summary>
        /// レイと矩形の衝突判定
        /// </summary>
        public static bool RayRectangle(Point rayOrigin, Vector2 rayDirection, Rectangle rectangle, out float distance)
        {
            distance = float.MaxValue;

            if (rayDirection.X == 0 && rayDirection.Y == 0)
                return false;

            var invDirX = rayDirection.X != 0 ? 1.0f / rayDirection.X : float.MaxValue;
            var invDirY = rayDirection.Y != 0 ? 1.0f / rayDirection.Y : float.MaxValue;

            var tMinX = (rectangle.Left - rayOrigin.X) * invDirX;
            var tMaxX = (rectangle.Right - rayOrigin.X) * invDirX;
            var tMinY = (rectangle.Top - rayOrigin.Y) * invDirY;
            var tMaxY = (rectangle.Bottom - rayOrigin.Y) * invDirY;

            if (tMinX > tMaxX) (tMinX, tMaxX) = (tMaxX, tMinX);
            if (tMinY > tMaxY) (tMinY, tMaxY) = (tMaxY, tMinY);

            var tMin = Math.Max(tMinX, tMinY);
            var tMax = Math.Min(tMaxX, tMaxY);

            if (tMax < 0 || tMin > tMax)
                return false;

            distance = tMin >= 0 ? tMin : tMax;
            return true;
        }

        /// <summary>
        /// 最短分離ベクトルを計算
        /// </summary>
        public static Vector2 GetMinimumTranslationVector(Rectangle rect1, Rectangle rect2)
        {
            if (!rect1.IntersectsWith(rect2))
                return Vector2.Zero;

            var intersection = Rectangle.Intersect(rect1, rect2);
            
            Vector2 mtv;
            if (intersection.Width < intersection.Height)
            {
                // 水平方向に分離
                var direction = rect1.X + rect1.Width / 2 < rect2.X + rect2.Width / 2 ? -1 : 1;
                mtv = new Vector2(intersection.Width * direction, 0);
            }
            else
            {
                // 垂直方向に分離
                var direction = rect1.Y + rect1.Height / 2 < rect2.Y + rect2.Height / 2 ? -1 : 1;
                mtv = new Vector2(0, intersection.Height * direction);
            }

            return mtv;
        }

        /// <summary>
        /// 複数の矩形の境界ボックスを計算
        /// </summary>
        public static Rectangle GetBoundingBox(IEnumerable<Rectangle> rectangles)
        {
            var rects = rectangles.ToList();
            if (!rects.Any())
                return Rectangle.Empty;

            var minX = rects.Min(r => r.Left);
            var minY = rects.Min(r => r.Top);
            var maxX = rects.Max(r => r.Right);
            var maxY = rects.Max(r => r.Bottom);

            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// 矩形の面積を計算
        /// </summary>
        public static int GetArea(Rectangle rectangle)
        {
            return rectangle.Width * rectangle.Height;
        }

        /// <summary>
        /// 矩形の周囲長を計算
        /// </summary>
        public static int GetPerimeter(Rectangle rectangle)
        {
            return 2 * (rectangle.Width + rectangle.Height);
        }

        /// <summary>
        /// 2つの矩形の重複面積を計算
        /// </summary>
        public static int GetOverlapArea(Rectangle rect1, Rectangle rect2)
        {
            if (!rect1.IntersectsWith(rect2))
                return 0;

            var intersection = Rectangle.Intersect(rect1, rect2);
            return GetArea(intersection);
        }

        /// <summary>
        /// 矩形を拡張する
        /// </summary>
        public static Rectangle Expand(Rectangle rectangle, int amount)
        {
            return new Rectangle(
                rectangle.X - amount,
                rectangle.Y - amount,
                rectangle.Width + 2 * amount,
                rectangle.Height + 2 * amount
            );
        }

        /// <summary>
        /// 矩形を縮小する
        /// </summary>
        public static Rectangle Contract(Rectangle rectangle, int amount)
        {
            var newWidth = Math.Max(0, rectangle.Width - 2 * amount);
            var newHeight = Math.Max(0, rectangle.Height - 2 * amount);
            
            return new Rectangle(
                rectangle.X + amount,
                rectangle.Y + amount,
                newWidth,
                newHeight
            );
        }

        /// <summary>
        /// 衝突判定のパフォーマンス統計
        /// </summary>
        public static CollisionStatistics CalculateStatistics(IReadOnlyList<ICollidable> objects)
        {
            var statistics = new CollisionStatistics
            {
                TotalObjects = objects.Count
            };

            // 総当たりで衝突をチェック
            var collisionPairs = new List<(ICollidable, ICollidable)>();
            
            for (int i = 0; i < objects.Count; i++)
            {
                for (int j = i + 1; j < objects.Count; j++)
                {
                    statistics.TotalChecks++;
                    
                    if (objects[i].CheckCollision(objects[j]))
                    {
                        statistics.TotalCollisions++;
                        collisionPairs.Add((objects[i], objects[j]));
                    }
                }
            }

            statistics.CollisionRatio = statistics.TotalChecks > 0 ? 
                (double)statistics.TotalCollisions / statistics.TotalChecks : 0;

            return statistics;
        }
    }

    /// <summary>
    /// 衝突判定の統計情報
    /// </summary>
    public class CollisionStatistics
    {
        public int TotalObjects { get; set; }
        public int TotalChecks { get; set; }
        public int TotalCollisions { get; set; }
        public double CollisionRatio { get; set; }

        public string GenerateReport()
        {
            return $@"Collision Statistics:
Total Objects: {TotalObjects}
Total Checks: {TotalChecks}
Total Collisions: {TotalCollisions}
Collision Ratio: {CollisionRatio:P2}";
        }
    }