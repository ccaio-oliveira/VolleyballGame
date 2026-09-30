using UnityEngine;

namespace Volley.Sim
{
    /// <summary>Roster, rotation, zones, roles, phases and who plays the ball.</summary>
    public partial class MatchSim
    {
        // ---------- court geometry for the rotation ----------

        // rotation order: whoever is in zone 1 moves to 6, from 6 to 5, and so on
        private static readonly int[] RotationOrder = { 1, 6, 5, 4, 3, 2 };

        // (x, z) of zones 1..6 seen from side B; side A mirrors both axes
        private static readonly Vector2[] ZoneCoordinates =
        {
            new Vector2(-3.0f, 6.5f),   // 1 back right
            new Vector2(-3.0f, 2.0f),   // 2 front right
            new Vector2( 0.0f, 2.0f),   // 3 front center
            new Vector2( 3.0f, 2.0f),   // 4 front left
            new Vector2( 3.0f, 6.5f),   // 5 back left
            new Vector2( 0.0f, 6.5f),   // 6 back center
        };

        // 5-1 lineup by slot; diagonals are 0-3, 1-4 and 2-5
        private static readonly PlayerRole[] SlotRoles =
        {
            PlayerRole.Setter,          // 0
            PlayerRole.OutsideHitter,   // 1
            PlayerRole.MiddleBlocker,   // 2
            PlayerRole.Opposite,        // 3
            PlayerRole.OutsideHitter,   // 4
            PlayerRole.MiddleBlocker,   // 5
        };

        // ---------- who plays the ball ----------

        /// <summary>
        /// Who plays the next ball: on the first touch whoever is closest to the contact
        /// point; then the setter; then the attacker. Nobody touches twice in a row.
        /// </summary>
        public int ActiveIndex
        {
            get
            {
                int teamBase = TeamBase(Rally.TouchingSide);

                if (Rally.TouchCount == 0)
                    return ClosestTo(teamBase, ContactPoint, Rally.LastToucher);

                if (Rally.TouchCount == 1)
                {
                    int setter = FindRole(teamBase, PlayerRole.Setter);
                    if (setter >= 0 && setter != Rally.LastToucher) return setter;

                    // the setter took the first ball: someone else covers the second
                    return ClosestTo(teamBase, ContactPoint, Rally.LastToucher);
                }

                // no front-row restriction: with physical jumps, back-row players attack too;
                // the 3 m line rule in ResolveTouch judges legality
                return ClosestTo(teamBase, ContactPoint, Rally.LastToucher);
            }
        }

        /// <summary>Who the human controls. Fixed to the chosen position for the whole match.</summary>
        public int ControlledIndex => HumanIndex;

        private int FindRole(int teamBase, PlayerRole role)
        {
            for (int k = 0; k < 6; k++)
            {
                if (Players[teamBase + k].Role == role) return teamBase + k;
            }

            return -1;
        }

        private int ClosestTo(int teamBase, Vector3 point, int exclude = -1, bool frontOnly = false)
        {
            int best = -1;
            float bestDistance = float.MaxValue;

            for (int k = 0; k < 6; k++)
            {
                int index = teamBase + k;
                if (index == exclude) continue;
                if (frontOnly && !IsFront(index)) continue;

                float distance = Vector2.Distance(
                    new Vector2(Players[index].Position.x, Players[index].Position.z),
                    new Vector2(point.x, point.z));

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = index;
                }
            }

            if (best < 0 && frontOnly) return ClosestTo(teamBase, point, exclude, false);

            return best < 0 ? teamBase : best;
        }

        private int FrontClosestToX(int teamBase, float x)
        {
            int best = -1;
            float bestDistance = float.MaxValue;

            for (int k = 0; k < 6; k++)
            {
                int index = teamBase + k;
                if (!IsFront(index)) continue;

                float distance = Mathf.Abs(Players[index].Position.x - x);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = index;
                }
            }

            return best;
        }

        // ---------- phases and roles ----------

        public TeamPhase PhaseOf(int side)
        {
            if (Rally.ServeInFlight)
                return (Rally.ServingSide == side) ? TeamPhase.Serve : TeamPhase.Reception;

            return (Rally.TouchingSide == side) ? TeamPhase.Attack : TeamPhase.Defense;
        }

        /// <summary>
        /// Libero approximation: a middle blocker who rotates to the back row plays as
        /// libero, except in zone 1 where he serves. A real substitution needs a roster
        /// with reserves.
        /// </summary>
        public PlayerRole EffectiveRole(int i)
        {
            if (Players[i].Role != PlayerRole.MiddleBlocker) return Players[i].Role;
            if (IsFront(i)) return PlayerRole.MiddleBlocker;
            if (ZoneOf(i) == 1) return PlayerRole.MiddleBlocker;   // serves; the libero comes in afterwards

            return PlayerRole.Libero;
        }

        /// <summary>Passers in serve reception: both outside hitters and the libero.</summary>
        private static bool IsReceiver(PlayerRole role)
            => role == PlayerRole.OutsideHitter || role == PlayerRole.Libero;

        /// <summary>Slot of the n-th player with <paramref name="role"/> in the lineup.</summary>
        public static int SlotForRole(PlayerRole role, int nth)
        {
            int found = 0;
            for (int k = 0; k < 6; k++)
            {
                if (SlotRoles[k] != role) continue;
                if (found == nth) return k;
                found++;
            }

            return 0;
        }

        // ---------- rotation ----------

        public static Vector3 ZonePosition(int zone, int side)
        {
            Vector2 v = ZoneCoordinates[zone - 1];
            return new Vector3(v.x * side, 0f, v.y * side);
        }

        public int ZoneOf(int i)
        {
            int team = TeamIndex(Players[i].Side);
            return RotationOrder[(Players[i].Slot + Rotation[team]) % 6];
        }

        /// <summary>Zones 2, 3 and 4 form the front row: only they may block.</summary>
        public bool IsFront(int i)
        {
            int zone = ZoneOf(i);
            return zone >= 2 && zone <= 4;
        }

        private void Rotate(int side)
        {
            int team = TeamIndex(side);
            Rotation[team] = (Rotation[team] + 1) % 6;
            OnLog?.Invoke($"rodízio do lado {SideName(side)} -> rotação {Rotation[team]}");
        }

        private void SetupTeam(int side)
        {
            int teamBase = TeamBase(side);
            int humanSlot = SlotForRole(HumanRole, HumanRoleIndex);

            for (int k = 0; k < 6; k++)
            {
                int i = teamBase + k;
                Players[i] = new PlayerState
                {
                    Id = i,
                    Side = side,
                    Slot = k,
                    Role = SlotRoles[k],
                };

                Attrs[i] = PlayerAttributes.Default;
                IsHuman[i] = (side == HumanSide) && (k == humanSlot);
                if (IsHuman[i]) HumanIndex = i;
            }

            // second pass: ZoneOf and HomeFor need Side and Slot already filled
            for (int k = 0; k < 6; k++)
            {
                int i = teamBase + k;

                Players[i].Base = HomeFor(i);
                Players[i].Position = ZonePosition(ZoneOf(i), side);   // legal rotation at the serve
                Players[i].Velocity = Vector3.zero;
                Players[i].BlockTimer = 0f;
            }
        }
    }
}
