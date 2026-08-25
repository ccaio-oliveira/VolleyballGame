using UnityEngine;
using Volley.View;

namespace Volley.Bootstrap
{
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private GameRoot root;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Material matA;
        [SerializeField] private Material matB;

        private void Start()
        {
            for (int i = 0; i < root.Sim.Players.Length; i++)
            {
                GameObject go = Instantiate(prefab, transform);
                go.name = $"Player_{i:00}";

                foreach (var v in go.GetComponentsInChildren<PlayerView>(true))
                {
                    v.Bind(root, i);
                }

                foreach (var b in go.GetComponentsInChildren<BlockBoxView>(true))
                {
                    b.Bind(root, i);
                }

                var body = go.transform.Find("Body");
                var rend = body != null ? body.GetComponent<Renderer>() : null;

                if (rend != null) rend.sharedMaterial = (i < 6) ? matA : matB;
            }
        }
    }
}