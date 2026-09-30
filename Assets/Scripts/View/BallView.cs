using UnityEngine;
using Volley.Bootstrap;

namespace Volley.View
{
    /// <summary>Only draws. Reads the simulation state and moves the transform — nothing else.</summary>
    public class BallView : MonoBehaviour
    {
        [SerializeField] private GameRoot root;

        private void Update()
        {
            if (root == null) return;
            transform.position = root.Sim.Ball.Position;
        }
    }
}
