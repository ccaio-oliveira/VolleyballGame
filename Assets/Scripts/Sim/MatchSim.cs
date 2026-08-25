using System;
using UnityEngine;

namespace Volley.Sim
{
    public class MatchSim
    {
        // ---------- bola ----------
        public BallState Ball;
        public bool BallLive;
        public Vector3 PredictedLanding;
        public bool HasPrediction;

        // ---------- contato ----------
        public Vector3 ContactPoint;
        public bool HasContact;
        public float TimeToContact;
        public TouchWindow Window = TouchWindow.Default;
        public bool TouchArmed;
        public float ArmedQuality;

        // ---------- elenco ----------
        public PlayerState[] Players = new PlayerState[12];
        public PlayerAttributes[] Attrs = new PlayerAttributes[12];
        public bool[] IsHuman = new bool[12];
        public int[] Rotation = new int[2];
        public int HumanSide = Court.SideB;
        public float SpikeDepth = 5.0f;
        public float MaxSpikeError = 3.0f;
        public float MaxDisplacement = 3.0f;
        public Vector2 MoveInput;

        // ---------- alvos e tuning ----------
        public float MaxPassError = 3.5f;
        public float MaxSetError = 2.5f;

        public readonly RallyState Rally = new RallyState();
        public float NetHeight = Court.NetHeightMen;

        public event Action<string> OnLog;

        private BallState _prev;
        private int _resolvedTouch = -1;
        private int _resolvedSide;

        private int _controlled = 0;
        private int _controlTouch = -1;
        private int _controlSide;
        private readonly System.Random _rng = new System.Random(12345);
        public Vector3 NetCrossPoint;
        public float TimeToNet;
        public bool HasNetCross;

        public float BlockHalfWidth = 0.55f;
        public float BlockDuration = 0.65f;
        public float BlockZone = 1.2f;
        public float BlockHandSpan = 0.75f;

        public float SpikeAimRange = 3.6f;
        private Vector2 _armedAim;
        private static int TeamBase(int side) => side == Court.SideA ? 0 : 6;
        private static int TeamIdx(int side) => side == Court.SideA ? 0 : 1;

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
        public int ControlledIndex => _controlled;

        private void UpdateControl()
        {
            bool novaOportunidade = _controlTouch != Rally.TouchCount || _controlSide != Rally.TouchingSide;

            if (!novaOportunidade) return;

            _controlTouch = Rally.TouchCount;
            _controlSide = Rally.TouchingSide;

            _controlled = (Rally.TouchingSide == HumanSide) ? ActiveIndex : ClosestTo(TeamBase(HumanSide), PredictedLanding);
        }

