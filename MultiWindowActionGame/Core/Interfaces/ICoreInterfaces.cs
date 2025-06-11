// Core/Interfaces/ICoreInterfaces.cs
using System.Numerics;

namespace MultiWindowActionGame.Core.Interfaces
{
    /// <summary>
    /// 描画可能なオブジェクトを表すインターフェース
    /// </summary>
    public interface IDrawable
    {
        /// <summary>
        /// オブジェクトを描画する
        /// </summary>
        /// <param name="graphics">描画に使用するGraphicsオブジェクト</param>
        void Draw(Graphics graphics);

        /// <summary>
        /// 再描画が必要かどうかを示すフラグ
        /// </summary>
        bool NeedsRedraw { get; }

        /// <summary>
        /// 再描画フラグをセットする
        /// </summary>
        void MarkForRedraw();

        /// <summary>
        /// 描画の優先度（高い値ほど前面に描画）
        /// </summary>
        int DrawPriority { get; }
    }

    /// <summary>
    /// 更新可能なオブジェクトを表すインターフェース
    /// </summary>
    public interface IUpdatable
    {
        /// <summary>
        /// オブジェクトの状態を更新する
        /// </summary>
        /// <param name="deltaTime">前回の更新からの経過時間（秒）</param>
        /// <returns>更新処理のタスク</returns>
        Task UpdateAsync(float deltaTime);

        /// <summary>
        /// 更新の優先度（高い値ほど先に更新）
        /// </summary>
        int UpdatePriority { get; }

        /// <summary>
        /// 更新が有効かどうか
        /// </summary>
        bool IsUpdateEnabled { get; }
    }

    /// <summary>
    /// 変形可能なオブジェクトを表すインターフェース
    /// </summary>
    public interface ITransformable
    {
        /// <summary>
        /// オブジェクトの位置
        /// </summary>
        Point Position { get; }

        /// <summary>
        /// オブジェクトのサイズ
        /// </summary>
        Size Size { get; }

        /// <summary>
        /// オブジェクトの境界矩形
        /// </summary>
        Rectangle Bounds { get; }

        /// <summary>
        /// 位置を設定する
        /// </summary>
        /// <param name="position">新しい位置</param>
        void SetPosition(Point position);

        /// <summary>
        /// サイズを設定する
        /// </summary>
        /// <param name="size">新しいサイズ</param>
        void SetSize(Size size);

        /// <summary>
        /// 相対的に移動する
        /// </summary>
        /// <param name="delta">移動量</param>
        void Move(Vector2 delta);

        /// <summary>
        /// スケーリングを適用する
        /// </summary>
        /// <param name="scale">スケール値</param>
        void Scale(SizeF scale);

        /// <summary>
        /// 変形イベント
        /// </summary>
        event EventHandler<TransformChangedEventArgs>? TransformChanged;
    }

    /// <summary>
    /// アクティブ状態を持つオブジェクトを表すインターフェース
    /// </summary>
    public interface IActivatable
    {
        /// <summary>
        /// アクティブ状態かどうか
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// アクティブ状態にする
        /// </summary>
        void Activate();

        /// <summary>
        /// 非アクティブ状態にする
        /// </summary>
        void Deactivate();

        /// <summary>
        /// アクティブ状態変更イベント
        /// </summary>
        event EventHandler<ActiveStateChangedEventArgs>? ActiveStateChanged;
    }

    /// <summary>
    /// 一意のIDを持つオブジェクトを表すインターフェース
    /// </summary>
    public interface IIdentifiable
    {
        /// <summary>
        /// 一意のID
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// 表示名
        /// </summary>
        string Name { get; }

        /// <summary>
        /// タイプ名
        /// </summary>
        string TypeName { get; }
    }

    /// <summary>
    /// 階層構造を持つオブジェクトを表すインターフェース
    /// </summary>
    /// <typeparam name="T">階層を構成するオブジェクトの型</typeparam>
    public interface IHierarchical<T> where T : IHierarchical<T>
    {
        /// <summary>
        /// 親オブジェクト
        /// </summary>
        T? Parent { get; }

        /// <summary>
        /// 子オブジェクトのコレクション
        /// </summary>
        IReadOnlyCollection<T> Children { get; }

