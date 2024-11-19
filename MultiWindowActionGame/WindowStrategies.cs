﻿using MultiWindowActionGame;
using System;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;

namespace MultiWindowActionGame
{
    public interface IWindowStrategy
    {
        void Update(GameWindow window, float deltaTime);
        void HandleInput(GameWindow window);
        void HandleResize(GameWindow window);
        void UpdateCursor(GameWindow window, Point clientMousePos);
    }

    public class NormalWindowStrategy : IWindowStrategy
    {
        public void Update(GameWindow window, float deltaTime)
        {
            // 通常ウィンドウの更新ロジック（必要に応じて）
        }

        public void HandleInput(GameWindow window)
        {
            // 通常ウィンドウの入力処理（必要に応じて）
        }
        public void HandleResize(GameWindow window) { }
        public void UpdateCursor(GameWindow window, Point clientMousePos)
        {
            window.Cursor = Cursors.Default;
        }
    }

    public class ResizableWindowStrategy : IWindowStrategy
    {
        private readonly ResizeEffect resizeEffect = new ResizeEffect();
        private bool isResizing = false;
        private Point lastMousePos;
        private Size originalSize;

        // リサイズ効果を公開するプロパティを追加
        public ResizeEffect ResizeEffect => resizeEffect;
        // 移動開始時の各ターゲットとの相対位置を保存
        private Dictionary<IEffectTarget, Point> initialRelativePositions = new Dictionary<IEffectTarget, Point>();
        public void Update(GameWindow window, float deltaTime)
        {

        }
        public void HandleInput(GameWindow window)
        {
            // キーボード入力の処理（必要に応じて）
        }

        public void HandleResize(GameWindow window)
        {
            // リサイズ後の処理が必要な場合はここに実装します
            window.OnWindowResized();
        }

        public void HandleWindowMessage(GameWindow window, Message m)
        {
            switch (m.Msg)
            {
                case 0x0201: // WM_LBUTTONDOWN
                    isResizing = true;
                    lastMousePos = window.PointToClient(Cursor.Position);
                    originalSize = window.Size;
                    break;

                case 0x0202: // WM_LBUTTONUP
                    isResizing = false;
                    ResizeEffect.UpdateScale(new SizeF(1.0f, 1.0f));
                    //WindowManager.Instance.CheckPotentialParentWindow(window);
                    break;

                case 0x0200: // WM_MOUSEMOVE
                    if (isResizing)
                    {
                        // 現在のマウス位置を取得
                        Point currentMousePos = window.PointToClient(Cursor.Position);

                        // マウスの移動量を計算
                        int dx = currentMousePos.X - lastMousePos.X;
                        int dy = currentMousePos.Y - lastMousePos.Y;

                        // 新しいサイズを計算
                        Size newSize = new Size(
                            originalSize.Width + dx,
                            originalSize.Height + dy
                        );

                        // 最小サイズ制限を適用
                        newSize.Width = Math.Max(newSize.Width, window.MinimumSize.Width);
                        newSize.Height = Math.Max(newSize.Height, window.MinimumSize.Height);

                        // 新しいスケールを計算
                        SizeF scale = new SizeF(
                            (float)newSize.Width / window.OriginalSize.Width,
                            (float)newSize.Height / window.OriginalSize.Height
                        );

                        // まず効果を更新
                        ResizeEffect.UpdateScale(scale);

                        // ウィンドウのサイズを更新
                        window.Size = newSize;

                        // 含まれているターゲットに効果を適用
                        var containedTargets = WindowManager.Instance.GetContainedTargets(window);
                        foreach (var target in containedTargets)
                        {
                            if (target.CanReceiveEffect(ResizeEffect))
                            {
                                target.ApplyEffect(ResizeEffect);
                            }
                        }

                        // リサイズイベントを発火
                        window.OnWindowResized();
                        WindowManager.Instance.CheckChildRelationBreak(window);
                        // デバッグ情報
                        Console.WriteLine($"Resizing - New Size: {newSize}, Scale: {scale}, Targets: {containedTargets.Count}");
                    }
                    break;
            }
        }

