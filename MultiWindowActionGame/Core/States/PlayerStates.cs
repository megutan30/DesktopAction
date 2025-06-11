// Core/States/PlayerStates.cs
using MultiWindowActionGame.Core.Interfaces;
using MultiWindowActionGame.Core.Constants;

namespace MultiWindowActionGame.Core.States
{
    /// <summary>
    /// プレイヤー状態の基底クラス
    /// </summary>
    public abstract class BasePlayerState : IPlayerState
    {
        public abstract string StateName { get; }
        public abstract bool ShouldCheckGround { get; }

        public virtual void Update(IPlayer player, float deltaTime)
        {
            // 基本的な状態遷移ロジック
            CheckStateTransitions(player, deltaTime);
        }

        public virtual void Draw(IPlayer player, Graphics graphics)
        {
            if (MainGame.IsDebugMode)
            {
                DrawDebugInfo(player, graphics);
            }
        }

        public virtual void HandleInput(IPlayer player)
        {
            // 基本入力処理（各状態でオーバーライド可能）
        }

        public virtual void OnEnter(IPlayer player)
        {
            System.Diagnostics.Debug.WriteLine($"Player entered state: {StateName}");
        }

        public virtual void OnExit(IPlayer player)
        {
            System.Diagnostics.Debug.WriteLine($"Player exited state: {StateName}");
        }

        /// <summary>
        /// 状態遷移のチェック（派生クラスでオーバーライド）
        /// </summary>
        protected virtual void CheckStateTransitions(IPlayer player, float deltaTime)
        {
            // デフォルトでは何もしない
        }

        /// <summary>
        /// デバッグ情報の描画
        /// </summary>
        protected virtual void DrawDebugInfo(IPlayer player, Graphics graphics)
        {
            var stateColor = GetStateColor();
            using (var brush = new SolidBrush(stateColor))
            {
                graphics.FillRectangle(brush, 0, 0, player.Size.Width, player.Size.Height);
            }

            // 状態名をテキストで表示
            using (var font = new Font("Arial", 8))
            using (var textBrush = new SolidBrush(Color.White))
            {
                graphics.DrawString(StateName, font, textBrush, 2, 2);
            }
        }

        /// <summary>
        /// 状態に対応するデバッグ色を取得
        /// </summary>
        protected abstract Color GetStateColor();
    }

    /// <summary>
    /// 通常状態 - 地面に立っている状態
    /// </summary>
    public class NormalState : BasePlayerState
    {
        public override string StateName => "Normal";
        public override bool ShouldCheckGround => true;

        protected override void CheckStateTransitions(IPlayer player, float deltaTime)
        {
            // 空中にいる場合は落下状態に移行
            if (!player.IsGrounded)
            {
                player.ChangeState(new FallingState());
            }
        }

        public override void HandleInput(IPlayer player)
        {
            // ジャンプ入力は Player クラス側で処理される
        }

        protected override Color GetStateColor()
        {
            return Color.FromArgb(128, Color.Blue);
        }
    }

    /// <summary>
    /// ジャンプ状態 - ジャンプの上昇中
    /// </summary>
    public class JumpingState : BasePlayerState
    {
        private float _jumpTime = 0f;
        private const float MAX_JUMP_TIME = 0.5f;

        public override string StateName => "Jumping";
        public override bool ShouldCheckGround => false; // ジャンプ中は地面チェックしない

        public override void Update(IPlayer player, float deltaTime)
        {
            _jumpTime += deltaTime;

            // ジャンプ時間経過または下降開始で落下状態に移行
            if (_jumpTime >= MAX_JUMP_TIME || player.VerticalVelocity > 0)
            {
                player.ChangeState(new FallingState());
            }
            // 意外にも地面に着いた場合（低い天井など）
            else if (player.IsGrounded)
            {
                player.ChangeState(new NormalState());
            }
        }

        public override void HandleInput(IPlayer player)
        {
            // ジャンプ中は追加の入力処理なし
            // 可変ジャンプを実装する場合はここで処理
        }

        public override void OnEnter(IPlayer player)
        {
            base.OnEnter(player);
            _jumpTime = 0f;
        }

        protected override Color GetStateColor()
        {
            return Color.FromArgb(128, Color.Green);
        }

