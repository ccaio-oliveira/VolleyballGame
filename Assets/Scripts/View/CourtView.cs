using UnityEngine;
using UnityEngine.Serialization;
using Volley.Sim;

namespace Volley.View
{
    /// <summary>
    /// Builds the court from the Court constants. No court measurement is written here:
    /// if it changes in the Sim, the drawing follows.
    /// </summary>
    public class CourtView : MonoBehaviour
    {
        [Header("Materials")]
        [FormerlySerializedAs("matLinha")]
        [SerializeField] private Material lineMaterial;

        [FormerlySerializedAs("matRede")]
        [SerializeField] private Material netMaterial;

        [FormerlySerializedAs("matFita")]
        [SerializeField] private Material tapeMaterial;

        [FormerlySerializedAs("matAntena")]
        [SerializeField] private Material antennaMaterial;

        [Header("Drawing sizes")]
        [FormerlySerializedAs("larguraLinha")]
        [SerializeField] private float lineWidth = 0.05f;        // official

        [FormerlySerializedAs("alturaLinha")]
        [SerializeField] private float lineThickness = 0.012f;

        [FormerlySerializedAs("alturaRede")]
        [SerializeField] private float netBandHeight = 1.0f;

        [FormerlySerializedAs("alturaAntena")]
        [SerializeField] private float antennaHeight = 0.80f;    // above the tape

        private void Start()
        {
            float halfWidth = Court.HalfWidth;
            float halfLength = Court.HalfLength;
            float w = lineWidth;

            // boundary lines
            CreateBlock("Sideline+X", new Vector3( halfWidth, 0f, 0f), new Vector3(w, 1f, halfLength * 2f + w), lineMaterial, true);
            CreateBlock("Sideline-X", new Vector3(-halfWidth, 0f, 0f), new Vector3(w, 1f, halfLength * 2f + w), lineMaterial, true);
            CreateBlock("Endline+Z",  new Vector3(0f, 0f,  halfLength), new Vector3(halfWidth * 2f + w, 1f, w), lineMaterial, true);
            CreateBlock("Endline-Z",  new Vector3(0f, 0f, -halfLength), new Vector3(halfWidth * 2f + w, 1f, w), lineMaterial, true);

            // center line and attack lines
            CreateBlock("CenterLine",  Vector3.zero,                           new Vector3(halfWidth * 2f, 1f, w), lineMaterial, true);
            CreateBlock("AttackLine+Z", new Vector3(0f, 0f,  Court.AttackLine), new Vector3(halfWidth * 2f, 1f, w), lineMaterial, true);
            CreateBlock("AttackLine-Z", new Vector3(0f, 0f, -Court.AttackLine), new Vector3(halfWidth * 2f, 1f, w), lineMaterial, true);

            BuildNet();

            BuildAttackLineDashes(1);
            BuildAttackLineDashes(-1);
        }

        private void BuildNet()
        {
            float height = Court.NetHeightMen;
            float halfWidth = Court.HalfWidth;

            CreateBlock("Net",  new Vector3(0f, height - netBandHeight * 0.5f, 0f), new Vector3(halfWidth * 2f, netBandHeight, 0.02f), netMaterial, false);
            CreateBlock("Tape", new Vector3(0f, height - 0.035f, 0f),               new Vector3(halfWidth * 2f, 0.07f, 0.03f),         tapeMaterial, false);

            BuildAntenna( halfWidth, height);
            BuildAntenna(-halfWidth, height);
        }

        private void BuildAntenna(float x, float tapeHeight)
        {
            var antenna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            antenna.name = $"Antenna_{(x > 0 ? "+X" : "-X")}";
            antenna.transform.SetParent(transform, false);

            // Unity's cylinder is 2 units tall, hence the half-height scale
            antenna.transform.localPosition = new Vector3(x, tapeHeight + antennaHeight * 0.5f, 0f);
            antenna.transform.localScale = new Vector3(0.02f, antennaHeight * 0.5f, 0.02f);

            Destroy(antenna.GetComponent<Collider>());
            if (antennaMaterial != null) antenna.GetComponent<Renderer>().sharedMaterial = antennaMaterial;
        }

        /// <summary>
        /// Attack line extension into the free zone. FIVB: 5 dashes of 15 cm spaced 20 cm apart,
        /// on both sides of the court.
        /// </summary>
        private void BuildAttackLineDashes(int courtSide)
        {
            const int dashCount = 5;
            const float dashLength = 0.15f;
            const float gap = 0.20f;

            float halfWidth = Court.HalfWidth;
            float z = Court.AttackLine * courtSide;

            for (int sideline = -1; sideline <= 1; sideline += 2)
            {
                for (int k = 0; k < dashCount; k++)
                {
                    // derived from the integer index, never accumulated
                    float offset = (halfWidth + gap) + k * (dashLength + gap) + dashLength * 0.5f;

                    CreateBlock($"AttackDash_{courtSide}_{sideline}_{k}", new Vector3(offset * sideline, 0f, z),
                                new Vector3(dashLength, 1f, lineWidth), lineMaterial, true);
                }
            }
        }

        private void CreateBlock(string blockName, Vector3 center, Vector3 scale, Material material, bool onFloor)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = blockName;
            block.transform.SetParent(transform, false);

            block.transform.localPosition = onFloor ? new Vector3(center.x, lineThickness * 0.5f, center.z) : center;
            block.transform.localScale = onFloor ? new Vector3(scale.x, lineThickness, scale.z) : scale;

            Destroy(block.GetComponent<Collider>());
            if (material != null) block.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