        /// <summary>
        /// 親を設定する
        /// </summary>
        /// <param name="parent">新しい親（nullで親を解除）</param>
        void SetParent(T? parent);

        /// <summary>
        /// 子を追加する
        /// </summary>
        /// <param name="child">追加する子オブジェクト</param>
        void AddChild(T child);

        /// <summary>
        /// 子を削除する
        /// </summary>
        /// <param name="child">削除する子オブジェクト</param>
        void RemoveChild(T child);

        /// <summary>
        /// 指定されたオブジェクトの祖先かどうかを判定する
        /// </summary>
        /// <param name="other">判定対象のオブジェクト</param>
        /// <returns>祖先の場合true</returns>
        bool IsAncestorOf(T other);

        /// <summary>
        /// 指定されたオブジェクトの子孫かどうかを判定する
        /// </summary>
        /// <param name="other">判定対象のオブジェクト</param>
        /// <returns>子孫の場合true</returns>
        bool IsDescendantOf(T other);

        /// <summary>
        /// ルートオブジェクトを取得する
        /// </summary>
        /// <returns>ルートオブジェクト</returns>
        T GetRoot();

        /// <summary>
        /// 階層の深さを取得する
        /// </summary>
        /// <returns>ルートからの深さ</returns>
        int GetDepth();

        /// <summary>
        /// 階層変更イベント
        /// </summary>
        event EventHandler<HierarchyChangedEventArgs<T>>? HierarchyChanged;
    }

    /// <summary>
    /// 衝突判定可能なオブジェクトを表すインターフェース
    /// </summary>
    public interface ICollidable
    {
        /// <summary>
        /// 衝突判定用の境界矩形
        /// </summary>
        Rectangle CollisionBounds { get; }

        /// <summary>
        /// 固体（他のオブジェクトの通過を妨げる）かどうか
        /// </summary>
        bool IsSolid { get; }

        /// <summary>
        /// 衝突レイヤー
        /// </summary>
        CollisionLayer CollisionLayer { get; }

        /// <summary>
        /// 他のオブジェクトとの衝突をチェックする
        /// </summary>
        /// <param name="other">衝突対象のオブジェクト</param>
        /// <returns>衝突している場合true</returns>
        bool CheckCollision(ICollidable other);

        /// <summary>
        /// 指定された矩形との衝突をチェックする
        /// </summary>
        /// <param name="bounds">衝突対象の矩形</param>
        /// <returns>衝突している場合true</returns>
        bool CheckCollision(Rectangle bounds);

        /// <summary>
        /// 指定された点が内部にあるかをチェックする
        /// </summary>
        /// <param name="point">チェック対象の点</param>
        /// <returns>内部にある場合true</returns>
        bool Contains(Point point);

        /// <summary>
        /// 衝突イベント
        /// </summary>
        event EventHandler<CollisionEventArgs>? CollisionDetected;
    }

    /// <summary>
    /// 最小化・復元可能なオブジェクトを表すインターフェース
    /// </summary>
    public interface IMinimizable
    {
        /// <summary>
        /// 最小化されているかどうか
        /// </summary>
        bool IsMinimized { get; }

        /// <summary>
        /// 最小化された時刻
        /// </summary>
        DateTime? MinimizedTime { get; }

        /// <summary>
        /// 最小化する
        /// </summary>
        void Minimize();

        /// <summary>
        /// 復元する
        /// </summary>
        void Restore();

        /// <summary>
        /// 最小化状態変更イベント
        /// </summary>
        event EventHandler<MinimizeStateChangedEventArgs>? MinimizeStateChanged;
    }

    /// <summary>
    /// Z-Order管理可能なオブジェクトを表すインターフェース
    /// </summary>
    public interface IZOrderable
    {
        /// <summary>
        /// Z-Order値
        /// </summary>
        int ZOrder { get; }

        /// <summary>
        /// Z-Order優先度
        /// </summary>
        ZOrderPriority Priority { get; }

        /// <summary>
        /// 最前面に移動する
        /// </summary>
        void BringToFront();

        /// <summary>
        /// 最背面に移動する
        /// </summary>
        void SendToBack();

        /// <summary>
        /// Z-Orderを設定する
        /// </summary>
        /// <param name="zOrder">新しいZ-Order値</param>
        void SetZOrder(int zOrder);

