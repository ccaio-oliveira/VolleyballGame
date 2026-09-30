using UnityEngine;
using UnityEngine.Serialization;
using Volley.Bootstrap;

namespace Volley.View
{
    /// <summary>Shrinking ring on the contact point: press when it meets the fixed target ring.</summary>
    public class TimingRingView : MonoBehaviour
    {
        [SerializeField] private GameRoot root;

        [FormerlySerializedAs("anelQueEncolhe")]
        [SerializeField] private Transform shrinkingRing;

        [FormerlySerializedAs("partes")]
        [SerializeField] private Renderer[] ringRenderers;

        [SerializeField] private float lead = 0.8f;       // seconds of warning before contact
        [SerializeField] private float baseSize = 0.55f;
        [SerializeField] private float maxSize = 2.6f;

        private void Update()
        {
            if (root == null) return;
            var sim = root.Sim;

            bool show = sim.BallLive
                     && sim.HasContact
                     && sim.Rally.TouchingSide == sim.HumanSide
                     && sim.TimeToContact < lead
                     && sim.TimeToContact > -0.30f;

            foreach (var ringRenderer in ringRenderers)
                if (ringRenderer != null) ringRenderer.enabled = show;

            if (!show) return;

            Vector3 position = sim.ContactPoint;
            position.y = 0.03f;
            transform.position = position;

            float progress = Mathf.Clamp01(sim.TimeToContact / lead);
            float size = Mathf.Lerp(baseSize, maxSize, progress);

            if (shrinkingRing != null) shrinkingRing.localScale = new Vector3(size, 0.004f, size);
        }
    }
}
