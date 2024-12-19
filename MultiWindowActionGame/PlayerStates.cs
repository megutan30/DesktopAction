using System.Drawing;

namespace MultiWindowActionGame
{
    public interface IPlayerState
    {
        void Update(PlayerForm player, float deltaTime);
        void Draw(PlayerForm player, Graphics g);
        void HandleInput(PlayerForm player);
        bool ShouldCheckGround { get; }
    }

    public class NormalState : IPlayerState
    {
        public bool ShouldCheckGround => true;
        public void Update(PlayerForm player, float deltaTime)
        {
            if (!player.IsGrounded)
            {
                player.SetState(new FallingState());
            }
        }

        public void Draw(PlayerForm player, Graphics g)
        {
            if (MainGame.IsDebugMode)
            {
                g.FillRectangle(Brushes.Blue, player.ClientRectangle);
            }
            else
            {
                g.FillRectangle(Brushes.Blue, player.ClientRectangle);
            }
        }

        public void HandleInput(PlayerForm player)
        {
        }
    }

    public class JumpingState : IPlayerState
    {
        public bool ShouldCheckGround => false;
        private float jumpTime = 0;
        private const float MaxJumpTime = 0.5f;

        public void Update(PlayerForm player, float deltaTime)
        {
            if (player.VerticalVelocity > 0)
            {
                player.SetState(new FallingState());
            }
            else if (player.IsGrounded)
            {
                player.SetState(new NormalState());
            }
        }

        public void Draw(PlayerForm player, Graphics g)
        {
            if (MainGame.IsDebugMode)
            {
                g.FillRectangle(Brushes.Green, player.ClientRectangle);
            }
            else
            {
                g.FillRectangle(Brushes.Blue, player.ClientRectangle);
            }
        }

        public void HandleInput(PlayerForm player)
        {
            // ジャンプ中の入力処理（必要に応じて）
        }
    }

    public class FallingState : IPlayerState
    {
        public bool ShouldCheckGround => true;
        public void Update(PlayerForm player, float deltaTime)
        {
            if (player.IsGrounded)
            {
                player.SetState(new NormalState());
            }
        }

        public void Draw(PlayerForm player, Graphics g)
        {
            if (MainGame.IsDebugMode)
            {
                g.FillRectangle(Brushes.Red, player.ClientRectangle);
            }
            else
            {
                g.FillRectangle(Brushes.Blue, player.ClientRectangle);
            }
        }

        public void HandleInput(PlayerForm player)
        {
            // 落下中の入力処理（必要に応じて）
        }
    }
}