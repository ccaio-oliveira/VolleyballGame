using UnityEngine;

namespace Volley.Sim
{
    /// <summary>Casas funcionais por fase e movimentação dos jogadores.</summary>
    public partial class MatchSim
    {
        /// <summary>
        /// Casa do levantador, em coordenadas de referência do lado B: entre as zonas 2 e 3,
        /// colado na rede. Fonte única — a posição dele e o alvo do passe derivam daqui,
        /// então é impossível os dois saírem de sincronia.
        /// </summary>
        private static readonly Vector2 CasaLevantador = new Vector2(-1.3f, 1.6f);

        private static Vector3 SetterHome(int side)
            => new Vector3(CasaLevantador.x * side, 0f, CasaLevantador.y * side);

        private static Vector3 SetterSpotOf(int side){
            Vector3 h = SetterHome(side);
            return new Vector3(h.x, 2.10f, h.z);
        }

        private static Vector3 AttackSpotOf(int side){
            Vector3 z4 = ZonePos(4, side);
            return new Vector3(z4.x, 3.00f, 1.2f * side);
        }

        private Vector3 HomeFor(int i)
        {
            int side = Players[i].Side;
            Vector2 p;

            switch (PhaseOf(side))
            {
                case TeamPhase.Saque:
                    {
                        p = SaqueHome(i);
                        break;
                    }
                case TeamPhase.Recepcao:
                    {
                        p = RecepcaoHome(i);
                        break;
                    }
                case TeamPhase.Ataque:
                    {
                        p = AtaqueHome(i);
                        break;
                    }
                default:
                    {
                        p = DefesaHome(i);
                        break;
                    }
            }

            return new Vector3(p.x * side, 0f, p.y * side);
        }

        private Vector2 SaqueHome(int i) => ZoneOf(i) == 1 ? new Vector2(-3.0f, 9.6f) : DefesaHome(i);

        private Vector2 RecepcaoHome(int i)
        {
            switch (EffectiveRole(i))
            {
                case PlayerRole.Levantador: return CasaLevantador; // infiltra para a rede
                case PlayerRole.Central: return new Vector2(0.3f, 1.4f); // P3, na rede
                case PlayerRole.Oposto: return IsFront(i) 
                    ? new Vector2(-3.4f, 1.8f) // P2
                    : new Vector2(-3.4f, 7.2f); // P1, atrás dos 3m
                default: return LinhaDeRecepcao(i); // ponteiros + libero
            }
        }

        private Vector2 AtaqueHome(int i)
        {
            switch (EffectiveRole(i))
            {
                case PlayerRole.Levantador: return CasaLevantador; // zona de levantamento
                case PlayerRole.Central: return new Vector2(0.6f, 1.6f); // primeiro tempo
                case PlayerRole.Libero: return new Vector2(1.5f, 3.6f); // cobertura
                case PlayerRole.Oposto: return IsFront(i) 
                    ? new Vector2(-3.8f, 2.6f) // aproximação P2
                    : new Vector2(-3.4f, 5.2f); // ataque de fundo
                default: return IsFront(i) 
                    ? new Vector2(3.8f, 3.0f) // aproximação P4 
                    : new Vector2(0.0f, 5.0f); // pipe
            }
        }

        private Vector2 DefesaHome(int i)
        {
            PlayerRole r = EffectiveRole(i);

            if (IsFront(i))
            {
                switch(r)
                {
                    case PlayerRole.Ponteiro: return new Vector2(3.0f, 0.9f); // P4
                    case PlayerRole.Central: return new Vector2(0.0f, 0.9f); // P3
                    default: return new Vector2(-3.0f, 0.9f); // P2
                }
            }

            switch (r)
            {
                case PlayerRole.Libero: return new Vector2(3.3f, 6.3f); // P5
                case PlayerRole.Ponteiro: return new Vector2(0.0f, 7.6f); // P6
                default: return new Vector2(-3.3f, 6.3f); // P1
            }
        }

