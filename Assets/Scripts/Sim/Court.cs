using UnityEngine;

namespace Volley.Sim
{
    /// <summary>
    /// Court geometry. Official FIVB measurements in meters. Origin at the center of the
    /// court on the floor: X along the net, Y up, Z along the length with the net at 0.
    /// </summary>
    public static class Court
    {
        public const float HalfLength = 9f;        // Z from -9 to +9
        public const float HalfWidth = 4.5f;       // X from -4.5 to +4.5
        public const float NetHeightMen = 2.43f;
        public const float NetHeightWomen = 2.24f;
        public const float AttackLine = 3f;        // distance from the net
        public const float Ceiling = 10f;
        public const float BallRadius = 0.105f;

        public const int SideA = -1;               // negative Z half
        public const int SideB = +1;               // positive Z half

        /// <summary>Did the ball land inside the court boundaries?</summary>
        public static bool IsInBounds(Vector3 p)
        {
            return Mathf.Abs(p.x) <= HalfWidth && Mathf.Abs(p.z) <= HalfLength;
        }

        /// <summary>Which side of the net this Z is on: SideA, SideB or 0.</summary>
        public static int SideOf(float z)
        {
            if (z < 0f) return SideA;
            if (z > 0f) return SideB;
            return 0;
        }
    }
}
