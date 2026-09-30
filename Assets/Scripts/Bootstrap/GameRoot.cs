using UnityEngine;
using UnityEngine.Serialization;
using Volley.Sim;

namespace Volley.Bootstrap
{
    /// <summary>Bridge between Unity and the simulation. The only MonoBehaviour that ticks the sim.</summary>
    public class GameRoot : MonoBehaviour
    {
        [Header("Serve")]
        [SerializeField] private float serveTargetX = 0f;
        [SerializeField] private float serveTargetZ = 6.5f;

        [Header("Camera")]
        [SerializeField] private Transform cameraTransform;

        [Header("Human player")]
        [SerializeField] private PlayerRole humanRole = PlayerRole.MiddleBlocker;
        [SerializeField] private int humanRoleIndex = 1;

        [Header("Difficulty")]
        [FormerlySerializedAs("nivelAdversario")]
        [SerializeField] private AiLevel opponentLevel = AiLevel.Hard;

        [FormerlySerializedAs("nivelCompanheiros")]
        [SerializeField] private AiLevel teammateLevel = AiLevel.Easy;

        public MatchSim Sim { get; private set; }

        // Button presses are discrete events that exist for a single render frame, and
        // FixedUpdate may not run on that frame. They are buffered here and consumed there.
        private bool _serveRequested;
        private bool _jumpRequested;
        private bool _passRequested;
        private bool _attackRequested;
        private bool _blockRequested;

        private void Awake()
        {
            Sim = new MatchSim
            {
                HumanRole = humanRole,
                HumanRoleIndex = humanRoleIndex,
                OpponentProfile = AiProfile.From(opponentLevel),
                TeammateProfile = AiProfile.From(teammateLevel),
            };

            Sim.OnLog += message => Debug.Log(message);

            Debug.Log($"config -> você: {humanRole} #{humanRoleIndex}  |  " +
                      $"adversário: {opponentLevel}  |  companheiros: {teammateLevel}");
        }

        private void Update()
        {
            if (InputRouter.ServePressed()) _serveRequested = true;
            if (InputRouter.JumpPressed()) _jumpRequested = true;
            if (InputRouter.PassPressed()) _passRequested = true;
            if (InputRouter.AttackPressed()) _attackRequested = true;
            if (InputRouter.BlockPressed()) _blockRequested = true;

            // held movement is continuous state, so reading it here is safe without buffering
            Sim.MoveInput = ScreenToWorld(InputRouter.ReadMove());
        }

        private void FixedUpdate()
        {
            if (Consume(ref _serveRequested) && !Sim.BallLive) RequestHumanServe();
            if (Consume(ref _jumpRequested)) Sim.TryJump();
            if (Consume(ref _passRequested)) Sim.TryPass();
            if (Consume(ref _attackRequested)) Sim.TryAttack();
            if (Consume(ref _blockRequested)) Sim.TryBlock();

            Sim.Tick(Time.fixedDeltaTime);
        }

        private static bool Consume(ref bool flag)
        {
            bool wasSet = flag;
            flag = false;
            return wasSet;
        }

        private void RequestHumanServe()
        {
            if (Sim.Match.Finished) return;
            if (Sim.Rally.ServingSide != Sim.HumanSide) return;

            Sim.ServeTargetX = serveTargetX;
            Sim.ServeTargetZ = serveTargetZ;
            Sim.ServeNow(Sim.Rally.ServingSide);
        }

        /// <summary>Converts screen-space intent into a world direction using the camera.</summary>
        private Vector2 ScreenToWorld(Vector2 screen)
        {
            if (cameraTransform == null) return screen;

            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;

            Vector3 right = cameraTransform.right;
            right.y = 0f;

            if (forward.sqrMagnitude < 1e-4f) return screen;   // camera looking straight down

            Vector3 world = right.normalized * screen.x + forward.normalized * screen.y;
            return new Vector2(world.x, world.z);
        }
    }
}
