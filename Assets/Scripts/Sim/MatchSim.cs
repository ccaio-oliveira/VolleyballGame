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

        public void Serve(Vector3 from, Vector3 velocity)
        {
            Ball = new BallState(from, velocity);
            BallLive = true;
            Rally.BeginServe(Court.SideOf(from.z));
            OnLog?.Invoke($"saque do lado {SideName(Rally.TouchingSide)} " + $"de y={from.y:F2} a {velocity.magnitude:F1} m/s");
        }

        public void Tick(float dt)
        {
            if (!BallLive) return;

            _prev = Ball;
            Ball = BallPhysics.Step(Ball, dt);

            HasPrediction = BallPhysics.PredictLanding(Ball, Court.BallRadius, dt, 6f, out PredictedLanding, out _);

            if(DetectNetCrossing()) return;
            if (DetectCeiling()) return;
            if (DetectGround()) return;
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
    }
}