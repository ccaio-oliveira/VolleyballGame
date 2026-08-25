using UnityEngine;
using Volley.Sim;

namespace Volley.Bootstrap
{
    /// <summary>Ponte entre o Unity e a simulação. O único MonoBehaviour com lógica.</summary>
    public class GameRoot : MonoBehaviour
    {
        [Header("Saque de teste")]
        [SerializeField] private Vector3 serveFrom = new Vector3(0f, 2.7f, -9.5f);
        [SerializeField] private float serveSpeed = 18f;
        [SerializeField] private float serveAngleDeg = 12f;

        [Header("Saque por alvo")]
        [SerializeField] private bool useTargetAiming = true;
        [SerializeField] private float targetX = 0f;
        [SerializeField] private float targetZ = 6.5f;
        
        [SerializeField] private float netClearance = 0.25f;
        [SerializeField] private Transform cameraTransform;

        public MatchSim Sim { get; private set; }

        private bool _serveRequested;
        private bool _receiveRequested;
        private bool _blockRequested;

        private void Awake()
        {
            Sim = new MatchSim();
            Sim.OnLog += msg => Debug.Log(msg);
        }

        // Input SEMPRE no Update: wasPressedThisFrame só é true por um frame de render,
        // e o FixedUpdate pode não rodar nesse frame. Por isso bufferizamos na flag.
        private void Update()
        {
            if (InputRouter.ServePressed())
            {
                _serveRequested = true;
            }

            if (InputRouter.ReceivePressed())
            {
                _receiveRequested = true;
            }

            if (InputRouter.BlockPressed())
            {
                _blockRequested = true;
            }

            Sim.MoveInput = ScreenToWorld(InputRouter.ReadMove());
        }

        private void FixedUpdate()
        {
            if (_serveRequested)
            {
                _serveRequested = false;
                if (!Sim.BallLive) ServeNow();
            }

            if (_receiveRequested)
            {
                _receiveRequested = false;
                Sim.TryReceive();
            }

            if (_blockRequested)
            {
                _blockRequested = false;
                Sim.TryBlock();
            }

            Sim.Tick(Time.fixedDeltaTime);
        }

        private void ServeNow()
        {
            int side = Sim.Rally.ServingSide;

            // espelhar o saque pro outro lado é uma troca de sinal - só por causa da origem no centro da quadra.
            Vector3 from = new Vector3(
                serveFrom.x,
                serveFrom.y,
                Mathf.Abs(serveFrom.z) * side
            );

            if (useTargetAiming)
            {
                Vector3 target = new Vector3(targetX, Court.BallRadius, Mathf.Abs(targetZ) * -side);

                if (!BallPhysics.SolveFlattestLegal(from, target, Sim.NetHeight, netClearance, Time.fixedDeltaTime, out Vector3 v))
                {
                    Debug.LogWarning($"nenhum ângulo legal atinge z={target.z:F1}");
                    return;
                }

                float yNet = BallPhysics.NetCrossHeight(new BallState(from, v), Time.fixedDeltaTime);
                float angle = Mathf.Asin(v.normalized.y) * Mathf.Rad2Deg;

                Debug.Log($"solver: {v.magnitude:F2} m/s a {angle:F1}º " + $"-> rede a {yNet:F2} m");

                Sim.Serve(from, v);
                return;
            }

            float rad = serveAngleDeg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(
                0f,
                Mathf.Sin(rad),
                Mathf.Cos(rad) * -side
            );

            Sim.Serve(from, dir * serveSpeed);
        }

        /// <summary>Converte intenção de tela em direção de mundo, usando a câmera.</summary>
        private Vector2 ScreenToWorld(Vector2 screen)
        {
            if (cameraTransform == null) return screen;

            Vector3 f = cameraTransform.forward;
            f.y = 0f;

            Vector3 r = cameraTransform.right;
            r.y = 0f;

            if (f.sqrMagnitude < 1e-4f) return screen;

            Vector3 world = r.normalized * screen.x + f.normalized * screen.y;
            return new Vector2(world.x, world.z);
        }
    }
}