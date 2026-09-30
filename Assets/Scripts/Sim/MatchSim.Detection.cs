using UnityEngine;

namespace Volley.Sim
{
    /// <summary>
    /// Crossing detection: net, ceiling, floor and block. Every test compares the previous
    /// and current ball state, so a fast ball can never tunnel through a thin plane.
    /// </summary>
    public partial class MatchSim
    {
        /// <returns>True when the rally ended on this crossing.</returns>
        private bool DetectNetCrossing()
        {
            float z0 = _previousBall.Position.z;
            float z1 = Ball.Position.z;

            bool crossed = (z0 < 0f && z1 >= 0f) || (z0 > 0f && z1 <= 0f);
            if (!crossed) return false;

            float t = -z0 / (z1 - z0);
            Vector3 crossing = Vector3.Lerp(_previousBall.Position, Ball.Position, t);

            if (crossing.y < NetHeight)
            {
                EndRally(-Rally.LastTouchSide, $"na rede (y={crossing.y:F2})");
                return true;
            }

            if (Mathf.Abs(crossing.x) > Court.HalfWidth)
            {
                EndRally(-Rally.LastTouchSide, $"fora das antenas (x={crossing.x:F2})");
                return true;
            }

            int defendingSide = Court.SideOf(z1);

            // a serve can never be blocked
            int blocker = Rally.ServeInFlight ? -1 : FindBlocker(crossing, defendingSide);

            if (blocker >= 0)
            {
                ApplyBlock(blocker, crossing, defendingSide);
                return false;
            }

            Rally.OnNetCrossed(defendingSide);
            OnLog?.Invoke($"cruzou a rede a {crossing.y:F2} m -> " + $"posse do lado {SideName(Rally.TouchingSide)}");
            return false;
        }

        private bool DetectCeiling()
        {
            if (_previousBall.Position.y >= Court.Ceiling || Ball.Position.y < Court.Ceiling) return false;

            EndRally(-Rally.LastTouchSide, "bateu no teto");
            return true;
        }

        private bool DetectGround()
        {
            float y0 = _previousBall.Position.y;
            float y1 = Ball.Position.y;

            if (!(y0 > Court.BallRadius && y1 <= Court.BallRadius)) return false;

            float denominator = y0 - y1;
            float t = denominator > 1e-6f ? (y0 - Court.BallRadius) / denominator : 0f;
            Vector3 landing = Vector3.Lerp(_previousBall.Position, Ball.Position, t);

            OnEvent?.Invoke(SimEventKind.BallLanded, landing, Mathf.Clamp01(Ball.Velocity.magnitude / 22f));

            if (Court.IsInBounds(landing))
                EndRally(-Court.SideOf(landing.z), $"quicou no lado {SideName(Court.SideOf(landing.z))} " + $"em x={landing.x:F2} z={landing.z:F2}");
            else
                EndRally(-Rally.LastTouchSide, $"fora em x={landing.x:F2} z={landing.z:F2}");

            return true;
        }

        // ---------- block ----------

        /// <summary>
        /// A blocker touches the ball when it crosses the net inside the band his hands
        /// occupy — which only reaches above the tape if he actually jumped.
        /// </summary>
        private int FindBlocker(Vector3 crossing, int defendingSide)
        {
            int teamBase = TeamBase(defendingSide);

            for (int k = 0; k < 6; k++)
            {
                int i = teamBase + k;
                if (Players[i].BlockTimer <= 0f) continue;
                if (!IsFront(i)) continue;

                float handTop = Players[i].Position.y + Attrs[i].ReachHeight;
                float handBottom = Mathf.Max(NetHeight, handTop - BlockHandSpan);

                bool insideLateral = Mathf.Abs(crossing.x - Players[i].Position.x) <= BlockHalfWidth;
                bool insideHeight = crossing.y >= handBottom && crossing.y <= handTop;

                if (insideLateral && insideHeight) return i;
            }

            return -1;
        }

        private void ApplyBlock(int i, Vector3 crossing, int defendingSide)
        {
            int attackingSide = -defendingSide;

            // margin 1 = ball crossed at the base of the hands -> stuff block
            // margin 0 = ball crossed at the fingertips      -> deflection
            float handTop = Players[i].Position.y + Attrs[i].ReachHeight;
            float margin = (handTop - crossing.y) / BlockHandSpan;

            Rally.OnBlockTouch(i, defendingSide);

            float speed = Ball.Velocity.magnitude;
            Vector3 position = crossing;

            OnEvent?.Invoke(SimEventKind.Block, crossing, Mathf.Clamp01(speed / 25f));

            if (margin > 0.55f)
            {
                // stuff block: drives straight down on the attacker's side.
                // The 5 cm offset keeps the next crossing test alive (zero is neither side).
                position.z = 0.05f * attackingSide;
                Vector3 velocity = new Vector3(Ball.Velocity.x * 0.25f, -speed * 0.30f, -Ball.Velocity.z * 0.28f);

                SetBallTrajectory(position, velocity);
                Rally.OnNetCrossed(attackingSide);

                OnLog?.Invoke($"BLOQUEIO #{i} margem={margin:F2} " + $"a bola volta pro lado {SideName(attackingSide)}");
            }
            else
            {
                // deflection: goes over, slow and high — a playable ball
                position.z = 0.05f * defendingSide;
                Vector3 velocity = Ball.Velocity * 0.42f;
                velocity.y = Mathf.Abs(velocity.y) + 1.6f;

                SetBallTrajectory(position, velocity);
                Rally.OnNetCrossed(defendingSide);

                OnLog?.Invoke($"raspão no bloqueio #{i} margem={margin:F2} " + $"lado {SideName(defendingSide)} com 3 toques");
            }
        }
    }
}
