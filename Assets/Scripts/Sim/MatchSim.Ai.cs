using UnityEngine;

namespace Volley.Sim
{
    /// <summary>Comportamento e dificuldade da IA.</summary>
    public partial class MatchSim
    {
        public AiProfile Ai = AiProfile.Normal; // adversário

        public AiProfile AiAmigo = AiProfile.Normal; // seus companheiros

        private readonly Vector3[] _leitura = new Vector3[2];

        private readonly float[] _desvioBloqueio = new float[2];

        private readonly bool[] _vaiBloquear = new bool[2];

        private float _desdeToque;

        private AiProfile PerfilDe(int side) => (side == HumanSide) ? AiAmigo : Ai;

        /// <summary>
        /// Qualidade da IA: quanto ela foi obrigada a sair da posição.
        /// </summary>
        private float AiQuality(int i)
        {
            float desloc = Vector2.Distance(
                new Vector2(Players[i].Position.x, Players[i].Position.z),
                new Vector2(Players[i].Base.x, Players[i].Base.z)
            );

            float q = 1f - Mathf.Clamp01(desloc / MaxDisplacement);

            return Mathf.Min(q, PerfilDe(Players[i].Side).TetoQualidade);
        }

        /// <summary>
        /// A cada toque a bola muda de trajetória e cada time "lê" de novo - com erro.
        /// Sorteado uma vez por trajetória, não por frame: erro por frame vira tremor.
        /// </summary>
        private void NovaLeitura()
        {
            _desdeToque = 0f;

            for (int t = 0; t < 2; t++)
            {
                int side = (t == 0) ? Court.SideA : Court.SideB;
                AiProfile p = PerfilDe(side);

                Vector2 e = RandomInCircle(p.ErroLeitura);
                _leitura[t] = new Vector3(e.x, 0f, e.y);

                _desvioBloqueio[t] = (float)(_rng.NextDouble() * 2.0 - 1.0) * p.ErroBloqueio;
                _vaiBloquear[t] = _rng.NextDouble() < p.ChanceBloqueio;
            }
        }

        private void UpdateAiBlock()
        {
            if (!HasNetCross) return;
            if (NetCrossPoint.y <= NetHeight) return;
            if (Rally.ServeInFlight) return;

            int atkSide = Court.SideOf(Ball.Position.z);
            if (atkSide == 0) return;

            int defSide = -atkSide;
            if (TimeToNet >= 0.14f) return;

            if (!_vaiBloquear[TeamIdx(defSide)]) return;

            int b = TeamBase(defSide);

            for (int k = 0; k < 6; k++)
            {
                int i = b + k;
                
                if (i == HumanIndex) continue;
                if (!IsFront(i)) continue;
                if (EffectiveRole(i) == PlayerRole.Libero) continue;
                if (Players[i].BlockTimer > 0f) continue;
                if (NetCrossPoint.y > Attrs[i].BlockReach) continue;
                if (Mathf.Abs(NetCrossPoint.x - Players[i].Position.x) > BlockHalfWidth) continue;

                Players[i].BlockTimer = BlockDuration;
                OnLog?.Invoke($"[#{i}] IA salta");
            }
        }

        /// <summary>A IA procura o buraco entre os bloqueadores adversários.</summary>
        private Vector2 AiAim(int i)
        {
            int opp = -Players[i].Side;
            int b = TeamBase(opp);

            float melhorX = 0f, melhorD = -1f;

            for (int s = 0; s < 7; s++)
            {
                float x = -3.6f + s * 1.2f;
                float d = float.MaxValue;

                for (int k = 0; k < 6; k++)
                {
                    int j = b + k;
                    if (!IsFront(j)) continue;
                    d = Mathf.Min(d, Mathf.Abs(Players[j].Position.x - x));
                }

                if (d > melhorD)
                {
                    melhorD = d;
                    melhorX = x;
                }
            }

            float ruido = (float)(_rng.NextDouble() * 2.0 - 1.0) * PerfilDe(Players[i].Side).RuidoMira;

            return new Vector2((melhorX + ruido) / 3.6f, 0f);
        }

        /// <summary>A IA levanta pra quem estiver menos marcado pelo bloqueio.</summary>
        private Vector2 AiSetAim(int levantador)
        {
            int side = Players[levantador].Side;
            int b = TeamBase(side);
            int opp = TeamBase(-side);

            float melhorX = 0f, melhorD = -1f;

            for (int k = 0; k < 6; k++)
            {
                int j = b + k;
                if (j == levantador || !IsFront(j)) continue;
                if (EffectiveRole(j) == PlayerRole.Libero) continue;

                float x = Players[j].Base.x;
                float d = float.MaxValue;

                for (int m = 0; m < 6; m++)
                {
                    int o = opp + m;
                    if (!IsFront(o)) continue;

                    d = Mathf.Min(d, Mathf.Abs(Players[o].Position.x - x));
                }

                if (d > melhorD)
                {
                    melhorD = d;
                    melhorX = x;
                }
            }

            float ruido = (float)(_rng.NextDouble() * 2.0 - 1.0) * PerfilDe(Players[levantador].Side).RuidoMira;

            return new Vector2((melhorX + ruido) / 3.6f, 0f);
        }
    }
}