        /// <summary>
        /// Z-Order変更イベント
        /// </summary>
        event EventHandler<ZOrderChangedEventArgs>? ZOrderChanged;
    }

    // ===== 列挙型 =====

    /// <summary>
    /// 衝突レイヤー
    /// </summary>
    public enum CollisionLayer
    {
        Default = 0,
        Player = 1,
        Window = 2,
        Goal = 3,
        Button = 4,
        NoEntry = 5,
        Debug = 6
    }

    /// <summary>
    /// Z-Order優先度
    /// </summary>
    public enum ZOrderPriority
    {
        Bottom = 1,
        Window = 2,
        WindowMark = 3,
        Button = 4,
        Goal = 5,
        Player = 6,
        DebugLayer = 7
    }

    // ===== イベント引数 =====

    /// <summary>
    /// 変形変更イベント引数
    /// </summary>
    public class TransformChangedEventArgs : EventArgs
    {
        public Point OldPosition { get; }
        public Point NewPosition { get; }
        public Size OldSize { get; }
        public Size NewSize { get; }
        public TransformChangeType ChangeType { get; }

        public TransformChangedEventArgs(Point oldPosition, Point newPosition, Size oldSize, Size newSize, TransformChangeType changeType)
        {
            OldPosition = oldPosition;
            NewPosition = newPosition;
            OldSize = oldSize;
            NewSize = newSize;
            ChangeType = changeType;
        }
    }

    /// <summary>
    /// アクティブ状態変更イベント引数
    /// </summary>
    public class ActiveStateChangedEventArgs : EventArgs
    {
        public bool IsActive { get; }
        public DateTime Timestamp { get; }

        public ActiveStateChangedEventArgs(bool isActive)
        {
            IsActive = isActive;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 階層変更イベント引数
    /// </summary>
    public class HierarchyChangedEventArgs<T> : EventArgs where T : IHierarchical<T>
    {
        public T? OldParent { get; }
        public T? NewParent { get; }
        public T Child { get; }
        public HierarchyChangeType ChangeType { get; }

        public HierarchyChangedEventArgs(T child, T? oldParent, T? newParent, HierarchyChangeType changeType)
        {
            Child = child;
            OldParent = oldParent;
            NewParent = newParent;
            ChangeType = changeType;
        }
    }

    /// <summary>
    /// 衝突イベント引数
    /// </summary>
    public class CollisionEventArgs : EventArgs
    {
        public ICollidable Other { get; }
        public Rectangle IntersectionArea { get; }
        public CollisionType CollisionType { get; }

        public CollisionEventArgs(ICollidable other, Rectangle intersectionArea, CollisionType collisionType)
        {
            Other = other;
            IntersectionArea = intersectionArea;
            CollisionType = collisionType;
        }
    }

    /// <summary>
    /// 最小化状態変更イベント引数
    /// </summary>
    public class MinimizeStateChangedEventArgs : EventArgs
    {
        public bool IsMinimized { get; }
        public DateTime Timestamp { get; }

        public MinimizeStateChangedEventArgs(bool isMinimized)
        {
            IsMinimized = isMinimized;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Z-Order変更イベント引数
    /// </summary>
    public class ZOrderChangedEventArgs : EventArgs
    {
        public int OldZOrder { get; }
        public int NewZOrder { get; }
        public ZOrderPriority Priority { get; }

        public ZOrderChangedEventArgs(int oldZOrder, int newZOrder, ZOrderPriority priority)
        {
            OldZOrder = oldZOrder;
            NewZOrder = newZOrder;
            Priority = priority;
        }
    }

    // ===== 変更タイプ列挙型 =====

    /// <summary>
    /// 変形変更の種類
    /// </summary>
    public enum TransformChangeType
    {
        Position,
        Size,
        Both
    }

    /// <summary>
    /// 階層変更の種類
    /// </summary>
    public enum HierarchyChangeType
    {
        ParentChanged,
        ChildAdded,
        ChildRemoved
    }

    /// <summary>
    /// 衝突の種類
    /// </summary>
    public enum CollisionType
    {
        Enter,      // 衝突開始
        Stay,       // 衝突継続
        Exit        // 衝突終了
    }
}