        public void UpdateCursor(GameWindow window, Point clientMousePos)
        {
            window.Cursor = Cursors.SizeNWSE;
        }
    }

    public class MovableWindowStrategy : IWindowStrategy
    {
        private bool isDragging = false;
        private Point lastMousePos;
        private const int WM_NCHITTEST = 0x84;
        private const int HTCAPTION = 2;
        public MovementEffect MovementEffect { get; } = new MovementEffect();
        private Dictionary<IEffectTarget, Point> initialRelativePositions = new Dictionary<IEffectTarget, Point>();
        public void Update(GameWindow window, float deltaTime)
        {
           
        }

        public void HandleInput(GameWindow window)
        {
            // キーボード入力の処理（必要に応じて）
        }

        public void HandleWindowMessage(GameWindow window, Message m)
        {
            switch (m.Msg)
            {
                case WM_NCHITTEST:
                    m.Result = (IntPtr)HTCAPTION;
                    break;

                case 0x0201: // WM_LBUTTONDOWN
                    isDragging = true;
                    lastMousePos = window.PointToClient(Cursor.Position);
                    break;

                case 0x0202: // WM_LBUTTONUP
                    isDragging = false;
                    MovementEffect.UpdateMovement(Vector2.Zero);
                    //WindowManager.Instance.CheckPotentialParentWindow(window);
                    break;

                case 0x0200: // WM_MOUSEMOVE
                    if (isDragging)
                    {
                        Point currentMousePos = window.PointToClient(Cursor.Position);
                        int dx = currentMousePos.X - lastMousePos.X;
                        int dy = currentMousePos.Y - lastMousePos.Y;
                        Vector2 movement = new Vector2(dx, dy);

                        MovementEffect.UpdateMovement(movement);
                        var containedTargets = WindowManager.Instance.GetContainedTargets(window);
                        // 現在の子要素に効果を適用
                        foreach (var target in containedTargets)
                        {
                            if (target.CanReceiveEffect(MovementEffect))
                            {
                                target.ApplyEffect(MovementEffect);
                            }
                        }

                        window.Location = new Point(
                            window.Location.X + (int)movement.X,
                            window.Location.Y + (int)movement.Y
                        );
                        window.OnWindowMoved();
                        // プレイヤーの位置を更新
                        Player? player = WindowManager.Instance.GetPlayerInWindow(window);
                        if (player != null) player.ConstrainToWindow(window);
                        WindowManager.Instance.CheckChildRelationBreak(window);
                    }
                    break;
            }
        }
        public void UpdateCursor(GameWindow window, Point clientMousePos)
        {
            // ウィンドウ全体で移動カーソルを表示
            window.Cursor = Cursors.SizeAll;
        }
        public void HandleResize(GameWindow window) { }
    }

    public class DeletableWindowStrategy : IWindowStrategy
    {
        public bool IsMinimized { get; private set; }

        public void Update(GameWindow window, float deltaTime)
        {
            // 既存の更新ロジック
        }

        public void HandleInput(GameWindow window)
        {
            if (Input.IsKeyDown(Keys.Delete))
            {               
                // 削除前に含まれているターゲットを解放
                foreach (var target in WindowManager.Instance.GetContainedTargets(window))
                {
                    if (target is Player player)
                    {
                        player.SetCurrentWindow(null);
                    }
                }

                window.Close();
                window.NotifyObservers(WindowChangeType.Deleted);
            }
        }

        public void HandleResize(GameWindow window) { }

        public void HandleMinimize(GameWindow window)
        {
            IsMinimized = true;
            // ここで最小化に関する追加の処理を行うことができます
        }

        public void HandleRestore(GameWindow window)
        {
            IsMinimized = false;
            // ここで復元に関する追加の処理を行うことができます
        }
        public void UpdateCursor(GameWindow window, Point clientMousePos)
        {
            window.Cursor = Cursors.Default;
        }
    }
}