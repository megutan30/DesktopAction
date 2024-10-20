using System.Drawing;

namespace MultiWindowActionGame
{
    public interface IPlayerState
    {
        void Update(Player player, float deltaTime);
        void Draw(Player player, Graphics g);
        void HandleInput(Player player);
    }

    public class NormalState : IPlayerState
    {
        public void Update(Player player, float deltaTime)
        {

        }

        public void Draw(Player player, Graphics g)
        {
            g.FillRectangle(Brushes.Blue, player.Bounds);
        }

        public void HandleInput(Player player)
        {
            if (Input.IsKeyDown(Keys.W) && player.IsGrounded)
            {
                player.Jump();
                player.SetState(new JumpingState());
            }
        }
    }

    public class JumpingState : IPlayerState
    {
        private float jumpTime = 0;
        private const float MaxJumpTime = 0.5f;

        public void Update(Player player, float deltaTime)
        {
            jumpTime += deltaTime;
            if (jumpTime >= MaxJumpTime || player.verticalVelocity > 0)
            {
                player.SetState(new FallingState());
            }
            player.Move(deltaTime);
            player.ApplyGravity(deltaTime);
        }

        public void Draw(Player player, Graphics g)
        {
            g.FillRectangle(Brushes.Green, player.Bounds);
        }

        public void HandleInput(Player player)
        {
            // ジャンプ中の入力処理（必要に応じて）
        }
    }

    public class FallingState : IPlayerState
    {
        public void Update(Player player, float deltaTime)
        {
            if (player.IsGrounded)
            {
                player.SetState(new NormalState());
            }
        }

        public void Draw(Player player, Graphics g)
        {
            g.FillRectangle(Brushes.Red, player.Bounds);
        }

        public void HandleInput(Player player)
        {
            // 落下中の入力処理（必要に応じて）
        }
    }
}