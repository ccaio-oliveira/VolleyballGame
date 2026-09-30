using UnityEngine;

namespace Volley.Sim
{
    /// <summary>Ball integration, prediction and inverse ballistics. Everything here is a pure function.</summary>
    public static class BallPhysics
    {
        /// <summary>Drag constant: k = (0.5 · air density · Cd · area) / mass.</summary>
        public const float DragK = 0.0314f;

        public static readonly Vector3 Gravity = new Vector3(0f, -9.81f, 0f);

        /// <summary>Gravity plus quadratic air drag.</summary>
        public static Vector3 Acceleration(Vector3 velocity)
        {
            return Gravity - DragK * velocity.magnitude * velocity;
        }

        /// <summary>
        /// Advances the ball by dt seconds with semi-implicit Euler: velocity FIRST, then
        /// position. Reversing the order makes the integrator gain energy over time.
        /// </summary>
        public static BallState Step(BallState s, float dt)
        {
            s.Velocity += Acceleration(s.Velocity) * dt;
            s.Position += s.Velocity * dt;
            return s;
        }

        // ================= prediction =================

        /// <summary>
        /// Simulates the ball forward until it crosses <paramref name="targetY"/> going down.
        /// Uses the SAME Step() as the real simulation on a copy of the state, so the
        /// prediction can never diverge from the ball.
        /// </summary>
        public static bool PredictLanding(BallState s, float targetY, float dt, float maxTime, out Vector3 hit, out float time)
        {
            hit = Vector3.zero;
            time = 0f;

            int maxSteps = Mathf.CeilToInt(maxTime / dt);

            for (int i = 0; i < maxSteps; i++)
            {
                BallState previous = s;
                s = Step(s, dt);
                time += dt;

                // downward crossing of the plane y = targetY
                if (previous.Position.y > targetY && s.Position.y <= targetY)
                {
                    float denominator = previous.Position.y - s.Position.y;
                    float t = denominator > 1e-6f ? (previous.Position.y - targetY) / denominator : 0f;

                    hit = Vector3.Lerp(previous.Position, s.Position, t);
                    time = time - dt + t * dt;
                    return true;
                }
            }

            return false;   // did not cross within maxTime
        }

        /// <summary>Where and when the ball crosses the net plane (z = 0).</summary>
        public static bool PredictNetCross(BallState s, float dt, out Vector3 point, out float time, float maxTime = 4f)
        {
            point = Vector3.zero;
            time = 0f;

            int maxSteps = Mathf.CeilToInt(maxTime / dt);

            for (int i = 0; i < maxSteps; i++)
            {
                BallState previous = s;
                s = Step(s, dt);
                time += dt;

                float z0 = previous.Position.z;
                float z1 = s.Position.z;
                bool crossed = (z0 < 0f && z1 >= 0f) || (z0 > 0f && z1 <= 0f);

                if (crossed)
                {
                    float t = -z0 / (z1 - z0);
                    point = Vector3.Lerp(previous.Position, s.Position, t);
                    time = time - dt + t * dt;
                    return true;
                }

                if (s.Position.y <= Court.BallRadius) break;
            }

            return false;
        }

        /// <summary>Height at which the ball crosses the net plane; NaN if it never does.</summary>
        public static float NetCrossHeight(BallState s, float dt, float maxTime = 8f)
        {
            return PredictNetCross(s, dt, out Vector3 point, out _, maxTime) ? point.y : float.NaN;
        }

        // ================= inverse ballistics =================

        /// <summary>
        /// Given the contact point, the target and the launch angle, binary-searches the
        /// speed that makes the ball land on the target.
        /// </summary>
        public static bool SolveLaunch(Vector3 from, Vector3 target, float angleDeg, float dt, out Vector3 velocity,
                                       float minSpeed = 1f, float maxSpeed = 40f, int iterations = 20)
        {
            velocity = Vector3.zero;

            Vector3 flat = new Vector3(target.x - from.x, 0f, target.z - from.z);
            float wanted = flat.magnitude;
            if (wanted < 1e-4f) return false;

            Vector3 direction = flat / wanted;
            float rad = angleDeg * Mathf.Deg2Rad;

            Vector3 unit = new Vector3(direction.x * Mathf.Cos(rad), Mathf.Sin(rad), direction.z * Mathf.Cos(rad));

            // validate the bracket before searching: a binary search without a valid
            // bracket converges to the edge and returns garbage that looks like an answer
            if (RangeFor(from, unit, maxSpeed, target.y, dt) < wanted) return false;
            if (RangeFor(from, unit, minSpeed, target.y, dt) > wanted) return false;

            float lo = minSpeed, hi = maxSpeed;
            for (int i = 0; i < iterations; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (RangeFor(from, unit, mid, target.y, dt) < wanted) lo = mid;
                else hi = mid;
            }

            velocity = unit * (0.5f * (lo + hi));
            return true;
        }

        /// <summary>
        /// Finds the flattest trajectory (smallest angle) that reaches the target and still
        /// crosses the net with the requested clearance. A flat ball is a fast ball.
        /// </summary>
        public static bool SolveFlattestLegal(Vector3 from, Vector3 target, float netHeight, float clearance, float dt,
                                              out Vector3 velocity, float minAngle = 5f, float maxAngle = 60f, float angleStep = 1f)
        {
            velocity = Vector3.zero;

            int steps = Mathf.CeilToInt((maxAngle - minAngle) / angleStep);

            for (int i = 0; i <= steps; i++)
            {
                float angle = minAngle + i * angleStep;   // derived from the index, never accumulated

                if (!SolveLaunch(from, target, angle, dt, out Vector3 candidate)) continue;

                float netY = NetCrossHeight(new BallState(from, candidate), dt);
                if (float.IsNaN(netY)) continue;

                if (netY >= netHeight + clearance)
                {
                    velocity = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Horizontal range of a launch, measured with the real simulation.</summary>
        private static float RangeFor(Vector3 from, Vector3 unit, float speed, float targetY, float dt)
        {
            BallState s = new BallState(from, unit * speed);

            if (PredictLanding(s, targetY, dt, 8f, out Vector3 hit, out _))
                return new Vector2(hit.x - from.x, hit.z - from.z).magnitude;

            // It never crossed targetY going down — for two OPPOSITE reasons. Without
            // telling them apart, the binary search walks the wrong way.
            return Apex(from, unit * speed, dt) < targetY
                ? 0f                // too weak: never rose that high
                : float.MaxValue;   // too strong: still in the air
        }

        /// <summary>Maximum height the ball reaches on this launch.</summary>
        private static float Apex(Vector3 from, Vector3 velocity, float dt)
        {
            BallState s = new BallState(from, velocity);
            float peak = from.y;

            for (int i = 0; i < 1200; i++)
            {
                s = Step(s, dt);
                if (s.Position.y <= peak) break;   // started descending
                peak = s.Position.y;
            }

            return peak;
        }
    }
}
