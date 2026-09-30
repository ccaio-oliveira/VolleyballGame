using UnityEngine;
using UnityEngine.Serialization;
using Volley.View;

namespace Volley.Bootstrap
{
    /// <summary>Instantiates one player prefab per simulated player and binds its views.</summary>
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private GameRoot root;
        [SerializeField] private GameObject prefab;

        [FormerlySerializedAs("matA")]
        [SerializeField] private Material teamAMaterial;

        [FormerlySerializedAs("matB")]
        [SerializeField] private Material teamBMaterial;

        private void Start()
        {
            for (int i = 0; i < root.Sim.Players.Length; i++)
            {
                GameObject player = Instantiate(prefab, transform);
                player.name = $"Player_{i:00}";

                foreach (var view in player.GetComponentsInChildren<PlayerView>(true))
                    view.Bind(root, i);

                foreach (var blockBox in player.GetComponentsInChildren<BlockBoxView>(true))
                    blockBox.Bind(root, i);

                // sharedMaterial, not material: the latter clones the asset per object
                var body = player.transform.Find("Body");
                var bodyRenderer = body != null ? body.GetComponent<Renderer>() : null;
                if (bodyRenderer != null) bodyRenderer.sharedMaterial = (i < 6) ? teamAMaterial : teamBMaterial;
            }
        }
    }
}
