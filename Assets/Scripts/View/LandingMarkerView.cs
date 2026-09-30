using UnityEngine;
using Volley.Bootstrap;

namespace Volley.View
{
    /// <summary>Disc on the predicted landing point of the ball.</summary>
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

            Vector3 position = sim.PredictedLanding;
            position.y = 0.01f;   // 1 cm above the floor avoids z-fighting
            transform.position = position;
        }
    }
}
