using UnityEngine;
using Volley.Bootstrap;

namespace Volley.View
{
    public class ControlIndicatorView : MonoBehaviour
    {
        [SerializeField] private GameRoot root;
        [SerializeField] private Renderer ring;

        private void Update()
        {
            if (root == null) return;

            int i = root.Sim.ControlledIndex;
            if (i < 0 || i >= root.Sim.Players.Length)
            {
                if (ring != null) ring.enabled = false;
                return;
            }

            if (ring != null) ring.enabled = true;

            Vector3 p = root.Sim.Players[i].Position;
            p.y = 0.02f;
            transform.position = p;
        }
    }
}