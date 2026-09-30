using System;
using UnityEngine;

namespace Volley.Sim
{
    /// <summary>
    /// Match simulation: owns the ball, the players and the rally lifecycle.
    /// Pure C# with no engine dependencies beyond math types, split into partial
    /// files by responsibility:
    ///   MatchSim.cs           state, serve, tick, scoring
    ///   MatchSim.Roster.cs    rotation, zones, roles, who plays the ball
    ///   MatchSim.Positions.cs functional homes per phase and player movement
    ///   MatchSim.Touches.cs   human actions and touch resolution
    ///   MatchSim.Detection.cs net, ceiling, floor and block detection
    ///   MatchSim.Ai.cs        AI decisions and difficulty
    /// </summary>
    public partial class MatchSim
    {
        // ---------- ball ----------
        public BallState Ball;
        public bool BallLive;
        public Vector3 PredictedLanding;
        public bool HasPrediction;

        // ---------- contact prediction (drives positioning) ----------
        public Vector3 ContactPoint;
        public bool HasContact;
        public float TimeToContact;

        // ---------- human touch armed by a button press ----------
        public TouchWindow Window = TouchWindow.Default;
        public bool TouchArmed;
        public float ArmedQuality;
        public float ArmedTimer;
        public int ArmedIndex;
        private Vector2 _armedAim;

        // ---------- net ----------
        public float NetHeight = Court.NetHeightMen;
        public Vector3 NetCrossPoint;
        public float TimeToNet;
        public bool HasNetCross;

        // ---------- roster ----------
        public PlayerState[] Players = new PlayerState[12];      // 0-5 side A, 6-11 side B
        public PlayerAttributes[] Attrs = new PlayerAttributes[12];
        public bool[] IsHuman = new bool[12];
        public int[] Rotation = new int[2];                      // 0 = side A, 1 = side B

        // ---------- human player ----------
        public int HumanSide = Court.SideB;
        public PlayerRole HumanRole = PlayerRole.MiddleBlocker;
        public int HumanRoleIndex = 1;                           // which of two same-role players
        public int HumanIndex { get; private set; }
        public Vector2 MoveInput;

        // ---------- game state ----------
        public readonly RallyState Rally = new RallyState();
        public readonly MatchState Match = new MatchState();

        // ---------- serve ----------
        public Vector3 ServeOrigin = new Vector3(0f, 2.70f, 9.5f); // z is mirrored per side
        public float ServeTargetX = 0f;
        public float ServeTargetZ = 6.5f;
        public float NetClearance = 0.25f;
        public float NextServeDelay = 1.6f;
        private float _serveTimer;

        // ---------- block ----------
        public float BlockHalfWidth = 0.55f;
        public float BlockDuration = 0.65f;
        public float BlockHandSpan = 0.75f;                      // hands + forearms

        // ---------- jump ----------
        public float JumpDirectionBoost = 1.6f;                  // how much the stick tilts a jump

        // ---------- touch tuning ----------
        public float MaxPassError = 3.5f;
        public float MaxSetError = 2.5f;
        public float SpikeDepth = 5.0f;
        public float MaxSpikeError = 3.0f;
        public float SpikeAimRange = 3.6f;
        public float MaxDisplacement = 3.0f;        // set/attack: home displacement that zeroes quality
        public float EdgeOfReachQuality = 0.30f;    // first touch: quality with the ball at the edge of reach

        // ---------- output ----------
        public event Action<string> OnLog;

        /// <summary>Semantic game events: kind, where it happened and how strong it was (0..1).</summary>
        public event Action<SimEventKind, Vector3, float> OnEvent;

        // ---------- internal ----------
        private BallState _previousBall;
        private int _resolvedTouch = -1;
        private int _resolvedSide;
        private readonly System.Random _rng = new System.Random(12345);

        // ================= serve =================

        public void Serve(Vector3 from, Vector3 velocity)
        {
            Ball = new BallState(from, velocity);
            BallLive = true;
            Rally.BeginServe(Court.SideOf(from.z));

            SetupTeam(Court.SideA);
            SetupTeam(Court.SideB);

            TouchArmed = false;
            HasContact = false;
            _resolvedTouch = -1;
            TimeToContact = 99f;
            ContactPoint = from;
            _serveTimer = 0f;

            RollAiReads();

            OnLog?.Invoke($"você: {Players[HumanIndex].Role} na zona {ZoneOf(HumanIndex)} " + $"({(IsFront(HumanIndex) ? "frente" : "fundo")})");
            OnLog?.Invoke($"saque do lado {SideName(Rally.TouchingSide)} " + $"de y={from.y:F2} a {velocity.magnitude:F1} m/s");
            OnEvent?.Invoke(SimEventKind.Serve, from, Mathf.Clamp01(velocity.magnitude / 25f));
        }

        /// <summary>Solves and launches a serve from <paramref name="side"/> toward the current serve target.</summary>
        public bool ServeNow(int side)
        {
            Vector3 from = new Vector3(ServeOrigin.x, ServeOrigin.y, Mathf.Abs(ServeOrigin.z) * side);
            Vector3 target = new Vector3(ServeTargetX, Court.BallRadius, Mathf.Abs(ServeTargetZ) * -side);

            if (!BallPhysics.SolveFlattestLegal(from, target, NetHeight, NetClearance, 1f / 60f, out Vector3 velocity))
            {
                OnLog?.Invoke("nenhum ângulo legal para esse alvo de saque");
                return false;
            }

            Serve(from, velocity);
            return true;
        }

        // ================= loop =================

