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
        public PlayerState[] Players = new PlayerState[6];
        public PlayerAttributes[] Attrs = new PlayerAttributes[6];
        public bool[] IsHuman = new bool[6];
        public int HumanSide = Court.SideB;
        public float SpikeX = 2.0f;
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

        private static int TeamBase(int side) => side == Court.SideA ? 0 : 3;

        /// <summary>
        /// Quem joga a próxima bola: no 1º toque quem estiver mais perto de onde a bola vai cair; depois o levantador e o atacante do time.
        /// </summary>
        public int ActiveIndex
        {
            get
            {
                int b = TeamBase(Rally.TouchingSide);

                if (Rally.TouchCount == 0) return ClosestTo(b, ContactPoint, Rally.LastToucher);

                int titular = b + Mathf.Clamp(Rally.TouchCount, 1, 2);
                if (titular != Rally.LastToucher) return titular;

                return ClosestTo(b, ContactPoint, Rally.LastToucher);
            }
        }

        private int ClosestTo(int b, Vector3 p, int exclude = -1)
        {
            int best = -1;
            float bestD = float.MaxValue;

            for (int k = 0; k < 3; k++)
            {
                int idx = b + k;
                if (idx == exclude) continue;

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

            return best < 0 ? b : best;
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

        private static Vector3 SetterSpotOf(int side) => new Vector3(1.5f, 2.10f, 2.0f * side);
        private static Vector3 AttackSpotOf(int side) => new Vector3(-3.0f, 3.00f, 1.2f * side);


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

        private void StepPlayers(float dt)
        {
            int ativo = ActiveIndex;
            int controlado = ControlledIndex;

            for (int i = 0; i < Players.Length; i++)
            {
                if (Players[i].BlockTimer > 0f)
                {
                    Players[i].BlockTimer -= dt;
                }

                UpdateAiBlock();
            }

            for (int i = 0; i < Players.Length; i++)
            {
                bool ownSide = Court.SideOf(ContactPoint.z) == Players[i].Side;

                if (Players[i].BlockTimer > 0f)
                {
                    Players[i].Velocity = Vector3.zero;
                    continue;
                }

                bool naRede = !IsHuman[i] && HasNetCross && Court.SideOf(Ball.Position.z) == -Players[i].Side && i == TeamBase(Players[i].Side) + 2;

                if (naRede)
                {
                    int atk = -Players[i].Side;

                    // fase 1: antes do ataque, posta-se onde o levantamento vai cair
                    // fase 2: ataque no ar e vindo por cima da fita, desliza pro ponto real
                    float alvoX = (Rally.TouchCount >= 2 && HasNetCross && NetCrossPoint.y > NetHeight)
                    ? NetCrossPoint.x
                    : AttackSpotOf(atk).x;

                    Vector3 posto = new Vector3(alvoX, 0f, 0.8f * Players[i].Side);
                    Players[i] = PlayerPhysics.StepToTarget(Players[i], Attrs[i], posto, dt);
                    continue;
                }

                if (i == controlado && IsHuman[i])
                {
                    Players[i] = PlayerPhysics.StepDirect(Players[i], Attrs[i], MoveInput, dt);
                } else if (i == ativo && HasContact && ownSide)
                {
                    Players[i] = PlayerPhysics.StepToTarget(Players[i], Attrs[i], ContactPoint, dt);
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

            OnLog?.Invoke($"{TouchTiming.Label(q)} q={q:F2} " + $"erro={TimeToContact:+0.00;-0.00}s");
        }

        public void TryBlock()
        {
            int i = ControlledIndex;
            if (!BallLive || !IsHuman[i]) return;
            if (Players[i].BlockTimer > 0f) return;

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

            int atkSide = Court.SideOf(Ball.Position.z);
            if (atkSide == 0) return;

            int defSide = -atkSide;
            if (defSide == HumanSide) return;

            int i = TeamBase(defSide) + 2;

            if (
                Players[i].BlockTimer <= 0f
                && TimeToNet < 0.14f
                && NetCrossPoint.y > NetHeight
                && NetCrossPoint.y < Attrs[i].BlockReach
                && Mathf.Abs(NetCrossPoint.x - Players[i].Position.x) < BlockHalfWidth + 0.3f
            )
            {
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
                        Vector2 d = RandomInCircle(MaxSpikeError * (1f - q));
                        Vector3 alvo = new Vector3(SpikeX + d.x, Court.BallRadius, (-SpikeDepth * side) + d.y);

                        float folga = Mathf.Lerp(0.60f, 0.10f, q);

                        if (BallPhysics.SolveFlattestLegal(Ball.Position, alvo, NetHeight, folga, 1f / 60f, out Vector3 v, -35f, 40f, 1f))
                        {
                            float ang = Mathf.Asin(v.normalized.y) * Mathf.Rad2Deg;
                            Ball = new BallState(Ball.Position, v);
                            Rally.OnTouch(i, side);
                            OnLog?.Invoke($"ATAQUE q={q:F2} {v.magnitude:F1} m/s a {ang:F0}º" + $" [toque {Rally.TouchCount}/3]");
                        } else
                        {
                            OnLog?.Invoke("sem angulo legal pro ataque");
                        }
                        break;
                    }
            }
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
            int blocker = FindBlocker(cross, defSide);

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
            Rally.AwardPoint(winnerSide, reason);

            OnLog?.Invoke($"PONTO {SideName(winnerSide)} — {reason}   |   " + $"A {Rally.ScoreA} x {Rally.ScoreB} B");
        }

        private static string SideName(int side) => side == Court.SideA ? "A" : "B";
        private void SetupTeam(int side)
        {
            int b = TeamBase(side);

            Vector3 baseP = new Vector3( 0.0f, 0f, 5.0f * side);
            Vector3 baseL = new Vector3( 1.5f, 0f, 2.0f * side);
            Vector3 baseA = new Vector3(-3.0f, 0f, 3.5f * side);

            Players[b + 0] = new PlayerState { Id = b + 0, Side = side, Role = PlayerRole.Passador,   Position = baseP, Base = baseP };
            Players[b + 1] = new PlayerState { Id = b + 1, Side = side, Role = PlayerRole.Levantador, Position = baseL, Base = baseL };
            Players[b + 2] = new PlayerState { Id = b + 2, Side = side, Role = PlayerRole.Atacante,   Position = baseA, Base = baseA };

            for (int k = 0; k < 3; k++)
            {
                Attrs[b + k]   = PlayerAttributes.Default;
                IsHuman[b + k] = (side == HumanSide) && k != 1;
            }

            Attrs[b + 2].ReachHeight = 3.2f;
        }

        private int FindBlocker(Vector3 cross, int defSide)
        {
            int b = TeamBase(defSide);

            for (int k = 0; k < 3; k++)
            {
                int i = b + k;
                if (Players[i].BlockTimer <= 0f) continue;

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
    }

}