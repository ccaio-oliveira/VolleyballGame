using UnityEngine;

namespace Volley.Sim
{
    /// <summary>Orquestra a simulação. Sem MonoBehaviour: C# puro.>/summary>
    public class MatchSim
    {
        public BallState Ball;
        public bool BallLive;

        private BallState _prev;

        public Vector3 PredictedLanding;
        public bool HasPrediction;

        public void Serve(Vector3 from, Vector3 velocity)
        {
            Ball = new BallState(from, velocity);
            BallLive = true;
        }

        public void Tick(float dt)
        {
            if (!BallLive) return;

            _prev = Ball;
            Ball = BallPhysics.Step(Ball, dt);
            HasPrediction = BallPhysics.PredictLanding(Ball, Court.BallRadius, dt, 6f, out PredictedLanding, out _);

            // TEMPORÁRIO: detecção de chão inline, só para validar a física.
            // No passo 6 isso vira evento e o Debug.Log sai daqui.
            bool crossedFloor = _prev.Position.y > Court.BallRadius && Ball.Position.y <= Court.BallRadius;

            if (crossedFloor)
            {
                BallLive = false;
                string veredito = Court.IsInBounds(Ball.Position) ? "DENTRO" : "FORA";
                Debug.Log($"chão em x={Ball.Position.x:F2} z={Ball.Position.z:F2} -> {veredito}");
            }
        }
    }
}