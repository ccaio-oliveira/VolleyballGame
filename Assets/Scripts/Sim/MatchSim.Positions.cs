using UnityEngine;

namespace Volley.Sim
{
    /// <summary>Functional homes per phase and player movement.</summary>
    public partial class MatchSim
    {
        // ---------- reference spots (side B coordinates, mirrored for side A) ----------

        /// <summary>
        /// The setter's home: between zones 2 and 3, tight to the net. Single source of
        /// truth — both the setter's position and the pass target derive from it, so the
        /// two can never drift apart.
        /// </summary>
        private static readonly Vector2 SetterHomeReference = new Vector2(-1.3f, 1.6f);

        private static Vector3 SetterHome(int side)
            => new Vector3(SetterHomeReference.x * side, 0f, SetterHomeReference.y * side);

        /// <summary>Where a pass should arrive: above the setter's home, at hand height.</summary>
        private static Vector3 SetterTargetOf(int side)
        {
            Vector3 home = SetterHome(side);
            return new Vector3(home.x, 2.10f, home.z);
        }

        /// <summary>Fallback set target: outside hitter spot in zone 4.</summary>
        private static Vector3 AttackTargetOf(int side)
        {
            Vector3 zone4 = ZonePosition(4, side);
            return new Vector3(zone4.x, 3.00f, 1.2f * side);
        }

        // ---------- functional home per phase ----------

        /// <summary>
        /// Where player <paramref name="i"/> should stand right now. Only the serve uses the
        /// rotation zone; every other phase uses the function (role + front/back row).
        /// </summary>
        private Vector3 HomeFor(int i)
        {
            int side = Players[i].Side;
            Vector2 spot;

            switch (PhaseOf(side))
            {
                case TeamPhase.Serve:     spot = ServeHome(i);     break;
                case TeamPhase.Reception: spot = ReceptionHome(i); break;
                case TeamPhase.Attack:    spot = AttackHome(i);    break;
                default:                  spot = DefenseHome(i);   break;
            }

            return new Vector3(spot.x * side, 0f, spot.y * side);
        }

        /// <summary>The server is whoever stands in zone 1; the rest wait in defense.</summary>
        private Vector2 ServeHome(int i)
            => ZoneOf(i) == 1 ? new Vector2(-3.0f, 9.6f) : DefenseHome(i);

        /// <summary>Three passers (outside hitters + libero); everyone else freed to attack.</summary>
        private Vector2 ReceptionHome(int i)
        {
            switch (EffectiveRole(i))
            {
                case PlayerRole.Setter:
                    return SetterHomeReference;                                   // runs in to the net

                case PlayerRole.MiddleBlocker:
                    return new Vector2(0.3f, 1.4f);                               // P3, at the net

                case PlayerRole.Opposite:
                    return IsFront(i) ? new Vector2(-3.4f, 1.8f)                  // P2
                                      : new Vector2(-3.4f, 7.2f);                 // P1, behind the 3 m line

                default:
                    return ReceptionLineSpot(i);                                  // outside hitters + libero
            }
        }

        /// <summary>Approach runs and coverage while the team builds its attack.</summary>
        private Vector2 AttackHome(int i)
        {
            switch (EffectiveRole(i))
            {
                case PlayerRole.Setter:
                    return SetterHomeReference;                                   // setting zone

                case PlayerRole.MiddleBlocker:
                    return new Vector2(0.6f, 1.6f);                               // quick attack

                case PlayerRole.Libero:
                    return new Vector2(1.5f, 3.6f);                               // coverage

                case PlayerRole.Opposite:
                    return IsFront(i) ? new Vector2(-3.8f, 2.6f)                  // P2 approach
                                      : new Vector2(-3.4f, 5.2f);                 // back-row attack

                default:
                    return IsFront(i) ? new Vector2(3.8f, 3.0f)                   // P4 approach
                                      : new Vector2(0.0f, 5.0f);                  // pipe
            }
        }

        /// <summary>Opponent attacking: front row at the net to block, back row defending 1/5/6.</summary>
        private Vector2 DefenseHome(int i)
        {
            PlayerRole role = EffectiveRole(i);

            if (IsFront(i))
            {
                switch (role)
                {
                    case PlayerRole.OutsideHitter: return new Vector2( 3.0f, 0.9f);   // P4
                    case PlayerRole.MiddleBlocker: return new Vector2( 0.0f, 0.9f);   // P3
                    default:                       return new Vector2(-3.0f, 0.9f);   // P2
                }
            }

            switch (role)
            {
                case PlayerRole.Libero:        return new Vector2( 3.3f, 6.3f);       // P5
                case PlayerRole.OutsideHitter: return new Vector2( 0.0f, 7.6f);       // P6
                default:                       return new Vector2(-3.3f, 6.3f);       // P1
            }
        }

