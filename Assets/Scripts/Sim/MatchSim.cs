using System;
using UnityEngine;

namespace Volley.Sim
{
    /// <summary>Simulação da partida. Estado, ciclo de vida do rally e placar.</summary>
    public partial class MatchSim
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
        private Vector2 _armedAim;

        // ---------- rede ----------
        public Vector3 NetCrossPoint;
        public float TimeToNet;
        public bool HasNetCross;
        public float NetHeight = Court.NetHeightMen;

        // ---------- elenco ----------
        public PlayerState[] Players = new PlayerState[12];
        public PlayerAttributes[] Attrs = new PlayerAttributes[12];
        public bool[] IsHuman = new bool[12];
        public int[] Rotation = new int[2];
        public int HumanSide = Court.SideB;
        public PlayerRole HumanRole = PlayerRole.Central;
        public int HumanRoleIndex = 1;
        public int HumanIndex { get; private set; }
        public Vector2 MoveInput;

        // ---------- estado do jogo ----------
        public readonly RallyState Rally = new RallyState();
        public readonly MatchState Match = new MatchState();

        // ---------- saque ----------
        public Vector3 ServeOrigin = new Vector3(0f, 2.70f, 9.5f);
        public float ServeTargetX = 0f;
        public float ServeTargetZ = 6.5f;
        public float NetClearance = 0.25f;
        public float NextServeDelay = 1.6f;
        private float _serveTimer;

        // ---------- bloqueio ----------
        public float BlockHalfWidth = 0.55f;
        public float BlockDuration = 0.65f;
        public float BlockZone = 1.2f;
        public float BlockHandSpan = 0.75f;

        // ---------- tuning dos toques ----------
        public float MaxPassError = 3.5f;
        public float MaxSetError = 2.5f;
        public float SpikeDepth = 5.0f;
        public float MaxSpikeError = 3.0f;
        public float SpikeAimRange = 3.6f;
        public float MaxDisplacement = 3.0f;

        // ---------- saída ----------
        public event Action<string> OnLog;

        // ---------- internos ----------
        private BallState _prev;
        private int _resolvedTouch = -1;
        private int _resolvedSide;
        private readonly System.Random _rng = new System.Random(12345);

        // ---------- índices de time ----------
        private static int TeamBase(int side) => side == Court.SideA ? 0 : 6;
        private static int TeamIdx(int side) => side == Court.SideA ? 0 : 1;

        // ---------- ciclo de vida ----------

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
            _serveTimer = 0f;

            NovaLeitura();

            OnLog?.Invoke($"você: {Players[HumanIndex].Role} na zona {ZoneOf(HumanIndex)} " + $"({(IsFront(HumanIndex) ? "frente" : "fundo")})");
            OnLog?.Invoke($"saque do lado {SideName(Rally.TouchingSide)} " + $"de y={from.y:F2} a {velocity.magnitude:F1} m/s");
        }

        public bool ServeNow(int side)
        {
            Vector3 from = new Vector3(ServeOrigin.x, ServeOrigin.y, Mathf.Abs(ServeOrigin.z) * side);
            Vector3 alvo = new Vector3(ServeTargetX, Court.BallRadius, Mathf.Abs(ServeTargetZ) * -side);

            if (!BallPhysics.SolveFlattestLegal(from, alvo, NetHeight, NetClearance, 1f / 60f, out Vector3 v))
            {
                OnLog?.Invoke("nenhum ângulo legal para esse alvo de saque");
                return false;
            }

            Serve(from, v);
            return true;
        }

        // ================= loop =================
        public void Tick(float dt)
        {
            if (!BallLive)
            {
                if (Match.Finished) return;

                _serveTimer += dt;

                // a IA saca sozinha; o seu saque continua no botão
                if (_serveTimer >= NextServeDelay && Rally.ServingSide != HumanSide)
                {
                    ServeTargetX = (float)(_rng.NextDouble() * 7.0 - 3.5);
                    ServeTargetZ = (float)(_rng.NextDouble() * 4.0 + 4.5);
                    ServeNow(Rally.ServingSide);
                }
                return;
            }

            _prev = Ball;
            _desdeToque += dt;
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

            StepPlayers(dt);

            // ORDEM IMPORTA: o levantador é avaliado ANTES do toque humano.
            // Se fosse depois, a recepção incrementaria TouchCount para 1 e o
            // levantador dispararia no mesmo tick, com a bola ainda na cintura.
            bool jaResolvido = (_resolvedTouch == Rally.TouchCount && _resolvedSide == Rally.TouchingSide);

            if (!jaResolvido && TimeToContact <= 0f)
            {
                if (Rally.TouchCount >= 3)
                {
                    EndRally(-Rally.TouchingSide, "quatro toques");
                    return;
                }

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

        private void EndRally(int winnerSide, string reason)
        {
            BallLive      = false;
            HasPrediction = false;
            
            bool viraSaque = (winnerSide != Rally.ServingSide);
            Rally.AwardPoint(winnerSide, reason);
            if (viraSaque) Rotate(winnerSide);

            RallyOutcome r = Match.AddPoint(winnerSide);

            string placar = $"{Match.PointsOf(Court.SideA)} X {Match.PointsOf(Court.SideB)}";
            string sets = $"{Match.SetsOf(Court.SideA)} - {Match.SetsOf(Court.SideB)}";

            switch (r)
            {
                case RallyOutcome.Ponto:
                    {
                        OnLog?.Invoke($"PONTO {SideName(winnerSide)} - {reason} | {placar} sets {sets}");
                        break;
                    }
                case RallyOutcome.Set:
                    {
                        Rotation[0] = Rotation[1] = 0;
                        OnLog?.Invoke($"=== SET {SideName(winnerSide)} - sets {sets}, " + $"vai pro set {Match.SetNumber} (até {Match.PointsToWin}) ===");
                        break;
                    }
                case RallyOutcome.Partida:
                    {
                        OnLog?.Invoke($"=== PARTIDA PARA {SideName(winnerSide)} - sets {sets} ===");
                        break;
                    }
            }
        }

        private static string SideName(int side) => side == Court.SideA ? "A" : "B";

        private Vector2 RandomInCircle(float radius)
        {
            double ang = _rng.NextDouble() * System.Math.PI * 2.0;
            double r = radius * System.Math.Sqrt(_rng.NextDouble());
            return new Vector2((float)(r * System.Math.Cos(ang)), (float)(r * System.Math.Sin(ang)));
        }
    }
}
