using UnityEngine;

namespace Volley.Sim
{
    /// <summary>What defines this athlete. Becomes the career-mode player card later.</summary>
    public struct PlayerAttributes
    {
        public float MaxSpeed;       // m/s
        public float Acceleration;   // m/s²
        public float Reach;          // horizontal reach radius
        public float ReachHeight;    // standing hand height with the arm raised

        public static PlayerAttributes Default => new PlayerAttributes
        {
            MaxSpeed = 6.5f,
            Acceleration = 25f,
            Reach = 1.0f,
            ReachHeight = 2.45f,
        };
    }

    /// <summary>What changes every tick.</summary>
    public struct PlayerState
    {
        public Vector3 Position;     // y > 0 means airborne
        public Vector3 Velocity;
        public int Id;
        public int Side;
        public int Slot;             // fixed seat in the rotation order (0..5)
        public PlayerRole Role;
        public Vector3 Base;         // functional home for the current phase

        public float BlockTimer;     // > 0 while the arms are raised to block
        public float HitTimer;       // AI touch countdown; resolves at zero
    }
}
