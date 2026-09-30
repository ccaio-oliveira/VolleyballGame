using UnityEngine;

namespace Volley.Sim
{
    /// <summary>Player movement: grounded acceleration and ballistic jumps. Pure functions.</summary>
    public static class PlayerPhysics
    {
        public const float Gravity = -9.81f;
        public const float JumpSpeed = 3.90f;   // ~78 cm high, ~0.80 s in the air

        private const float SlowRadius = 1.2f;  // starts braking at this distance from the target
        private const float NetMargin = 0.15f;

        /// <summary>AI movement: runs to a point and brakes on arrival (arrive, not seek).</summary>
        public static PlayerState StepToTarget(PlayerState p, PlayerAttributes a, Vector3 target, float dt)
        {
            Vector3 flat = new Vector3(target.x - p.Position.x, 0f, target.z - p.Position.z);
            float distance = flat.magnitude;

            Vector3 desired = Vector3.zero;
            if (distance > 0.02f)
            {
                desired = flat / distance * a.MaxSpeed;

                // proportional braking near the target: without it the player orbits
                if (distance < SlowRadius) desired *= distance / SlowRadius;
            }

            return Integrate(p, a, desired, dt);
        }

        /// <summary>Human movement: direction from the stick, already in world space.</summary>
        public static PlayerState StepDirect(PlayerState p, PlayerAttributes a, Vector2 input, float dt)
        {
            Vector3 desired = new Vector3(input.x, 0f, input.y) * a.MaxSpeed;
            return Integrate(p, a, desired, dt);
        }

        /// <summary>In the air: no control, but physics keeps running.</summary>
        public static PlayerState StepAirborne(PlayerState p, PlayerAttributes a, float dt)
            => Integrate(p, a, Vector3.zero, dt);

        /// <summary>
        /// Vertical impulse added to the velocity the player already had. That is why
        /// jumping while running carries you along — and the stick at takeoff tilts it further.
        /// </summary>
        public static PlayerState Jump(PlayerState p, Vector2 direction, float directionalBoost)
        {
            if (p.Position.y > 0.001f) return p;   // already airborne

            p.Velocity.y = JumpSpeed;
            p.Velocity.x += direction.x * directionalBoost;
            p.Velocity.z += direction.y * directionalBoost;

            return p;
        }

        private static PlayerState Integrate(PlayerState p, PlayerAttributes a, Vector3 desired, float dt)
        {
            // ---- airborne: pure ballistics, no control ----
            if (p.Position.y > 0f || p.Velocity.y > 0f)
            {
                p.Velocity.y += Gravity * dt;
                p.Position += p.Velocity * dt;

                if (p.Position.y <= 0f)
                {
                    p.Position.y = 0f;
                    p.Velocity.y = 0f;   // landed
                }

                return ClampToOwnSide(p);
            }

            // ---- grounded: horizontal velocity approaches the desired one, limited by acceleration ----
            Vector3 dv = desired - new Vector3(p.Velocity.x, 0f, p.Velocity.z);
            float maxDv = a.Acceleration * dt;
            if (dv.magnitude > maxDv) dv = dv.normalized * maxDv;

            p.Velocity += dv;
            p.Position += p.Velocity * dt;
            p.Position.y = 0f;

            return ClampToOwnSide(p);
        }

        /// <summary>Nobody crosses the net, not even in the air.</summary>
        private static PlayerState ClampToOwnSide(PlayerState p)
        {
            p.Position.z = p.Side < 0
                ? Mathf.Min(p.Position.z, -NetMargin)
                : Mathf.Max(p.Position.z, NetMargin);

            return p;
        }
    }
}
