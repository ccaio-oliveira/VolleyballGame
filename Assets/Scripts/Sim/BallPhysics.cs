using log4net.Filter;
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

        /// <summary>
        /// Balística inversa: dado o ponto de contato, o alvo e o ângulo de saída, acha por
        /// busca binária a velocidade que faz a bola cair no alvo.
        /// <summary>
        public static bool SolveLaunch(Vector3 from, Vector3 target, float angleDeg, float dt, out Vector3 velocity, float minSpeed = 1f, float maxSpeed = 40f, int interations = 20)
        {
            velocity = Vector3.zero;

            Vector3 flat = new Vector3(target.x - from.x, 0f, target.z - from.z);
            float wanted = flat.magnitude;
            if(wanted < 1e-4f) return false;

            Vector3 dir = flat /wanted;
            float rad = angleDeg * Mathf.Deg2Rad;

            Vector3 unit = new Vector3(dir.x * Mathf.Cos(rad), Mathf.Sin(rad), dir.z * Mathf.Cos(rad));

            // valida o bracket antes de buscar
            if (RangeFor(from, unit, maxSpeed, target.y, dt) < wanted) return false;
            if (RangeFor(from, unit, minSpeed, target.y, dt) > wanted) return false;

            float lo = minSpeed, hi = maxSpeed;
            for (int i = 0; i < interations; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (RangeFor(from, unit, mid, target.y, dt) < wanted) lo = mid;
                else hi = mid;
            }

            velocity = unit * (0.5f * (lo + hi));
            return true;
        }

        /// <summary>Altura em que a bola cruza o plano da rede (z = 0). NaN se não cruzar.</summary>
        public static float NetCrossHeight(BallState s, float dt, float maxTime = 8f)
        {
            int maxSteps = Mathf.CeilToInt(maxTime / dt);

            for (int i = 0; i < maxSteps; i++)
            {
                BallState prev = s;
                s = Step(s, dt);

                float z0 = prev.Position.z, z1 = s.Position.z;
                bool crossed = (z0 < 0f && z1 >= 0f) || (z0 > 0f && z1 <= 0f);

                if (crossed)
                {
                    float f = -z0 / (z1 - z0);
                    return Mathf.Lerp(prev.Position.y, s.Position.y, f);
                }

                if (s.Position.y <= Court.BallRadius) break;
            }

            return float.NaN;
        }

        /// <summary>
        /// Acha a trajetória mais esticada (menor ângulo) que atinge o alvo e ainda cruza
        /// a rede com a folga pedida. Bola rasa é bola rápida.
        /// </summary>
        public static bool SolveFlattestLegal(Vector3 from, Vector3 target, float netHeight, float clearance, float dt, out Vector3 velocity, float minAngle = 5f, float maxAngle = 60f, float angleStep = 1f)
        {
            velocity = Vector3.zero;

            int steps = Mathf.CeilToInt((maxAngle - minAngle) / angleStep);

            for (int i = 0; i <= steps; i++)
            {
                float a = minAngle + i * angleStep;

                if (!SolveLaunch(from, target, a, dt, out Vector3 v)) continue;

                float yNet = NetCrossHeight(new BallState(from, v), dt);
                if (float.IsNaN(yNet)) continue;

                if (yNet >= netHeight + clearance)
                {
                    velocity = v;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Alcance horizontal de um lançamento, usando a simulação real.</summary>
        private static float RangeFor(Vector3 from, Vector3 unit, float speed, float targetY, float dt)
        {
            BallState s = new BallState(from, unit * speed);

            if (PredictLanding(s, targetY, dt, 8f, out Vector3 hit, out _)) return new Vector2(hit.x - from.x, hit.z - from.z).magnitude;

            return Apex(from, unit * speed, dt) < targetY ? 0f : float.MaxValue;
        }

        /// <summary>Altura máxima que a bola atinge neste lançamento.</summary>
        private static float Apex(Vector3 from, Vector3 velocity, float dt)
        {
            BallState s = new BallState(from, velocity);
            float peak = from.y;

            for (int i = 0; i < 1200; i++)
            {
                s = Step(s, dt);
                if (s.Position.y <= peak) break;

                peak = s.Position.y;
            }

            return peak;
        }
    }
}