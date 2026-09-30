using UnityEngine;
using Volley.Bootstrap;

namespace Volley.View
{
    /// <summary>Draws one player. The jump height comes straight from the simulated position.</summary>
    public class PlayerView : MonoBehaviour
    {
        private const float HalfBodyHeight = 0.95f;

        [SerializeField] private GameRoot root;
        [SerializeField] private int index;

        public void Bind(GameRoot gameRoot, int playerIndex)
        {
            root = gameRoot;
            index = playerIndex;
        }

        private void Update()
        {
            if (root == null) return;

            var sim = root.Sim;
            Vector3 position = sim.Players[index].Position;

            // debug line to the functional home, visible in the Scene view during Play
            Debug.DrawLine(position, sim.Players[index].Base + Vector3.up * 0.1f, Color.yellow);

            transform.position = position + Vector3.up * HalfBodyHeight;
        }
    }
}
