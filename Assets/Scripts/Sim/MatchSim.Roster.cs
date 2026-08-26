using UnityEngine;

namespace Volley.Sim
{
    /// <summary>Elenco, zonas, rodízio, papéis e fases.</summary>
    public partial class MatchSim
    {
        /// <summary>
        /// Quem joga a próxima bola: no 1º toque quem estiver mais perto de onde a bola vai cair; depois o levantador e o atacante do time.
        /// </summary>
        public int ActiveIndex
        {
            get
            {
                int b = TeamBase(Rally.TouchingSide);

                if (Rally.TouchCount == 0) return ClosestTo(b, ContactPoint, Rally.LastToucher);

                if (Rally.TouchCount == 1)
                {
                    int lev = FindRole(b, PlayerRole.Levantador);

                    if (lev >= 0 && lev != Rally.LastToucher) return lev;
                    
                    return ClosestTo(b, ContactPoint, Rally.LastToucher);
                }

                return ClosestTo(b, ContactPoint, Rally.LastToucher, true);
            }
        }

        /// <summary>Manchete na cintura, levantamento acima da cabeça.</summary>
        public float ActiveContactHeight
        {
            get
            {
                switch (Rally.TouchCount)
                {
                    case 1: return 2.10f;
                    case 2: return 3.00f;
                    default: return 0.90f;
                }
            }
        }

        /// <summary>
        /// Quem o humano controla. Só muda em evento discreto, nunca no meio do voo.
        /// </summary>
        public int ControlledIndex => HumanIndex;

        private int FindRole(int b, PlayerRole r)
        {
            for (int k = 0; k < 6; k++)
            {
                if (Players[b + k].Role == r) return b + k;
            }

            return -1;
        }

        private int ClosestTo(int b, Vector3 p, int exclude = -1, bool frontOnly = false)
        {
            int best = -1;
            float bestD = float.MaxValue;

            for (int k = 0; k < 6; k++)
            {
                int idx = b + k;
                if (idx == exclude) continue;
                if (frontOnly && !IsFront(idx)) continue;

                float d = Vector2.Distance(
                    new Vector2(Players[idx].Position.x, Players[idx].Position.z),
                    new Vector2(p.x, p.z)
                );

                if (d < bestD)
                {
                    bestD = d;
                    best = idx;
                }
            }

            if (best < 0 && frontOnly) return ClosestTo(b, p, exclude, false);

            return best < 0 ? b : best;
        }

        private int FrontClosestToX(int b, float x)
        {
            int best = -1;
            float bestD = float.MaxValue;

            for (int k = 0; k < 6; k++)
            {
                int i = b + k;
                if (!IsFront(i)) continue;

                float d = Mathf.Abs(Players[i].Position.x - x);

                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }

            return best;
        }

        private static readonly int[] RotOrder = { 1, 6, 5, 4, 3, 2 };

        // (x, z) das zonas 1...6 vistas do lado B; o lado A espelha os dois eixos
        private static readonly Vector2[] ZoneXZ =
        {
            new Vector2(-3.0f, 6.5f), // 1 fundo direita
            new Vector2(-3.0f, 2.0f), // 2 frente direita
            new Vector2(0.0f, 2.0f), // 3 frente centro
            new Vector2(3.0f, 2.0f), // 4 frente esquerda
            new Vector2(3.0f, 6.5f), // 5 fundo esquerda
            new Vector2(0.0f, 6.5f), // 6 fundo centro
        };

        private static readonly PlayerRole[] SlotRole =
        {
            PlayerRole.Levantador,   // 0  ─┐ diagonal
            PlayerRole.Ponteiro,     // 1  ─┼─┐
            PlayerRole.Central,      // 2  ─┼─┼─┐
            PlayerRole.Oposto,       // 3  ─┘ │ │
            PlayerRole.Ponteiro,     // 4  ───┘ │
            PlayerRole.Central,      // 5  ─────┘
        };

        public enum TeamPhase { Saque, Recepcao, Ataque, Defesa }

        public TeamPhase PhaseOf(int side)
        {
            if (Rally.ServeInFlight)
            {
                return (Rally.ServingSide == side) ? TeamPhase.Saque : TeamPhase.Recepcao;
            }

            return (Rally.TouchingSide == side) ? TeamPhase.Ataque : TeamPhase.Defesa;
        }

        /// <summary>
        /// Aproximação do líbero: um central que rodizia para o fundo joga como líbero.
        /// A substituição de verdade precisa de elenco com reservas - M6.
        /// </summary>
        public PlayerRole EffectiveRole(int i) {
            if (Players[i].Role != PlayerRole.Central) return Players[i].Role;

            if (IsFront(i)) return PlayerRole.Central;

            if (ZoneOf(i) == 1) return PlayerRole.Central; // saca; o líbero só entra depois

            return PlayerRole.Libero;
        } 

        private static bool Recebe(PlayerRole r) => r == PlayerRole.Ponteiro || r == PlayerRole.Libero;

        public static int SlotForRole(PlayerRole r, int nth)
        {
            int achados = 0;
            for (int k = 0; k < 6; k++)
            {
                if (SlotRole[k] != r) continue;
                if (achados == nth) return k;
                achados++;
            }
            return 0;
        }

        // ============= rodízio ==============
        public static Vector3 ZonePos(int zone, int side)
        {
            Vector2 v = ZoneXZ[zone - 1];
            return new Vector3(v.x * side, 0f, v.y * side);
        }

        public int ZoneOf(int i)
        {
            int t = TeamIdx(Players[i].Side);
            return RotOrder[(Players[i].Slot + Rotation[t]) % 6];
        }

        /// <summary>Zonas 2, 3 e 4 são a linha de frente: só elas bloqueiam.</summary>
        public bool IsFront(int i)
        {
            int z = ZoneOf(i);
            return z >= 2 && z <= 4;
        }

        private void Rotate(int side)
        {
            int t = TeamIdx(side);
            Rotation[t] = (Rotation[t] + 1) % 6;
            OnLog?.Invoke($"rodízio do lado {SideName(side)} -> rotação {Rotation[t]}");
        }

        private void SetupTeam(int side)
        {
            int b = TeamBase(side);
            int slotHumano = SlotForRole(HumanRole, HumanRoleIndex);

            for (int k = 0; k < 6; k++)
            {
                int i = b + k;
                Players[i] = new PlayerState
                {
                    Id = i,
                    Side = side,
                    Slot = k,
                    Role = SlotRole[k]
                };

                Attrs[i] = PlayerAttributes.Default;
                IsHuman[i] = (side == HumanSide) && (k == slotHumano);
                if (IsHuman[i]) HumanIndex = i;
            }

            for (int k = 0; k < 6; k++)
            {
                int i = b + k;
                Vector3 p = ZonePos(ZoneOf(i), side);

                Players[i].Base = HomeFor(i);
                Players[i].Position = ZonePos(ZoneOf(i), side);
                Players[i].Velocity = Vector3.zero;
                Players[i].BlockTimer = 0f;
            }
        }
    }
}
