using UnityEngine;

namespace Volley.Sim
{
    /// <summary>Human actions (jump, pass, attack, block) and touch resolution.</summary>
    public partial class MatchSim
    {
        // ---------- contact heights: the player's current height plus the reach of the action ----------

        private float HandHeight(int i) => Players[i].Position.y + Attrs[i].ReachHeight;

        private float PassHeight(int i) => Players[i].Position.y + 0.90f;

        private float OverheadHeight(int i) => Players[i].Position.y + 2.10f;

        // ================= human actions =================

        public void TryJump()
        {
            int i = HumanIndex;
            Players[i] = PlayerPhysics.Jump(Players[i], MoveInput, JumpDirectionBoost);
        }

        public void TryPass()
        {
            int i = ActiveIndex;
            if (!CanPlayBall(i)) return;

            ArmTouch(i, Rally.TouchCount == 1 ? OverheadHeight(i) : PassHeight(i), "passe");
        }

        public void TryAttack()
        {
            int i = ActiveIndex;
            if (!CanPlayBall(i)) return;

            ArmTouch(i, HandHeight(i), "ataque");
        }

        /// <summary>Raises the arms; if the player is on the floor, jumps as well.</summary>
        public void TryBlock()
        {
            int i = HumanIndex;
            if (!BallLive || Rally.ServeInFlight) return;
            if (Players[i].BlockTimer > 0f) return;

            Players[i] = PlayerPhysics.Jump(Players[i], MoveInput, JumpDirectionBoost);
            Players[i].BlockTimer = BlockDuration;
            OnLog?.Invoke($"[#{i}] salta");
        }

        private bool CanPlayBall(int i)
        {
            return BallLive
                && IsHuman[i]
                && i == HumanIndex
                && Rally.TouchingSide == Players[i].Side
                && Rally.TouchCount < 3;
        }

        /// <summary>Arms a touch at the requested height. It resolves when the ball gets there.</summary>
        private void ArmTouch(int i, float height, string label)
        {
            if (TouchArmed) return;

            if (!BallPhysics.PredictLanding(Ball, height, 1f / 60f, 6f, out _, out float error))
            {
                OnLog?.Invoke($"{label}: a bola não passa nessa altura");
                return;
            }

            float quality = TouchTiming.Quality(error, Window);
            if (quality <= 0f)
            {
                OnLog?.Invoke($"{label} fora da janela - {error:+0.00;-0.00} s");
                return;
            }

            TouchArmed = true;
            ArmedQuality = quality;
            ArmedTimer = Mathf.Max(0f, error);
            ArmedIndex = i;
            _armedAim = MoveInput;

            OnLog?.Invoke($"{label} {TouchTiming.Label(quality)} q={quality:F2} erro={error:+0.00;-0.00}s");
        }

        // ================= resolution =================

        /// <summary>Called when an AI player's HitTimer reaches zero: the hand meets the ball.</summary>
        private void ResolveAiTouch(int i)
        {
            Players[i].HitTimer = 0f;

            // measured now, with the ball at the contact height — not when the touch was armed
            float quality = AiQuality(i);

            if (quality <= 0f)
            {
                OnLog?.Invoke($"[#{i}] passou por baixo da bola");
                return;
            }

            ResolveTouch(i, quality);
        }

        /// <summary>
        /// Plays the ball for player <paramref name="i"/> with quality <paramref name="quality"/>.
        /// Pass, set or attack is decided by the touch count; the quality turns into trajectory error.
        /// </summary>
        private void ResolveTouch(int i, float quality)
        {
            if (Rally.TouchCount >= 3)
            {
                EndRally(-Players[i].Side, "quatro toques");
                return;
            }

            TouchArmed = false;
            _resolvedTouch = Rally.TouchCount;
            _resolvedSide = Rally.TouchingSide;

            Vector2 flat = new Vector2(Ball.Position.x - Players[i].Position.x, Ball.Position.z - Players[i].Position.z);

            if (flat.magnitude > Attrs[i].Reach)
            {
                OnLog?.Invoke($"[#{i} {Players[i].Role}] não alcançou - {flat.magnitude:F2} m");
                return;
            }

            switch (Rally.TouchCount)
            {
                case 0:  ResolvePass(i, quality);   break;
                case 1:  ResolveSet(i, quality);    break;
                default: ResolveAttack(i, quality); break;
            }
        }

        private void ResolvePass(int i, float quality)
        {
            int side = Players[i].Side;

            Vector2 deviation = RandomInCircle(MaxPassError * (1f - quality));
            Vector3 target = SetterTargetOf(side) + new Vector3(deviation.x, 0f, deviation.y);

            LaunchTo(KeepOffNet(target, side), 65f, i, $"passe q={quality:F2} {deviation.magnitude:F2} m do levantador");
        }

