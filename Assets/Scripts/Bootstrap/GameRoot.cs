using UnityEngine;
using UnityEngine.InputSystem;
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

        public MatchSim Sim { get; private set; }

        private bool _serveRequested;

        private void Awake()
        {
            Sim = new MatchSim();
            Sim.OnLog += msg => Debug.Log(msg);
        }

        // Input SEMPRE no Update: wasPressedThisFrame só é true por um frame de render,
        // e o FixedUpdate pode não rodar nesse frame. Por isso bufferizamos na flag.
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                _serveRequested = true;
            }
        }

        private void FixedUpdate()
        {
            if (_serveRequested)
            {
                _serveRequested = false;
                if (!Sim.BallLive) ServeNow();
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

            float rad = serveAngleDeg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(
                0f,
                Mathf.Sin(rad),
                Mathf.Cos(rad) * -side
            );

            Sim.Serve(from, dir * serveSpeed);
        }
    }
}