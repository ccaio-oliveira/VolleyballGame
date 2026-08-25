using UnityEngine;

namespace Volley.Sim
{
    /// <summary>Geometria da quadra. Medidas oficiais FIVB, em metros.</summary>
    public static class Court
    {
        public const float HalfLength = 9f;
        public const float HalfWidth = 4.5f;
        public const float NetHeightMen = 2.43f;
        public const float NetHeighWomen = 2.24f;
        public const float AttackLine = 3f;
        public const float Ceiling = 10f;
        public const float BallRadius = 0.105f;

        public const int SideA = -1;
        public const int SideB = +1;

        /// <summary>A bola caiu dentro dos limites da quadra?</summary>
        public static bool IsInBounds(Vector3 p)
        {
            return Mathf.Abs(p.x) <= HalfWidth && Mathf.Abs(p.z) <= HalfLength;
        }

        /// <summary>Em que lado da rede está esse Z. Retorna SideA, SideB ou 0.</summary>
        public static int SideOf(float z)
        {
            if (z < 0f) return SideA;
            if (z > 0f) return SideB;
            return 0;
        }
    }
}