using UnityEngine;

namespace Volley.Sim
{
    public static class PlayerPhysics
    {
        private const float SlowRadius = 1.2f;

        /// <summary>Núcleo: aproxima a velocidade atual da desejada, respeitando a aceleração.</summary>
        private static PlayerState Integrate(PlayerState p, PlayerAttributes a, Vector3 desired, float dt)
        {
            Vector3 dv = desired - p.Velocity;
            float maxDv = a.Acceleration * dt;
            if (dv.magnitude > maxDv) dv = dv.normalized * maxDv;

            p.Velocity += dv;
            p.Position += p.Velocity * dt;
            p.Position.y = 0f;

            return p;
        }

        /// <summary>
        /// Modo IA: corre até um ponto e freia ao chegar.
        /// </summary>
        public static PlayerState StepToTarget(PlayerState p, PlayerAttributes a, Vector3 target, float dt)
        {
            Vector3 flat = new Vector3(target.x - p.Position.x, 0f, target.z - p.Position.z);
            float dist = flat.magnitude;

            Vector3 desired = Vector3.zero;
            if (dist > 0.02f)
            {
                desired = flat / dist * a.MaxSpeed;
                if (dist < SlowRadius) desired *= dist / SlowRadius;
            }

            return Integrate(p, a, desired, dt);
        }

        /// <summary>
        /// Modo humado: direção vinda do teclado.
        /// </summary>
        public static PlayerState StepDirect(PlayerState p, PlayerAttributes a, Vector2 input, float dt)
        {
            Vector3 desired = new Vector3(input.x, 0f, input.y) * a.MaxSpeed;
            return Integrate(p, a, desired, dt);
        }

        /// <summary>A bola está ao alcance deste jogador agora?</summary>
        public static bool CanReach(PlayerState p, PlayerAttributes a, Vector3 ball)
        {
            Vector2 flat = new Vector2(ball.x - p.Position.x, ball.z - p.Position.z);
            return flat.magnitude <= a.Reach && ball.y <= a.ReachHeight;
        }
    }
}