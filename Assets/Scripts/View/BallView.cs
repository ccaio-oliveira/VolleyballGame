using UnityEngine;
using Volley.Bootstrap;

namespace Volley.View
{
    /// <summary>Só desenha. Lê o estado da simulação e move o transform. Nada mais.</summary>
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