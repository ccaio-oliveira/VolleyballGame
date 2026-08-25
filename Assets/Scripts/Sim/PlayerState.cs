using UnityEngine;

namespace Volley.Sim
{
    public enum PlayerRole { Passador, Levantador, Atacante }
    
    /// <summary>O que define este atleta. No M6 vira a ficha do modo carreira.</summary>
    public struct PlayerAttributes
    {
        public float MaxSpeed;
        public float Acceleration;
        public float Reach;
        public float ReachHeight;
        public float BlockReach;

        public static PlayerAttributes Default => new PlayerAttributes
        {
            MaxSpeed = 6.5f,
            Acceleration = 25f,
            Reach = 1.0f,
            ReachHeight =  2.4f,
            BlockReach = 3.2f
        };
    }

    /// <summary>O que muda a cada tick.</summary>
    public struct PlayerState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public int Id;
        public int Side;
        public PlayerRole Role;
        public Vector3 Base; // posição de formação
        public float BlockTimer;
    }
}