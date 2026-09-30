using UnityEngine;
using Volley.Bootstrap;

namespace Volley.View
{
    /// <summary>Ring on the floor under the player the human controls.</summary>
    public class ControlIndicatorView : MonoBehaviour
    {
        [SerializeField] private GameRoot root;
        [SerializeField] private Renderer ring;

        private void Update()
        {
            if (root == null) return;

            int i = root.Sim.ControlledIndex;
            bool valid = i >= 0 && i < root.Sim.Players.Length;

            if (ring != null) ring.enabled = valid;
            if (!valid) return;

            Vector3 position = root.Sim.Players[i].Position;
            position.y = 0.02f;
            transform.position = position;
        }
    }
}
