using UnityEngine;

namespace Volley.Sim
{
    /// <summary>Timing window in seconds around the ideal contact moment.</summary>
    public struct TouchWindow
    {
        public float Perfect;
        public float Good;
        public float Late;

        public static TouchWindow Default => new TouchWindow
        {
            Perfect = 0.05f,
            Good = 0.15f,
            Late = 0.30f,
        };
    }

    public static class TouchTiming
    {
        /// <summary>0 = no contact, 1 = perfect contact.</summary>
        public static float Quality(float errorSeconds, TouchWindow window)
        {
            float error = Mathf.Abs(errorSeconds);

            if (error <= window.Perfect) return 1f;

            if (error <= window.Good)
                return Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(window.Perfect, window.Good, error));

            if (error <= window.Late)
                return Mathf.Lerp(0.55f, 0f, Mathf.InverseLerp(window.Good, window.Late, error));

            return 0f;
        }

        public static string Label(float quality)
        {
            if (quality >= 0.95f) return "PERFEITO";
            if (quality >= 0.70f) return "BOM";
            if (quality >= 0.35f) return "RUIM";
            return "PÉSSIMO";
        }
    }
}