        protected override void DrawDebugInfo(IPlayer player, Graphics graphics)
        {
            base.DrawDebugInfo(player, graphics);

            // ジャンプ時間の表示
            using (var font = new Font("Arial", 6))
            using (var brush = new SolidBrush(Color.Yellow))
            {
                var jumpPercent = (_jumpTime / MAX_JUMP_TIME) * 100;
                graphics.DrawString($"Jump: {jumpPercent:F0}%", font, brush, 2, 12);
            }
        }
    }

    /// <summary>
    /// 落下状態 - 空中で下降中
    /// </summary>
    public class FallingState : BasePlayerState
    {
        private float _fallTime = 0f;
        private float _fallDistance = 0f;
        private float _previousY;

        public override string StateName => "Falling";
        public override bool ShouldCheckGround => true;

        public override void Update(IPlayer player, float deltaTime)
        {
            _fallTime += deltaTime;
            
            // 落下距離を計算
            var currentY = player.Position.Y;
            if (_previousY != 0)
            {
                var deltaY = currentY - _previousY;
                if (deltaY > 0) // 下向きの移動のみカウント
                {
                    _fallDistance += deltaY;
                }
            }
            _previousY = currentY;

            base.Update(player, deltaTime);
        }

        protected override void CheckStateTransitions(IPlayer player, float deltaTime)
        {
            // 地面に着いたら通常状態に移行
            if (player.IsGrounded)
            {
                player.ChangeState(new NormalState());
            }
        }

        public override void OnEnter(IPlayer player)
        {
            base.OnEnter(player);
            _fallTime = 0f;
            _fallDistance = 0f;
            _previousY = player.Position.Y;
        }

        protected override Color GetStateColor()
        {
            return Color.FromArgb(128, Color.Red);
        }

        protected override void DrawDebugInfo(IPlayer player, Graphics graphics)
        {
            base.DrawDebugInfo(player, graphics);

            // 落下情報の表示
            using (var font = new Font("Arial", 6))
            using (var brush = new SolidBrush(Color.Yellow))
            {
                graphics.DrawString($"Fall: {_fallTime:F1}s", font, brush, 2, 12);
                graphics.DrawString($"Dist: {_fallDistance:F0}px", font, brush, 2, 20);
                graphics.DrawString($"Vel: {player.VerticalVelocity:F0}", font, brush, 2, 28);
            }
        }
    }

    /// <summary>
    /// ダッシュ状態 - 高速移動中（将来の拡張用）
    /// </summary>
    public class DashingState : BasePlayerState
    {
        private float _dashTime = 0f;
        private float _dashDirection = 1f; // 1 = 右, -1 = 左
        private const float DASH_DURATION = 0.3f;
        private const float DASH_SPEED_MULTIPLIER = 2.5f;

        public override string StateName => "Dashing";
        public override bool ShouldCheckGround => true;

        public override void Update(IPlayer player, float deltaTime)
        {
            _dashTime += deltaTime;

            // ダッシュ終了
            if (_dashTime >= DASH_DURATION)
            {
                if (player.IsGrounded)
                {
                    player.ChangeState(new NormalState());
                }
                else
                {
                    player.ChangeState(new FallingState());
                }
            }

            base.Update(player, deltaTime);
        }

        public override void OnEnter(IPlayer player)
        {
            base.OnEnter(player);
            _dashTime = 0f;
            
            // 入力方向に基づいてダッシュ方向を決定
            var inputService = Program.ServiceContainer?.GetService<IInputService>();
            if (inputService != null)
            {
                if (GameConstants.Input.MOVEMENT_LEFT.Any(key => inputService.IsKeyDown(key)))
                {
                    _dashDirection = -1f;
                }
                else if (GameConstants.Input.MOVEMENT_RIGHT.Any(key => inputService.IsKeyDown(key)))
                {
                    _dashDirection = 1f;
                }
            }
        }

        protected override Color GetStateColor()
        {
            return Color.FromArgb(128, Color.Purple);
        }

        protected override void DrawDebugInfo(IPlayer player, Graphics graphics)
        {
            base.DrawDebugInfo(player, graphics);

            // ダッシュ情報の表示
            using (var font = new Font("Arial", 6))
            using (var brush = new SolidBrush(Color.White))
            {
                var remainingTime = DASH_DURATION - _dashTime;
                var direction = _dashDirection > 0 ? "→" : "←";
                graphics.DrawString($"Dash {direction}: {remainingTime:F1}s", font, brush, 2, 12);
            }

            // ダッシュエフェクトの描画
            DrawDashEffect(graphics, player);
        }