        private static Vector3 SetterSpotOf(int side){
            Vector3 h = SetterHome(side);
            return new Vector3(h.x, 2.10f, h.z);
        }
        private static Vector3 AttackSpotOf(int side){
            Vector3 z4 = ZonePos(4, side);
            return new Vector3(z4.x, 3.00f, 1.2f * side);
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

        /// <summary>Onde o levantador joga: entre as zonas 2 e 3, colado na rede.</summary>
        private static Vector3 SetterHome(int side) => new Vector3(-1.5f * side, 0f, 1.6f * side);

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
        public PlayerRole EffectiveRole(int i) => (Players[i].Role == PlayerRole.Central && !IsFront(i)) ? PlayerRole.Libero : Players[i].Role;

        private static bool Recebe(PlayerRole r) => r == PlayerRole.Ponteiro || r == PlayerRole.Libero;

        private static readonly PlayerRole[] SlotRole =
        {
            PlayerRole.Levantador,   // 0  ─┐ diagonal
            PlayerRole.Ponteiro,     // 1  ─┼─┐
            PlayerRole.Central,      // 2  ─┼─┼─┐
            PlayerRole.Oposto,       // 3  ─┘ │ │
            PlayerRole.Ponteiro,     // 4  ───┘ │
            PlayerRole.Central,      // 5  ─────┘
        };

        // ================= saque =================
        public void Serve(Vector3 from, Vector3 velocity)
        {
            Ball = new BallState(from, velocity);
            BallLive = true;
            Rally.BeginServe(Court.SideOf(from.z));

            int recvSide = -Court.SideOf(from.z);

            SetupTeam(Court.SideA);
            SetupTeam(Court.SideB);

            TouchArmed = false;
            HasContact = false;
            _resolvedTouch = -1;
            TimeToContact = 99f;
            ContactPoint = from;
            _controlled = TeamBase(HumanSide);
            _controlTouch = -1;
            _controlSide = 0;

            OnLog?.Invoke($"saque do lado {SideName(Rally.TouchingSide)} " + $"de y={from.y:F2} a {velocity.magnitude:F1} m/s");
        }

        // ================= loop =================
        public void Tick(float dt)
        {
            if (!BallLive) return;

            _prev = Ball;
            Ball = BallPhysics.Step(Ball, dt);

            HasPrediction = BallPhysics.PredictLanding(Ball, Court.BallRadius, dt, 6f, out PredictedLanding, out _);

            if (BallPhysics.PredictLanding(Ball, ActiveContactHeight, dt, 6f, out Vector3 cp, out float tc))
            {
                ContactPoint = cp;
                TimeToContact = tc;
                HasContact = true;
                HasNetCross = BallPhysics.PredictNetCross(Ball, dt, out NetCrossPoint, out TimeToNet);
            } else
            {
                TimeToContact -= dt;
            }

            UpdateControl();
            StepPlayers(dt);

            // ORDEM IMPORTA: o levantador é avaliado ANTES do toque humano.
            // Se fosse depois, a recepção incrementaria TouchCount para 1 e o
            // levantador dispararia no mesmo tick, com a bola ainda na cintura.
            bool jaResolvido = (_resolvedTouch == Rally.TouchCount && _resolvedSide == Rally.TouchingSide);

            if (!jaResolvido && TimeToContact <= 0f)
            {
                int i = ActiveIndex;

                if (IsHuman[i])
                {
                    if (TouchArmed)
                    {
                        ResolveTouch(i, ArmedQuality);
                    }
                } else
                {
                    ResolveTouch(i, AiQuality(i));
                }
            }

            if (DetectNetCrossing()) return;
            if (DetectCeiling()) return;
            if (DetectGround()) return;
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

                if (i == controlado && IsHuman[i])
                {
                    Players[i] = PlayerPhysics.StepDirect(Players[i], Attrs[i], MoveInput, dt);
                } else if (i == resp && vemPraCa)
                {
                    Players[i] = PlayerPhysics.StepToTarget(Players[i], Attrs[i], alvoBola, dt);
                } else
                {
                    Players[i] = PlayerPhysics.StepToTarget(Players[i], Attrs[i], Players[i].Base, dt);
                }
            }
        }

        // ================= toques =================
        public void TryReceive()
        {
            if (!BallLive || TouchArmed) return;
            if (Rally.TouchingSide != Players[ActiveIndex].Side) return;
            if(!IsHuman[ActiveIndex]) return;
            if (!HasContact) 
            { 
                OnLog?.Invoke("sem ponto de contato");
                return;
            }

            float q = TouchTiming.Quality(TimeToContact, Window);

            if (q <= 0f)
            {
                OnLog?.Invoke($"fora da janela - {TimeToContact:+0.00;-0.00} s");
                return;
            }

            TouchArmed = true;
            ArmedQuality = q;
            _armedAim = MoveInput;

            OnLog?.Invoke($"{TouchTiming.Label(q)} q={q:F2} " + $"erro={TimeToContact:+0.00;-0.00}s");
        }

        public void TryBlock()
        {
            int i = ControlledIndex;
            if (!BallLive || !IsHuman[i]) return;
            if (Players[i].BlockTimer > 0f) return;

            if (Rally.ServeInFlight)
            {
                OnLog?.Invoke("não se bloqueia saque");
                return;
            }

            if (Mathf.Abs(Players[i].Position.z) > BlockZone)
            {
                OnLog?.Invoke("longe demais da rede pra bloquear");
                return;
            }

            Players[i].BlockTimer = BlockDuration;
            OnLog?.Invoke($"[#{i}] salta");
        }

