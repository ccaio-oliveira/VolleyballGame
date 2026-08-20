using UnityEngine;
using Volley.Bootstrap;

namespace Volley.View
{
    public class PlayerView : MonoBehaviour
    {
        [SerializeField] private GameRoot root;

        private void Update()
        {
            if (root == null) return;
            transform.position = root.Sim.Receiver.Position + Vector3.up * 0.95f;
        }
    }
}