using System;
using UnityEngine;

namespace Volley.Sim
{
    /// <summary>Orquestra a simulação: integra a bola, detecta eventos, resolve pontos.>/summary>
    public class MatchSim
    {
        public BallState Ball;
        public bool BallLive;
        public Vector3 PredictedLanding;
        public bool HasPrediction;

        public readonly RallyState Rally = new RallyState();

        public float NetHeight = Court.NetHeightMen;

        /// <summary>Canal de saída do Sim. Quem quiser logar, assina.</summary>
        public event Action<string> OnLog;

        private BallState _prev;

        public PlayerState Receiver;
        public PlayerAttributes ReceiverAttr = PlayerAttributes.Default;
        public bool ReceiverReached;
        public float ContactHeight = 0.9f;
        public float TimeToContact;
        public Vector3 ContactPoint;
        public TouchWindow Window = TouchWindow.Default;
        public float MaxPassError = 3.5f;
        public Vector3 SetterSpot;
        public bool HasContact;
        public bool TouchArmed;
        public float ArmedQuality;
        public Vector2 MoveInput;
        public bool AutoPosition = false;

        private readonly System.Random _rng = new System.Random(12345);

        public void Serve(Vector3 from, Vector3 velocity)
        {
            Ball = new BallState(from, velocity);
            BallLive = true;
            Rally.BeginServe(Court.SideOf(from.z));
            OnLog?.Invoke($"saque do lado {SideName(Rally.TouchingSide)} " + $"de y={from.y:F2} a {velocity.magnitude:F1} m/s");

            int recvSide = -Court.SideOf(from.z);
            Receiver = new PlayerState
            {
                Id = 0,
                Side = recvSide,
                Position = new Vector3(0f, 0f, 5f * recvSide),
                Velocity = Vector3.zero
            };

            SetterSpot = new Vector3(1.5f, 2.2f, 2.0f * recvSide);
            TimeToContact = 99f;
            HasContact = false;
            ContactPoint = Receiver.Position;
            TouchArmed = false;
            ReceiverReached = false;
        }

        public void Tick(float dt)
        {
            if (!BallLive) return;

            _prev = Ball;
            Ball = BallPhysics.Step(Ball, dt);

            HasPrediction = BallPhysics.PredictLanding(Ball, Court.BallRadius, dt, 6f, out PredictedLanding, out _);

            if (BallPhysics.PredictLanding(Ball, ContactHeight, dt, 6f, out Vector3 cp, out float tc))
            {
                ContactPoint = cp;
                TimeToContact = tc;
                HasContact = true;
            } else
            {
                TimeToContact -= dt;
            }

            if (TouchArmed && TimeToContact <= 0f) ResolveTouch();

            if (AutoPosition)
            {
                Vector3 alvo = (HasContact && Rally.TouchCount == 0) ? ContactPoint : Receiver.Position;
                Receiver = PlayerPhysics.StepToTarget(Receiver, ReceiverAttr, alvo, dt);    
            } else
            {
                Receiver = PlayerPhysics.StepDirect(Receiver, ReceiverAttr, MoveInput, dt);
            }

            if(DetectNetCrossing()) return;
            if (DetectCeiling()) return;
            if (DetectGround()) return;
        }

        public void TryReceive()
        {
            if (!BallLive || TouchArmed) return;
            if (Rally.TouchingSide != Receiver.Side) return;
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

        // ------ detecção por travessia ------
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

            Rally.OnNetCrossed(Court.SideOf(z1));
            OnLog?.Invoke($"cruzou a rede a {cross.y:F2} m -> posse do lado {SideName(Rally.TouchingSide)}");
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

            bool crossed = y0 > Court.BallRadius && y1 <= Court.BallRadius;
            if (!crossed) return false;

            float denom = y0 - y1;
            float f = denom > 1e-6f ? (y0 - Court.BallRadius) / denom : 0f;
            Vector3 p = Vector3.Lerp(_prev.Position, Ball.Position, f);

            if (Court.IsInBounds(p))
                EndRally(-Court.SideOf(p.z), $"quicou no lado {SideName(Court.SideOf(p.z))} em x={p.x:F2} z={p.z:F2}");
            else
                EndRally(-Rally.LastTouchSide, $"fora em x={p.x:F2} z={p.z:F2}");

            return true;
        }

        // ----- resolução -----
        private void EndRally(int winnerSide, string reason)
        {
            BallLive = false;
            HasPrediction = false;
            Rally.AwardPoint(winnerSide, reason);

            OnLog?.Invoke($"PONTO {SideName(winnerSide)} - {reason} | A {Rally.ScoreA} x {Rally.ScoreB} B");
        }

        private static string SideName(int side) => side == Court.SideA ? "A" : "B";

        /// <summary>Ponto uniforme num disco. O sqrt evita amontoar tudo no centro.</summary>
        private Vector2 RandomInCircle(float radius)
        {
            double ang = _rng.NextDouble() * System.Math.PI * 2.0;
            double r = radius * System.Math.Sqrt(_rng.NextDouble());
            return new Vector2((float)(r * System.Math.Cos(ang)), (float)(r * System.Math.Sin(ang)));
        }

        private void ResolveTouch()
        {
            TouchArmed = false;

            Vector2 flat = new Vector2(Ball.Position.x - Receiver.Position.x, Ball.Position.z - Receiver.Position.z);

            if (flat.magnitude > ReceiverAttr.Reach)
            {
                OnLog?.Invoke($"não chegou a tempo - {flat.magnitude:F2} m da bola");
                return;
            }

            Vector2 desvio = RandomInCircle(MaxPassError * (1f - ArmedQuality));
            Vector3 alvo = SetterSpot + new Vector3(desvio.x, 0f, desvio.y);

            if (BallPhysics.SolveLaunch(Ball.Position, alvo, 65f, 1f / 60f, out Vector3 v))
            {
                Ball = new BallState(Ball.Position, v);
                Rally.OnTouch(Receiver.Id, Receiver.Side);

                OnLog?.Invoke($"passe -> {desvio.magnitude:F2} m do levantador");
            } else
            {
                OnLog?.Invoke($"solver falhou no passe: de y={Ball.Position.y:F2} " + $"para {alvo} - bola perdida");
            }
        }
    }
}