using UnityEngine;
using UnityEngine.Serialization;
using Volley.Bootstrap;

namespace Volley.View
{
    /// <summary>Keeps the camera behind the human side, drifting laterally with the controlled player.</summary>
    public class FollowCameraView : MonoBehaviour
    {
        [SerializeField] private GameRoot root;
        [SerializeField] private Vector3 offset = new Vector3(0f, 8f, 15f);

        [FormerlySerializedAs("lateral")]
        [SerializeField] private float lateralFollow = 0.35f;   // 0 = fixed, 1 = locked to the player

        [FormerlySerializedAs("suavidade")]
        [SerializeField] private float smoothing = 4f;

        private void LateUpdate()
        {
            if (root == null) return;

            var sim = root.Sim;
            int i = sim.ControlledIndex;
            if (i < 0 || i >= sim.Players.Length) return;

            Vector3 desired = offset;
            desired.x += sim.Players[i].Position.x * lateralFollow;

            // exponential smoothing: same settle time at any frame rate
            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, t);
        }
    }
}