        private void DrawDashEffect(Graphics graphics, IPlayer player)
        {
            // ダッシュ軌跡の描画
            using (var pen = new Pen(Color.FromArgb(100, Color.Yellow), 3))
            {
                var startX = _dashDirection > 0 ? 0 : player.Size.Width;
                var endX = _dashDirection > 0 ? player.Size.Width : 0;
                
                for (int i = 0; i < 3; i++)
                {
                    var y = player.Size.Height / 2 + (i - 1) * 5;
                    graphics.DrawLine(pen, startX, y, endX, y);
                }
            }
        }
    }

    /// <summary>
    /// 壁張り付き状態（将来の拡張用）
    /// </summary>
    public class WallGrabState : BasePlayerState
    {
        private float _grabTime = 0f;
        private const float MAX_GRAB_TIME = 3.0f; // 最大3秒間壁につかまれる
        private bool _isOnLeftWall = false;

        public override string StateName => "WallGrab";
        public override bool ShouldCheckGround => false;

        public override void Update(IPlayer player, float deltaTime)
        {
            _grabTime += deltaTime;

            // 最大時間経過で落下
            if (_grabTime >= MAX_GRAB_TIME)
            {
                player.ChangeState(new FallingState());
                return;
            }

            // 壁から離れる入力で落下
            var inputService = Program.ServiceContainer?.GetService<IInputService>();
            if (inputService != null)
            {
                var pressingAwayFromWall = (_isOnLeftWall && 
                    GameConstants.Input.MOVEMENT_LEFT.Any(key => inputService.IsKeyDown(key))) ||
                    (!_isOnLeftWall && 
                    GameConstants.Input.MOVEMENT_RIGHT.Any(key => inputService.IsKeyDown(key)));

                if (pressingAwayFromWall)
                {
                    player.ChangeState(new FallingState());
                    return;
                }
            }

            base.Update(player, deltaTime);
        }

        public override void HandleInput(IPlayer player)
        {
            // 壁ジャンプの処理
            var inputService = Program.ServiceContainer?.GetService<IInputService>();
            if (inputService != null)
            {
                var jumpPressed = GameConstants.Input.JUMP_KEYS.Any(key => inputService.IsKeyPressed(key));
                if (jumpPressed)
                {
                    PerformWallJump(player);
                }
            }
        }

        public override void OnEnter(IPlayer player)
        {
            base.OnEnter(player);
            _grabTime = 0f;
            
            // 壁の方向を判定（簡略化）
            _isOnLeftWall = DetermineWallSide(player);
        }

        private bool DetermineWallSide(IPlayer player)
        {
            // 壁の判定ロジック（実装が必要）
            // 現在は簡単に画面の左右で判定
            var screenCenter = Program.mainForm?.ClientSize.Width / 2 ?? 400;
            return player.Position.X < screenCenter;
        }

        private void PerformWallJump(IPlayer player)
        {
            // 壁と反対方向にジャンプ
            var jumpDirection = _isOnLeftWall ? 1 : -1;
            
            // 壁ジャンプロジック（実装が必要）
            // 通常のジャンプとは異なる軌道
            
            player.ChangeState(new JumpingState());
        }

        protected override Color GetStateColor()
        {
            return Color.FromArgb(128, Color.Orange);
        }

        protected override void DrawDebugInfo(IPlayer player, Graphics graphics)
        {
            base.DrawDebugInfo(player, graphics);

            // 壁張り付き情報の表示
            using (var font = new Font("Arial", 6))
            using (var brush = new SolidBrush(Color.White))
            {
                var remainingTime = MAX_GRAB_TIME - _grabTime;
                var wallSide = _isOnLeftWall ? "Left" : "Right";
                graphics.DrawString($"Wall {wallSide}: {remainingTime:F1}s", font, brush, 2, 12);
            }

            // 壁張り付きエフェクトの描画
            DrawWallGrabEffect(graphics, player);
        }

