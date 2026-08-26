using UnityEngine;
using Volley.Sim;

namespace Volley.Bootstrap
{
    /// <summary>Ponte entre o Unity e a simulação. O único MonoBehaviour com lógica.</summary>
    public class GameRoot : MonoBehaviour
    {
        [Header("Saque por alvo")]
        [SerializeField] private float serveTargetX = 0f;
        [SerializeField] private float serveTargetZ = 6.5f;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private PlayerRole humanRole = PlayerRole.Central;
        [SerializeField] private int humanRoleIndex = 1;

        [Header("Dificuldade")]
        [SerializeField] private AiLevel nivelAdversario = AiLevel.Dificil;
        [SerializeField] private AiLevel nivelCompanheiros = AiLevel.Facil;

        public MatchSim Sim { get; private set; }

        private bool _serveRequested;
        private bool _receiveRequested;
        private bool _blockRequested;

        private void Awake()
        {
            Sim = new MatchSim();
            Sim.HumanRole = humanRole;
            Sim.HumanRoleIndex = humanRoleIndex;
            Sim.Ai = AiProfile.From(nivelAdversario);
            Sim.AiAmigo = AiProfile.From(nivelCompanheiros);
            Sim.OnLog += msg => Debug.Log(msg);

            Debug.Log($"config -> você: {humanRole} #{humanRoleIndex}  |  " +
            $"adversário: {nivelAdversario}  |  companheiros: {nivelCompanheiros}");
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
            if(Sim.Match.Finished) return;
            if (Sim.Rally.ServingSide != Sim.HumanSide) return;

            Sim.ServeTargetX = serveTargetX;
            Sim.ServeTargetZ = serveTargetZ;
            Sim.ServeNow(Sim.Rally.ServingSide);
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