        /// <summary>
        /// Qualidade da IA: quanto ela foi obrigada a sair da posição.
        /// </summary>
        private float AiQuality(int i)
        {
            float desloc = Vector2.Distance(
                new Vector2(Players[i].Position.x, Players[i].Position.z),
                new Vector2(Players[i].Base.x, Players[i].Base.z)
            );

            return 1f - Mathf.Clamp01(desloc / MaxDisplacement);
        }

        private void UpdateAiBlock()
        {
            if (!HasNetCross) return;
            if (NetCrossPoint.y <= NetHeight) return;
            if (Rally.ServeInFlight) return;

            int atkSide = Court.SideOf(Ball.Position.z);
            if (atkSide == 0) return;

            int defSide = -atkSide;
            if (defSide == HumanSide) return;
            if (TimeToNet >= 0.14f) return;

            int b = TeamBase(defSide);

            for (int k = 0; k < 6; k++)
            {
                int i = b + k;
                
                if (!IsFront(i)) continue;
                if (EffectiveRole(i) == PlayerRole.Libero) continue;
                if (Players[i].BlockTimer > 0f) continue;
                if (NetCrossPoint.y > Attrs[i].BlockReach) continue;
                if (Mathf.Abs(NetCrossPoint.x - Players[i].Position.x) > BlockHalfWidth) continue;

                Players[i].BlockTimer = BlockDuration;
                OnLog?.Invoke($"[#{i}] IA salta");
            }
        }

