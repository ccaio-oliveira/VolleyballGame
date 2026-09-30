using UnityEngine;
using Volley.Bootstrap;

namespace Volley.View
{
    /// <summary>Draws the exact volume FindBlocker tests: the band the blocker's hands occupy.</summary>
    public class BlockBoxView : MonoBehaviour
    {
        [SerializeField] private GameRoot root;
        [SerializeField] private int index;
        [SerializeField] private Renderer box;

        public void Bind(GameRoot gameRoot, int playerIndex)
        {
            root = gameRoot;
            index = playerIndex;
        }

        private void Awake()
        {
            if (box == null) box = GetComponent<Renderer>();
        }

        private void Update()
        {
            if (root == null) return;
            var sim = root.Sim;
            var player = sim.Players[index];

            bool armsRaised = player.BlockTimer > 0f;
            if (box != null) box.enabled = armsRaised;
            if (!armsRaised) return;

            float handTop = player.Position.y + sim.Attrs[index].ReachHeight;
            float handBottom = Mathf.Max(sim.NetHeight, handTop - sim.BlockHandSpan);

            transform.position = new Vector3(player.Position.x, (handTop + handBottom) * 0.5f, 0.08f * player.Side);
            transform.localScale = new Vector3(sim.BlockHalfWidth * 2f, handTop - handBottom, 0.12f);
        }
    }
}
