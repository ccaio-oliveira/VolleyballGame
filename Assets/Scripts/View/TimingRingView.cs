using UnityEngine;
using Volley.Bootstrap;

namespace Volley.View
{
    public class TimingRingView : MonoBehaviour
    {
        [SerializeField] private GameRoot root;
        [SerializeField] private Transform anelQueEncolhe;
        [SerializeField] private Renderer[] partes;

        [SerializeField] private float lead = 0.8f;
        [SerializeField] private float baseSize = 0.55f;
        [SerializeField] private float maxSize = 2.6f;

        private void Update()
        {
            if (root == null) return;
            var sim = root.Sim;

            bool show = sim.BallLive && sim.HasContact 
                        && sim.Rally.TouchingSide == sim.HumanSide 
                        && sim.TimeToContact < lead && sim.TimeToContact > -0.30f;
            
            foreach (var r in partes) if (r != null) r.enabled = show;
            if (!show) return;

            Vector3 p = sim.ContactPoint;
            p.y = 0.03f;
            transform.position = p;

            float k = Mathf.Clamp01(sim.TimeToContact / lead);
            float s = Mathf.Lerp(baseSize, maxSize, k);

            if (anelQueEncolhe != null)
            {
                anelQueEncolhe.localScale = new Vector3(s, 0.004f, s);
            }
        }
    }
}