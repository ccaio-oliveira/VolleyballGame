using UnityEngine;

namespace Volley.Sim
{
    /// <summary>Integração da bola. Tudo aqui é função pura.</summary>
    public static class BallPhysics
    {
        // k = (0.5 * densidade_ar * Cd * area) / massa
        public const float K = 0.0314f;

        public static readonly Vector3 Gravity = new Vector3(0f, -9.81f, 0f);

        /// <summary>Gravidade + arrasto quadrático do ar.</summary>
        public static Vector3 Acceleration(Vector3 velocity)
        {
            return Gravity - K * velocity.magnitude * velocity;
        }

        /// <summary>
        /// Avança a bola em dt segundos. Euler semi-implícito:
        /// velocidade PRIMEIRO, posição depois. Inverter faz o integrador ganhar energia e a bola sobe sozinha ao longo do tempo.
        /// </summary>
        public static BallState Step(BallState s, float dt)
        {
            s.Velocity += Acceleration(s.Velocity) * dt;
            s.Position += s.Velocity * dt;
            return s;
        }

        /// <summary>
        /// Simula a bola pra frente até ela cruzar a altura targetY descendo.
        /// Usa a MESMA Step() da simulação real, numa cópia do estado -
        /// por isso é impossível a predição divergir da bola.
        /// >/summary>
        public static bool PredictLanding(BallState s, float targetY, float dt, float maxTime, out Vector3 hit, out float time)
        {
            hit = Vector3.zero;
            time = 0f;

            int maxSteps = Mathf.CeilToInt(maxTime / dt);

            for (int i = 0; i < maxSteps; i++)
            {
                BallState prev = s;
                s = Step(s, dt);
                time += dt;

                // travessia descendente do plano Y = targetY
                if (prev.Position.y > targetY && s.Position.y <= targetY)
                {
                    float denom = prev.Position.y - s.Position.y;
                    float f = denom > 1e-6f ? (prev.Position.y - targetY) / denom : 0f;

                    hit = Vector3.Lerp(prev.Position, s.Position, f);
                    time = time - dt + f * dt;
                    return true;
                }
            }

            return false; // não cruzou dentro de maxTime
        }
    }
}