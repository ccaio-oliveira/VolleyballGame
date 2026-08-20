using UnityEngine;

namespace Volley.Sim
{
    public struct TouchWindow
    {
        public float Perfect;
        public float Good;
        public float Late;

        public static TouchWindow Default => new TouchWindow
        {
            Perfect = 0.05f,
            Good = 0.15f,
            Late = 0.30f
        };
    }

    public static class TouchTiming
    {
        /// <summary>0 = não encostou, 1 = contato perfeito.</summary>
        public static float Quality(float errorSeconds, TouchWindow w)
        {
            float e = Mathf.Abs(errorSeconds);

            if (e <= w.Perfect) return 1;

            if (e<= w.Good) return Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(w.Perfect, w.Good, e));

            if (e <= w.Late) return Mathf.Lerp(0.55f, 0f, Mathf.InverseLerp(w.Good, w.Late, e));

            return 0f;
        }

        public static string Label(float q)
        {
            if (q >= 0.95f) return "PERFEITO";
            if (q >= 0.70f) return "BOM";
            if (q >= 0.35f) return "RUIM";
            return "PÉSSIMO";
        }
    }
}