using UnityEngine;
using Volley.Bootstrap;

namespace Volley.View
{
    public class PlayerView : MonoBehaviour
    {
        [SerializeField] private GameRoot root;
        [SerializeField] private int index;
        [SerializeField] private float jumpHeight = 0.55f;

        private void Update()
        {
            if (root == null) return;

            var sim = root.Sim;
            Vector3 p = sim.Players[index].Position;

            float bt = sim.Players[index].BlockTimer;
            if (bt > 0f)
            {
                float t = 1f - (bt / sim.BlockDuration);
                p.y += 4f * jumpHeight * t * (1f - t);
            }

            transform.position = p + Vector3.up * 0.95f;
        }
    }
}