using UnityEngine;
using Volley.Bootstrap;

namespace Volley.View
{
    public class BlockBoxView : MonoBehaviour
    {
        [SerializeField] private GameRoot root;
        [SerializeField] private int index;
        [SerializeField] private Renderer box;

        public void Bind(GameRoot r, int i)
        {
            root = r;
            index = i;
        }

        private void Update()
        {
            if (root == null) return;
            var sim = root.Sim;

            bool up = sim.Players[index].BlockTimer > 0f;
            if (box != null) box.enabled = up;
            if (!up) return;

            float topo = sim.Attrs[index].BlockReach;
            float baixo = Mathf.Max(sim.NetHeight, topo - sim.BlockHandSpan);

            transform.position = new Vector3(sim.Players[index].Position.x, (topo + baixo) * 0.5f, 0.08f * sim.Players[index].Side);
            transform.localScale = new Vector3(sim.BlockHalfWidth * 2f, topo - baixo, 0.12f);
        }

        private void Awake()
        {
            if (box == null)
            {
                box = GetComponent<Renderer>();
            }
        }
    }
}