using UnityEngine;
using Volley.Bootstrap;

namespace Volley.View
{
    /// <summary>Desenha o marcador circular no ponto de queda previsto.</summary>
    public class LandingMarkerView : MonoBehaviour
    {
        [SerializeField] private GameRoot root;
        [SerializeField] private Renderer markerRenderer;

        private void Update()
        {
            if (root == null) return;

            var sim = root.Sim;
            bool show = sim.BallLive && sim.HasPrediction;

            if (markerRenderer != null) markerRenderer.enabled = show;
            if (!show) return;

            Vector3 p = sim.PredictedLanding;
            p.y = 0.01f; // 1 cm acima do chão

            transform.position = p;
        }
    }
}