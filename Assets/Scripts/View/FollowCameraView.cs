using UnityEngine;
using Volley.Bootstrap;

namespace Volley.View
{
    public class FollowCameraView : MonoBehaviour
    {
        [SerializeField] private GameRoot root;
        [SerializeField] private Vector3 offset = new Vector3(0f, 8f, 15f);
        [SerializeField] private float lateral = 0.35f; // 0 = fixa, 1 = cola no jogador
        [SerializeField] private float suavidade = 4f;

        private void LateUpdate()
        {
            if (root == null) return;

            var sim = root.Sim;
            int i = sim.ControlledIndex;
            if (i < 0 || i >= sim.Players.Length) return;

            Vector3 desejada = offset;
            desejada.x += sim.Players[i].Position.x * lateral;

            float t = 1f - Mathf.Exp(-suavidade * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desejada, t);
        }
    }
}