        private void ResolveSet(int i, float quality)
        {
            int side = Players[i].Side;

            Vector2 aim = IsHuman[i] ? _armedAim : AiSetAim(i);
            int attacker = ChooseAttacker(i, aim);

            if (attacker < 0)
            {
                Vector2 fallback = RandomInCircle(MaxSetError * (1f - quality));
                Vector3 fallbackTarget = AttackTargetOf(side) + new Vector3(fallback.x, 0f, fallback.y);

                LaunchTo(KeepOffNet(fallbackTarget, side), 70f, i, $"levantamento sem atacante q={quality:F2}");
                return;
            }

            // a middle set is lower and tighter to the net: faster to the hitter
            bool isMiddle = EffectiveRole(attacker) == PlayerRole.MiddleBlocker;
            float netDistance = isMiddle ? 0.9f : 1.3f;
            float angle = isMiddle ? 52f : 70f;

            Vector3 baseTarget = new Vector3(Players[attacker].Base.x, 3.00f, netDistance * side);
            Vector2 deviation = RandomInCircle(MaxSetError * (1f - quality));
            Vector3 target = baseTarget + new Vector3(deviation.x, 0f, deviation.y);

            LaunchTo(KeepOffNet(target, side), angle, i, $"levantamento -> #{attacker} {EffectiveRole(attacker)} " + $"q={quality:F2} {deviation.magnitude:F2}m do alvo");
        }

        private void ResolveAttack(int i, float quality)
        {
            int side = Players[i].Side;

            if (!IsFront(i) && Mathf.Abs(Ball.Position.z) < Court.AttackLine && Ball.Position.y > NetHeight)
            {
                EndRally(-side, "ataque de fundo à frente da linha de 3m");
                return;
            }

            Vector2 aim = IsHuman[i] ? _armedAim : AiSpikeAim(i);
            float baseX = Mathf.Clamp(aim.x * SpikeAimRange, -3.8f, 3.8f);

            Vector2 deviation = RandomInCircle(MaxSpikeError * (1f - quality));
            Vector3 target = new Vector3(baseX + deviation.x, Court.BallRadius, (-SpikeDepth * side) + deviation.y);

            // a poor contact cannot hit down: it must clear the tape by more
            float clearance = Mathf.Lerp(0.60f, 0.10f, quality);

            // sweep from -35 degrees upward: returns the steepest spike that still clears the net
            if (!BallPhysics.SolveFlattestLegal(Ball.Position, target, NetHeight, clearance, 1f / 60f, out Vector3 velocity, -35f, 40f, 1f))
            {
                OnLog?.Invoke("sem angulo legal pro ataque");
                return;
            }

            float launchAngle = Mathf.Asin(velocity.normalized.y) * Mathf.Rad2Deg;

            SetBallTrajectory(Ball.Position, velocity);
            Rally.OnTouch(i, side);

            OnLog?.Invoke($"ATAQUE q={quality:F2} {velocity.magnitude:F1} m/s a {launchAngle:F0}º" + $" alvo x={baseX:F1} [toque {Rally.TouchCount}/3]");
            OnEvent?.Invoke(SimEventKind.Attack, Ball.Position, Mathf.Clamp01(velocity.magnitude / 25f));
        }

        /// <summary>Picks which front-row attacker receives the set, from the lateral aim.</summary>
        private int ChooseAttacker(int setter, Vector2 aim)
        {
            int teamBase = TeamBase(Players[setter].Side);
            float desiredX = Mathf.Clamp(aim.x * 3.6f, -3.8f, 3.8f);

            int chosen = -1;
            float bestDistance = float.MaxValue;

            for (int k = 0; k < 6; k++)
            {
                int j = teamBase + k;
                if (j == setter) continue;
                if (!IsFront(j)) continue;
                if (EffectiveRole(j) == PlayerRole.Libero) continue;

                float distance = Mathf.Abs(Players[j].Base.x - desiredX);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    chosen = j;
                }
            }

            return chosen;
        }

        /// <summary>Solves the ballistics of a touch and records it in the rally state.</summary>
        private void LaunchTo(Vector3 target, float angle, int playerIndex, string message)
        {
            if (!BallPhysics.SolveLaunch(Ball.Position, target, angle, 1f / 60f, out Vector3 velocity))
            {
                OnLog?.Invoke($"solver falhou: de y={Ball.Position.y:F2} para {target}");
                return;
            }

            SetBallTrajectory(Ball.Position, velocity);
            Rally.OnTouch(playerIndex, Players[playerIndex].Side);
            OnLog?.Invoke($"{message} [toque {Rally.TouchCount}/3]");

            SimEventKind kind = Rally.TouchCount == 1 ? SimEventKind.Pass
                              : Rally.TouchCount == 2 ? SimEventKind.Set
                                                      : SimEventKind.Attack;

            OnEvent?.Invoke(kind, Ball.Position, Mathf.Clamp01(velocity.magnitude / 25f));
        }

        /// <summary>A pass or set is never placed on top of the net or on the other side.</summary>
        private static Vector3 KeepOffNet(Vector3 target, int side)
        {
            const float minDistance = 0.6f;

            if (Court.SideOf(target.z) != side || Mathf.Abs(target.z) < minDistance)
                target.z = minDistance * side;

            return target;
        }
    }
}