        private Vector2 LinhaDeRecepcao(int i)
        {
            int b = TeamBase(Players[i].Side);
            int ordem = 0, total = 0;

            for (int k = 0; k < 6; k++)
            {
                int j = b + k;
                if (!Recebe(EffectiveRole(j))) continue;
                if (j == 1) ordem = total;
                total++;
            }

            float x = (total <= 1) ? 0f : Mathf.Lerp(3.2f, -3.2f, ordem / (float)(total -1));

            return new Vector2(x, 6.0f);
        }

        /// <summary>
        /// Quem deste lado vai jogar a próxima bola - inclusive quando a posse ainda é do 
        /// adversário mas a bola já vem pra ca. Sem isso a IA só reage depois da travessia
        /// e chega sempre atrasada
        /// </summary>
        private int ResponsavelDoLado(int side)
        {
            if (Rally.TouchingSide == side) return ActiveIndex;

            if (HasPrediction && Court.SideOf(PredictedLanding.z) == side) return ClosestTo(TeamBase(side), PredictedLanding);

            return -1;
        }

        private void StepPlayers(float dt)
        {
            for (int i = 0; i < Players.Length; i++)
            {
                if (Players[i].BlockTimer > 0f)
                {
                    Players[i].BlockTimer -= dt;
                }

                UpdateAiBlock();
            }

            int controlado = ControlledIndex;
            int respA = ResponsavelDoLado(Court.SideA);
            int respB = ResponsavelDoLado(Court.SideB);

            for (int i = 0; i < Players.Length; i++)
            {
                int side = Players[i].Side;

                Players[i].Base = HomeFor(i);

                if (Players[i].BlockTimer > 0f)
                {
                    Players[i].Velocity = Vector3.zero;
                    continue;
                }
                
                bool defendeRede = !IsHuman[i] && HasNetCross && IsFront(i) && !Rally.ServeInFlight && EffectiveRole(i) != PlayerRole.Libero && Court.SideOf(Ball.Position.z) == -side;

                if (defendeRede)
                {
                    int atk = -side;
                    
                    float alvoX = (Rally.TouchCount >= 2 && NetCrossPoint.y > NetHeight)
                    ? NetCrossPoint.x 
                    : AttackSpotOf(atk).x;

                    alvoX += _desvioBloqueio[TeamIdx(side)];

                    int perto = FrontClosestToX(TeamBase(side), alvoX);

                    // o mais próximo vai no ponto; os outros fecham ao lado, formando parede
                    float x = (i == perto) ? alvoX : Mathf.Lerp(Players[i].Base.x, alvoX, 0.4f);

                    Vector3 posto = new Vector3(x, 0f, 0.8f * Players[i].Side);
                    
                    Players[i] = PlayerPhysics.StepToTarget(Players[i], Attrs[i], posto, dt);
                    
                    continue;
                }

                int resp = (side == Court.SideA) ? respA : respB;
                bool temPosse = (Rally.TouchingSide == side);
                Vector3 alvoBola = temPosse ? ContactPoint : PredictedLanding;
                bool vemPraCa = Court.SideOf(alvoBola.z) == side && (temPosse ? HasContact : HasPrediction);

                if (i == HumanIndex)
                {
                    Players[i] = PlayerPhysics.StepDirect(Players[i], Attrs[i], MoveInput, dt);
                } else if (i == resp && vemPraCa)
                {
                    Vector3 alvo = alvoBola;

                    if (!IsHuman[i])
                    {
                        AiProfile p = PerfilDe(side);

                        alvo = (_desdeToque < p.AtrasoLeitura) ? Players[i].Base : alvoBola + _leitura[TeamIdx(side)];
                    }

                    Players[i] = PlayerPhysics.StepToTarget(Players[i], Attrs[i], alvoBola, dt);
                } else
                {
                    Players[i] = PlayerPhysics.StepToTarget(Players[i], Attrs[i], Players[i].Base, dt);
                }
            }
        }
    }
}