        private void DrawWallGrabEffect(Graphics graphics, IPlayer player)
        {
            // 壁張り付きエフェクトの描画
            using (var pen = new Pen(Color.FromArgb(150, Color.Orange), 2))
            {
                var wallX = _isOnLeftWall ? 0 : player.Size.Width - 1;
                graphics.DrawLine(pen, wallX, 0, wallX, player.Size.Height);
                
                // 爪痕のような表現
                for (int i = 1; i < player.Size.Height; i += 8)
                {
                    var scratchX = _isOnLeftWall ? 2 : player.Size.Width - 3;
                    graphics.DrawLine(pen, scratchX, i, scratchX + 3, i + 3);
                }
            }
        }
    }

    /// <summary>
    /// 状態ファクトリー - 状態の生成を管理
    /// </summary>
    public static class PlayerStateFactory
    {
        /// <summary>
        /// 状態名から状態インスタンスを作成
        /// </summary>
        public static IPlayerState CreateState(string stateName)
        {
            return stateName.ToLower() switch
            {
                "normal" => new NormalState(),
                "jumping" => new JumpingState(),
                "falling" => new FallingState(),
                "dashing" => new DashingState(),
                "wallgrab" => new WallGrabState(),
                _ => new NormalState()
            };
        }

        /// <summary>
        /// 利用可能な状態名一覧を取得
        /// </summary>
        public static IReadOnlyList<string> GetAvailableStates()
        {
            return new List<string>
            {
                "Normal",
                "Jumping", 
                "Falling",
                "Dashing",
                "WallGrab"
            };
        }

        /// <summary>
        /// 状態の説明を取得
        /// </summary>
        public static string GetStateDescription(string stateName)
        {
            return stateName.ToLower() switch
            {
                "normal" => "地面に立っている通常状態",
                "jumping" => "ジャンプの上昇中",
                "falling" => "空中で落下中", 
                "dashing" => "高速移動中",
                "wallgrab" => "壁に張り付き中",
                _ => "不明な状態"
            };
        }
    }

    /// <summary>
    /// 状態遷移管理クラス
    /// </summary>
    public class PlayerStateManager
    {
        private readonly IPlayer _player;
        private readonly Stack<IPlayerState> _stateHistory = new();
        private const int MAX_HISTORY_SIZE = 10;

        public PlayerStateManager(IPlayer player)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        /// <summary>
        /// 状態を変更し、履歴を記録
        /// </summary>
        public void ChangeState(IPlayerState newState)
        {
            if (newState == null) return;

            // 現在の状態を履歴に追加
            if (_player.CurrentState != null)
            {
                _stateHistory.Push(_player.CurrentState);
                
                // 履歴サイズの制限
                while (_stateHistory.Count > MAX_HISTORY_SIZE)
                {
                    _stateHistory.TryPop(out _);
                }
            }

            _player.ChangeState(newState);
        }

        /// <summary>
        /// 前の状態に戻る
        /// </summary>
        public bool RevertToPreviousState()
        {
            if (_stateHistory.Count > 0)
            {
                var previousState = _stateHistory.Pop();
                _player.ChangeState(previousState);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 状態履歴を取得
        /// </summary>
        public IReadOnlyList<string> GetStateHistory()
        {
            return _stateHistory.Select(state => state.StateName).ToList();
        }

        /// <summary>
        /// 状態履歴をクリア
        /// </summary>
        public void ClearHistory()
        {
            _stateHistory.Clear();
        }

        /// <summary>
        /// 特定の状態への遷移が可能かチェック
        /// </summary>
        public bool CanTransitionTo(string targetStateName)
        {
            var currentStateName = _player.CurrentState?.StateName?.ToLower();
            var targetState = targetStateName.ToLower();

            // 基本的な遷移ルール
            return (currentStateName, targetState) switch
            {
                ("normal", "jumping") => _player.IsGrounded,
                ("normal", "dashing") => _player.IsGrounded,
                ("jumping", "falling") => true,
                ("falling", "normal") => _player.IsGrounded,
                ("falling", "wallgrab") => !_player.IsGrounded,
                ("wallgrab", "jumping") => true,
                ("wallgrab", "falling") => true,
                ("dashing", "normal") => _player.IsGrounded,
                ("dashing", "falling") => !_player.IsGrounded,
                _ => false
            };
        }
    }
}