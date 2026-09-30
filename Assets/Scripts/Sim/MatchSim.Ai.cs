using UnityEngine;

namespace Volley.Sim
{
    /// <summary>
    /// AI decisions and difficulty. Difficulty degrades perception, never execution:
    /// a read error becomes displacement, and displacement becomes a poor touch.
    /// </summary>
    public partial class MatchSim
    {
        public AiProfile OpponentProfile = AiProfile.Normal;
        public AiProfile TeammateProfile = AiProfile.Normal;

        // rolled once per trajectory, per team (0 = side A, 1 = side B)
        private readonly Vector3[] _readError = new Vector3[2];
        private readonly float[] _blockOffset = new float[2];
        private readonly bool[] _willBlock = new bool[2];
        private float _timeSinceTouch;

        private AiProfile ProfileFor(int side) => (side == HumanSide) ? TeammateProfile : OpponentProfile;

        /// <summary>
        /// AI touch quality, measured at the moment of contact.
        /// First touch (reception or dig): how centered the player is under the ball. A passer
        /// is supposed to run to the ball, so leaving his home is not a mistake.
        /// Set and attack: displacement from the functional home — a setter dragged out of
        /// position by a bad pass sets worse, which is the chain the whole game is built on.
        /// </summary>
        private float AiQuality(int i)
        {
            float quality = Rally.TouchCount == 0
                ? FirstTouchQuality(i)
                : HomeDisplacementQuality(i);

            return Mathf.Min(quality, ProfileFor(Players[i].Side).QualityCap);
        }

        /// <summary>1 with the ball on the player's midline, down to EdgeOfReachQuality at the edge of reach.</summary>
        private float FirstTouchQuality(int i)
        {
            float offCenter = HorizontalDistance(Players[i].Position, Ball.Position);
            return Mathf.Lerp(1f, EdgeOfReachQuality, offCenter / Attrs[i].Reach);
        }

        private float HomeDisplacementQuality(int i)
        {
            float displacement = HorizontalDistance(Players[i].Position, Players[i].Base);
            return 1f - Mathf.Clamp01(displacement / MaxDisplacement);
        }

        /// <summary>
        /// Every touch changes the trajectory and each team "reads" it again — with error.
        /// Rolled once per trajectory, never per frame: per-frame error becomes jitter.
        /// </summary>
        private void RollAiReads()
        {
            _timeSinceTouch = 0f;

            for (int team = 0; team < 2; team++)
            {
                int side = (team == 0) ? Court.SideA : Court.SideB;
                AiProfile profile = ProfileFor(side);

                Vector2 error = RandomInCircle(profile.ReadError);
                _readError[team] = new Vector3(error.x, 0f, error.y);

                _blockOffset[team] = (float)(_rng.NextDouble() * 2.0 - 1.0) * profile.BlockError;
                _willBlock[team] = _rng.NextDouble() < profile.BlockChance;
            }
        }

        /// <summary>AI front-row players jump when an attack is about to cross in front of them.</summary>
        private void UpdateAiBlock()
        {
            if (!HasNetCross) return;
            if (NetCrossPoint.y <= NetHeight) return;
            if (Rally.ServeInFlight) return;

            int attackingSide = Court.SideOf(Ball.Position.z);
            if (attackingSide == 0) return;

            int defendingSide = -attackingSide;
            if (TimeToNet >= 0.14f) return;
            if (!_willBlock[TeamIndex(defendingSide)]) return;

            int teamBase = TeamBase(defendingSide);

            for (int k = 0; k < 6; k++)
            {
                int i = teamBase + k;

                if (i == HumanIndex) continue;   // the human block is on the button
                if (!IsFront(i)) continue;
                if (EffectiveRole(i) == PlayerRole.Libero) continue;
                if (Players[i].BlockTimer > 0f) continue;
                if (NetCrossPoint.y > Players[i].Position.y + Attrs[i].ReachHeight) continue;
                if (Mathf.Abs(NetCrossPoint.x - Players[i].Position.x) > BlockHalfWidth) continue;

                Players[i] = PlayerPhysics.Jump(Players[i], Vector2.zero, 0f);
                Players[i].BlockTimer = BlockDuration;
                OnLog?.Invoke($"[#{i}] IA salta");
            }
        }

        /// <summary>
        /// The AI arms its own touch, each at the right height. Same mechanism as the human —
        /// one clock per player; only who presses the button changes.
        /// </summary>
        private void UpdateAiTouches()
        {
            if (Rally.TouchCount >= 3) return;

            int i = ActiveIndex;
            if (IsHuman[i]) return;
            if (Players[i].HitTimer > 0f) return;
            if (_resolvedTouch == Rally.TouchCount && _resolvedSide == Rally.TouchingSide) return;

            bool isAttack = (Rally.TouchCount == 2);

            // on the attack the AI aims its hand at the top of the jump, not standing reach
            float height = isAttack ? Attrs[i].ReachHeight + 0.78f
                         : Rally.TouchCount == 1 ? OverheadHeight(i)
                                                 : PassHeight(i);

            if (!BallPhysics.PredictLanding(Ball, height, 1f / 60f, 6f, out _, out float timeToHeight)) return;
            if (timeToHeight > (isAttack ? 0.40f : 0.22f)) return;   // only commits when close

            if (isAttack && Players[i].Position.y <= 0f)
                Players[i] = PlayerPhysics.Jump(Players[i], Vector2.zero, 0f);

            Players[i].HitTimer = Mathf.Max(0.001f, timeToHeight);
        }

        /// <summary>The AI spikes into the widest gap between the opposing blockers.</summary>
        private Vector2 AiSpikeAim(int i)
        {
            int opponentBase = TeamBase(-Players[i].Side);

            float bestX = 0f, bestGap = -1f;

            for (int s = 0; s < 7; s++)
            {
                float x = -3.6f + s * 1.2f;   // derived from the integer index, never accumulated
                float gap = float.MaxValue;

                for (int k = 0; k < 6; k++)
                {
                    int j = opponentBase + k;
                    if (!IsFront(j)) continue;
                    gap = Mathf.Min(gap, Mathf.Abs(Players[j].Position.x - x));
                }

                if (gap > bestGap)
                {
                    bestGap = gap;
                    bestX = x;
                }
            }

            float noise = (float)(_rng.NextDouble() * 2.0 - 1.0) * ProfileFor(Players[i].Side).AimNoise;

            return new Vector2((bestX + noise) / 3.6f, 0f);
        }

        /// <summary>The AI sets whichever attacker the opposing block has left least covered.</summary>
        private Vector2 AiSetAim(int setter)
        {
            int side = Players[setter].Side;
            int teamBase = TeamBase(side);
            int opponentBase = TeamBase(-side);

            float bestX = 0f, bestGap = -1f;

            for (int k = 0; k < 6; k++)
            {
                int j = teamBase + k;
                if (j == setter || !IsFront(j)) continue;
                if (EffectiveRole(j) == PlayerRole.Libero) continue;

                float x = Players[j].Base.x;
                float gap = float.MaxValue;

                for (int m = 0; m < 6; m++)
                {
                    int o = opponentBase + m;
                    if (!IsFront(o)) continue;
                    gap = Mathf.Min(gap, Mathf.Abs(Players[o].Position.x - x));
                }

                if (gap > bestGap)
                {
                    bestGap = gap;
                    bestX = x;
                }
            }

            float noise = (float)(_rng.NextDouble() * 2.0 - 1.0) * ProfileFor(side).AimNoise;

            return new Vector2((bestX + noise) / 3.6f, 0f);
        }
    }
}
