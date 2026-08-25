using UnityEngine;
using Volley.Bootstrap;

namespace Volley.View
{
    public class NetCrossView : MonoBehaviour
    {
        [SerializeField] private GameRoot root;
        [SerializeField] private Renderer dot;

        private void Update()
        {
            if (root == null) return;
            var sim = root.Sim;

            bool show = sim.BallLive && sim.HasNetCross && sim.TimeToNet < 1.2f && sim.NetCrossPoint.y > 0f;

            if (dot != null) dot.enabled = show;
            if (!show) return;

            transform.position = sim.NetCrossPoint;
        }
    }
}