using UnityEngine;

namespace Volley.Sim
{
    /// <summary>O que define este atleta. No M6 vira a ficha do modo carreira.</summary>
    public struct PlayerAttributes
    {
        public float MaxSpeed;
        public float Acceleration;
        public float Reach;
        public float ReachHeight;

        public static PlayerAttributes Default => new PlayerAttributes
        {
            MaxSpeed = 6.5f,
            Acceleration = 25f,
            Reach = 1.0f,
            ReachHeight =  2.4f
        };
    }

    /// <summary>O que muda a cada tick.</summary>
    public struct PlayerState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public int Id;
        public int Side;
    }
}