        /// <summary>Spreads the passers evenly across the reception line.</summary>
        private Vector2 ReceptionLineSpot(int i)
        {
            int teamBase = TeamBase(Players[i].Side);
            int order = 0, total = 0;

            for (int k = 0; k < 6; k++)
            {
                int j = teamBase + k;
                if (!IsReceiver(EffectiveRole(j))) continue;
                if (j == i) order = total;
                total++;
            }

            float x = (total <= 1) ? 0f : Mathf.Lerp(3.2f, -3.2f, order / (float)(total - 1));

            return new Vector2(x, 6.0f);
        }

        // ---------- movement ----------

        /// <summary>
        /// Who on this side will play the next ball — including when the opponent still
        /// has possession but the ball is already heading here. Without this the AI only
        /// reacts after the net crossing and always arrives late.
        /// </summary>
        private int ResponsiblePlayer(int side)
        {
            if (Rally.TouchingSide == side) return ActiveIndex;

            if (HasPrediction && Court.SideOf(PredictedLanding.z) == side)
                return ClosestTo(TeamBase(side), PredictedLanding);

            return -1;
        }

        private void StepPlayers(float dt)
        {
            for (int i = 0; i < Players.Length; i++)
            {
                if (Players[i].BlockTimer > 0f)
                    Players[i].BlockTimer -= dt;

                if (Players[i].HitTimer > 0f)
                {
                    Players[i].HitTimer -= dt;
                    if (Players[i].HitTimer <= 0f) ResolveAiTouch(i);
                }
            }

            UpdateAiBlock();
            UpdateAiTouches();

            int responsibleA = ResponsiblePlayer(Court.SideA);
            int responsibleB = ResponsiblePlayer(Court.SideB);

            for (int i = 0; i < Players.Length; i++)
            {
                int side = Players[i].Side;

                Players[i].Base = HomeFor(i);   // functional home depends on the phase, so every tick

                // airborne: no control, but physics keeps running
                if (Players[i].Position.y > 0f)
                {
                    Players[i] = PlayerPhysics.StepAirborne(Players[i], Attrs[i], dt);
                    continue;
                }

                if (ShouldHoldNetForBlock(i, side))
                {
                    Players[i] = PlayerPhysics.StepToTarget(Players[i], Attrs[i], BlockPost(i, side), dt);
                    continue;
                }

                int responsible = (side == Court.SideA) ? responsibleA : responsibleB;
                bool hasPossession = (Rally.TouchingSide == side);
                Vector3 ballTarget = hasPossession ? ContactPoint : PredictedLanding;
                bool ballComingHere = Court.SideOf(ballTarget.z) == side
                                   && (hasPossession ? HasContact : HasPrediction);

                if (i == HumanIndex)
                {
                    Players[i] = PlayerPhysics.StepDirect(Players[i], Attrs[i], MoveInput, dt);
                }
                else if (i == responsible && ballComingHere)
                {
                    Vector3 target = ballTarget;

                    if (!IsHuman[i])
                    {
                        AiProfile profile = ProfileFor(side);
                        target = (_timeSinceTouch < profile.ReadDelay)
                            ? Players[i].Base                               // has not reacted yet
                            : ballTarget + _readError[TeamIndex(side)];     // read with error
                    }

                    Players[i] = PlayerPhysics.StepToTarget(Players[i], Attrs[i], target, dt);
                }
                else
                {
                    Players[i] = PlayerPhysics.StepToTarget(Players[i], Attrs[i], Players[i].Base, dt);
                }
            }
        }

        /// <summary>AI front-row players move up to the net while the opponent builds an attack.</summary>
        private bool ShouldHoldNetForBlock(int i, int side)
        {
            return !IsHuman[i]
                && HasNetCross
                && IsFront(i)
                && !Rally.ServeInFlight
                && EffectiveRole(i) != PlayerRole.Libero
                && Court.SideOf(Ball.Position.z) == -side;
        }

        /// <summary>
        /// Block post at the net. The closest blocker goes to the read point; the others
        /// close in beside him, forming a wall.
        /// </summary>
        private Vector3 BlockPost(int i, int side)
        {
            int attackSide = -side;

            float targetX = (Rally.TouchCount >= 2 && NetCrossPoint.y > NetHeight)
                ? NetCrossPoint.x                  // attack in the air: slide to the real point
                : AttackTargetOf(attackSide).x;    // before the attack: read the set

            targetX += _blockOffset[TeamIndex(side)];

            int nearest = FrontClosestToX(TeamBase(side), targetX);
            float x = (i == nearest) ? targetX : Mathf.Lerp(Players[i].Base.x, targetX, 0.4f);

            return new Vector3(x, 0f, 0.8f * side);
        }
    }
}
