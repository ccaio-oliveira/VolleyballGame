using UnityEngine;
using UnityEngine.InputSystem;
using Volley.Sim;

namespace Volley.Bootstrap
{
    /// <summary>Ponte entre o Unity e a simulação. O único MonoBehaviour com lógica.</summary>
    public class GameRoot : MonoBehaviour
    {
        [Header("Saque de teste")]
        [SerializeField] private Vector3 serveFrom = new Vector3(0f, 2.0f, -9.5f);
        [SerializeField] private float serveSpeed = 18f;
        [SerializeField] private float serveAngleDeg = 12f;

        public MatchSim Sim { get; private set; }

        private bool _serveRequested;

        private void Awake()
        {
            Sim = new MatchSim();
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

                float rad = serveAngleDeg * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(0f, Mathf.Sin(rad), Mathf.Cos(rad));
                Sim.Serve(serveFrom, dir * serveSpeed);
            }

            Sim.Tick(Time.fixedDeltaTime);
        }
    }
}