        public void Tick(float dt)
        {
            if (!BallLive)
            {
                TickBetweenRallies(dt);
                return;
            }

            _previousBall = Ball;
            _timeSinceTouch += dt;
            Ball = BallPhysics.Step(Ball, dt);

            HasPrediction = BallPhysics.PredictLanding(Ball, Court.BallRadius, dt, 6f, out PredictedLanding, out _);
            UpdateContactPrediction(dt);

            StepPlayers(dt);
            TickArmedTouch(dt);

            if (DetectNetCrossing()) return;
            if (DetectCeiling()) return;
            if (DetectGround()) return;
        }

        private void TickBetweenRallies(float dt)
        {
            if (Match.Finished) return;

            _serveTimer += dt;

            // the AI serves on its own; the human serve stays on the button
            if (_serveTimer >= NextServeDelay && Rally.ServingSide != HumanSide)
            {
                ServeTargetX = (float)(_rng.NextDouble() * 7.0 - 3.5);
                ServeTargetZ = (float)(_rng.NextDouble() * 4.0 + 4.5);
                ServeNow(Rally.ServingSide);
            }
        }

        private void UpdateContactPrediction(float dt)
        {
            float phaseHeight = PhaseContactHeight();
            bool found = BallPhysics.PredictLanding(Ball, phaseHeight, dt, 6f, out Vector3 point, out float time);

            // A ball that never rises to the phase height (low set, desperate dig):
            // the team reorganizes to play it from the floor instead of running to a
            // point the ball will never occupy.
            if (!found && phaseHeight > 0.95f)
                found = BallPhysics.PredictLanding(Ball, 0.90f, dt, 6f, out point, out time);

            if (found)
            {
                ContactPoint = point;
                TimeToContact = time;
                HasContact = true;
            }
            else
            {
                TimeToContact -= dt;
            }
        }

        /// <summary>The armed human touch counts down on its own clock, independent of any global prediction.</summary>
        private void TickArmedTouch(float dt)
        {
            if (!TouchArmed) return;

            ArmedTimer -= dt;
            if (ArmedTimer > 0f) return;

            TouchArmed = false;
            ResolveTouch(ArmedIndex, ArmedQuality);
        }

        /// <summary>
        /// Height at which the current phase plays the ball. Depends only on
        /// Rally.TouchCount — never on ActiveIndex, or the circular dependency returns.
        /// </summary>
        private float PhaseContactHeight()
        {
            switch (Rally.TouchCount)
            {
                case 1:  return 2.10f;   // set, above the head
                case 2:  return 3.05f;   // attack, hand at the top of the jump
                default: return 0.90f;   // forearm pass
            }
        }

        // ================= ball trajectory =================

        /// <summary>
        /// The only way to change the ball's trajectory. Every prediction derived from
        /// the old trajectory dies here: whoever changes the ball invalidates its dependents.
        /// </summary>
        public void SetBallTrajectory(Vector3 position, Vector3 velocity)
        {
            Ball = new BallState(position, velocity);
            HasContact = false;
            TimeToContact = 99f;
            HasPrediction = false;
            HasNetCross = false;

            RollAiReads();
        }

        // ================= scoring =================

        private void EndRally(int winnerSide, string reason)
        {
            BallLive = false;
            HasPrediction = false;

            // rally point: only the team that was receiving rotates when it wins
            bool sideOut = (winnerSide != Rally.ServingSide);
            Rally.AwardPoint(winnerSide, reason);
            if (sideOut) Rotate(winnerSide);

            RallyOutcome outcome = Match.AddPoint(winnerSide);

            string score = $"{Match.PointsOf(Court.SideA)} X {Match.PointsOf(Court.SideB)}";
            string sets = $"{Match.SetsOf(Court.SideA)} - {Match.SetsOf(Court.SideB)}";

            switch (outcome)
            {
                case RallyOutcome.Point:
                    OnLog?.Invoke($"PONTO {SideName(winnerSide)} - {reason} | {score} sets {sets}");
                    OnEvent?.Invoke(SimEventKind.Point, Vector3.zero, 1f);
                    break;

                case RallyOutcome.SetWon:
                    Rotation[0] = Rotation[1] = 0;   // lineup resets every set
                    OnLog?.Invoke($"=== SET {SideName(winnerSide)} - sets {sets}, " + $"vai pro set {Match.SetNumber} (até {Match.PointsToWin}) ===");
                    OnEvent?.Invoke(SimEventKind.SetWon, Vector3.zero, 1f);
                    break;

                case RallyOutcome.MatchWon:
                    OnLog?.Invoke($"=== PARTIDA PARA {SideName(winnerSide)} - sets {sets} ===");
                    OnEvent?.Invoke(SimEventKind.MatchWon, Vector3.zero, 1f);
                    break;
            }
        }

        // ================= helpers =================

        private static int TeamBase(int side) => side == Court.SideA ? 0 : 6;

        private static int TeamIndex(int side) => side == Court.SideA ? 0 : 1;

        private static string SideName(int side) => side == Court.SideA ? "A" : "B";

        private static float HorizontalDistance(Vector3 a, Vector3 b)
            => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>Uniform point inside a disc. The sqrt keeps samples from bunching at the center.</summary>
        private Vector2 RandomInCircle(float radius)
        {
            double angle = _rng.NextDouble() * Math.PI * 2.0;
            double distance = radius * Math.Sqrt(_rng.NextDouble());
            return new Vector2((float)(distance * Math.Cos(angle)), (float)(distance * Math.Sin(angle)));
        }
    }
}
