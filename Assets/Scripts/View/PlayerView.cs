using UnityEngine;
using Volley.Bootstrap;

namespace Volley.View
{
    public class PlayerView : MonoBehaviour
    {
        [SerializeField] private GameRoot root;
        [SerializeField] private int index;

        private void Update()
        {
            if (root == null) return;
            transform.position = root.Sim.Players[index].Position + Vector3.up * 0.95f;
        }
    }
}