        private void ResolveTouch(int i, float q)
        {
            TouchArmed = false;
            _resolvedTouch = Rally.TouchCount;
            _resolvedSide = Rally.TouchingSide;

            Vector2 flat = new Vector2(Ball.Position.x - Players[i].Position.x, Ball.Position.z - Players[i].Position.z);

            if (flat.magnitude > Attrs[i].Reach)
            {
                OnLog?.Invoke($"[#{i} {Players[i].Role}] não alcançou - {flat.magnitude:F2} m");
                return;
            }

            int side = Players[i].Side;

            switch (Rally.TouchCount)
            {
                case 0:
                    {
                        Vector2 d = RandomInCircle(MaxPassError * (1f - q));
                        Vector3 alvo = SetterSpotOf(side) + new Vector3(d.x, 0f, d.y);
                        LaunchTo(KeepOffNet(alvo, side), 65f, i, $"passe q={q:F2} {d.magnitude:F2} m do levantador");
                        break;
                    }
                case 1:
                    {
                        Vector2 d = RandomInCircle(MaxSetError * (1f - q));
                        Vector3 alvo = AttackSpotOf(side) + new Vector3(d.x, 0f, d.y);
                        LaunchTo(KeepOffNet(alvo, side), 70f, i, $"levantamento q={q:F2} {d.magnitude:F2} m do alvo");
                        break;
                    }
                default:
                    {
                        Vector2 mira = IsHuman[i] ? _armedAim : AiAim(i);
                        float baseX = Mathf.Clamp(mira.x * SpikeAimRange, -3.8f, 3.8f);

                        Vector2 d = RandomInCircle(MaxSpikeError * (1f - q));
                        Vector3 alvo = new Vector3(baseX + d.x, Court.BallRadius, (-SpikeDepth * side) + d.y);

                        float folga = Mathf.Lerp(0.60f, 0.10f, q);

                        if (BallPhysics.SolveFlattestLegal(Ball.Position, alvo, NetHeight, folga, 1f / 60f, out Vector3 v, -35f, 40f, 1f))
                        {
                            float ang = Mathf.Asin(v.normalized.y) * Mathf.Rad2Deg;
                            Ball = new BallState(Ball.Position, v);
                            Rally.OnTouch(i, side);
                            OnLog?.Invoke($"ATAQUE q={q:F2} {v.magnitude:F1} m/s a {ang:F0}º" + $" alvo x={baseX:F1} [toque {Rally.TouchCount}/3]");
                        } else
                        {
                            OnLog?.Invoke("sem angulo legal pro ataque");
                        }
                        break;
                    }
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

            return new Vector2(melhorX / SpikeAimRange, 0f);
        }

        /// <summary>Levantamento nunca é colocado em cima da rede nem do outro lado.</summary>
        private static Vector3 KeepOffNet(Vector3 alvo, int side)
        {
            const float minOff = 0.6f;
            if (Court.SideOf(alvo.z) != side || Mathf.Abs(alvo.z) < minOff)
            {
                alvo.z = minOff * side;
            }

            return alvo;
        }

        /// <summary>Resolve a balística de um toque e registra na máquina de estados.</summary>
        private void LaunchTo(Vector3 alvo, float angle, int playerIndex, string msg)
        {
            if (BallPhysics.SolveLaunch(Ball.Position, alvo, angle, 1f / 60f, out Vector3 v))
            {
                Ball = new BallState(Ball.Position, v);
                Rally.OnTouch(playerIndex, Players[playerIndex].Side);
                OnLog?.Invoke($"{msg} [toque {Rally.TouchCount}/3]");
            } else
            {
                OnLog?.Invoke($"solver falhou: de y={Ball.Position.y:F2} para {alvo}");
            }
        }

        private Vector2 RandomInCircle(float radius)
        {
            double ang = _rng.NextDouble() * System.Math.PI * 2.0;
            double r = radius * System.Math.Sqrt(_rng.NextDouble());
            return new Vector2((float)(r * System.Math.Cos(ang)), (float)(r * System.Math.Sin(ang)));
        }

        // ================= detecção =================
        private bool DetectNetCrossing()
        {
            float z0 = _prev.Position.z;
            float z1 = Ball.Position.z;

            bool crossed = (z0 < 0f && z1 >= 0f) || (z0 > 0f && z1 <= 0f);
            if (!crossed) return false;

            float f = -z0 / (z1 - z0);
            Vector3 cross = Vector3.Lerp(_prev.Position, Ball.Position, f);

            if (cross.y < NetHeight)
            {
                EndRally(-Rally.LastTouchSide, $"na rede (y={cross.y:F2})");
                return true;
            }

            if (Mathf.Abs(cross.x) > Court.HalfWidth)
            {
                EndRally(-Rally.LastTouchSide, $"fora das antenas (x={cross.x:F2})");
                return true;
            }

            int defSide = Court.SideOf(z1);
            int blocker = Rally.ServeInFlight ? -1 : FindBlocker(cross, defSide);

            if (blocker >= 0)
            {
                ApplyBlock(blocker, cross, defSide);
                return false;
            }

            Rally.OnNetCrossed(Court.SideOf(z1));
            OnLog?.Invoke($"cruzou a rede a {cross.y:F2} m -> " + $"posse do lado {SideName(Rally.TouchingSide)}");
            return false;
        }

        private bool DetectCeiling()
        {
            if (_prev.Position.y < Court.Ceiling && Ball.Position.y >= Court.Ceiling)
            {
                EndRally(-Rally.LastTouchSide, "bateu no teto");
                return true;
            }
            return false;
        }

        private bool DetectGround()
        {
            float y0 = _prev.Position.y;
            float y1 = Ball.Position.y;

            if (!(y0 > Court.BallRadius && y1 <= Court.BallRadius)) return false;

            float denom = y0 - y1;
            float f = denom > 1e-6f ? (y0 - Court.BallRadius) / denom : 0f;
            Vector3 p = Vector3.Lerp(_prev.Position, Ball.Position, f);

            if (Court.IsInBounds(p))
                EndRally(-Court.SideOf(p.z), $"quicou no lado {SideName(Court.SideOf(p.z))} " + $"em x={p.x:F2} z={p.z:F2}");
            else
                EndRally(-Rally.LastTouchSide, $"fora em x={p.x:F2} z={p.z:F2}");

            return true;
        }

        private void EndRally(int winnerSide, string reason)
        {
            BallLive      = false;
            HasPrediction = false;
            
            bool viraSaque = (winnerSide != Rally.ServingSide);

            Rally.AwardPoint(winnerSide, reason);

            if (viraSaque) Rotate(winnerSide);

            OnLog?.Invoke($"PONTO {SideName(winnerSide)} — {reason}   |   " + $"A {Rally.ScoreA} x {Rally.ScoreB} B");
        }

        private static string SideName(int side) => side == Court.SideA ? "A" : "B";
        private void SetupTeam(int side)
        {
            int b = TeamBase(side);

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
                IsHuman[i] = (side == HumanSide);
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

        private int FindBlocker(Vector3 cross, int defSide)
        {
            int b = TeamBase(defSide);

            for (int k = 0; k < 6; k++)
            {
                int i = b + k;
                if (Players[i].BlockTimer <= 0f) continue;
                if (!IsFront(i)) continue;

                float topo = Attrs[i].BlockReach;
                float baixo = Mathf.Max(NetHeight, topo - BlockHandSpan);

                if (Mathf.Abs(cross.x - Players[i].Position.x) <= BlockHalfWidth && cross.y >= baixo && cross.y <= topo)
                {
                    return i;
                }
            }

            return -1;
        }

        private void ApplyBlock(int i, Vector3 cross, int defSide)
        {
            int atkSide = -defSide;

            // margem 1 = bola passou rente à fita, mãos muito acima -> murro
            // margem 0 = bola passou no topo do alcance -> raspão
            float margem = (Attrs[i].BlockReach - cross.y) / BlockHandSpan;

            Rally.OnBlockTouch(i, defSide);

            float speed = Ball.Velocity.magnitude;
            Vector3 pos = cross;

            if (margem > 0.55f)
            {
                // BLOQUEIO: desce cravado do lado de quem atacou
                pos.z = 0.05f * atkSide;
                Vector3 v = new Vector3(Ball.Velocity.x * 0.25f, -speed * 0.30f, -Ball.Velocity.z * 0.28f);

                Ball = new BallState(pos, v);
                Rally.OnNetCrossed(atkSide);

                OnLog?.Invoke($"BLOQUEIO #{i} margem={margem:F2} " + $"a bola volta pro lado {SideName(atkSide)}");
            } else
            {
                // RASPÃO: passa, mas lenta e alta - vira bola defensável
                pos.z = 0.05f * defSide;
                Vector3 v = Ball.Velocity * 0.42f;
                v.y = Mathf.Abs(v.y) + 1.6f;

                Ball = new BallState(pos, v);
                Rally.OnNetCrossed(defSide);

                OnLog?.Invoke($"raspão no bloqueio #{i} margem={margem:F2} " + $"lado {SideName(defSide)} com 3 toques");
            }
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
            Rotation[t] = (Rotation[t] + 1) & 6;
            OnLog?.Invoke($"rodízio do lado {SideName(side)}");
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
                case PlayerRole.Levantador: return new Vector2(-1.2f, 1.5f);
                case PlayerRole.Central: return new Vector2(0.4f, 1.3f);
                case PlayerRole.Oposto: return IsFront(i) ? new Vector2(-3.3f, 1.6f) : new Vector2(-3.4f, 7.2f);
                default: return LinhaDeRecepcao(i);
            }
        }

        private Vector2 AtaqueHome(int i)
        {
            switch (EffectiveRole(i))
            {
                case PlayerRole.Levantador: return new Vector2(-1.2f, 1.5f);
                case PlayerRole.Central: return new Vector2(0.4f, 1.5f);
                case PlayerRole.Libero: return new Vector2(0.5f, 4.0f);
                case PlayerRole.Oposto: return IsFront(i) ? new Vector2(-3.4f, 2.2f) : new Vector2(-3.4f, 4.2f);
                default: return IsFront(i) ? new Vector2(3.6f, 2.6f) : new Vector2(3.2f, 4.4f);
            }
        }

        private Vector2 DefesaHome(int i)
        {
            int z = ZoneOf(i);

            // linha de frente sobe pra rede pra bloquear - libero nunca bloqueia
            if (IsFront(i) && EffectiveRole(i) != PlayerRole.Libero)
            {
                float x = (z == 4) ? 3.0f : (z == 3) ? 0.0f : -3.0f;
                return new Vector2(x, 0.9f);
            }

            if (z == 1) return new Vector2(-3.4f, 6.6f);
            if (z == 5) return new Vector2(3.4f, 6.6f);
            if (z == 6) return new Vector2(0.0f, 8.0f); // o 6 cobre o fundo

            return new Vector2(0.0f, 6.5f);
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

            float x = (total <= 1) ? 0f : Mathf.Lerp(3.4f, -3.4f, ordem / (float)(total -1));

            return new Vector2(x, 6.2f);
        }
    }

}