using UnityEngine;

namespace Volley.Sim
{
    /// <summary>Estado completo da bola num instante.
    /// É struct (value type) de propósito: copiar é cópia profunda e barata, então a predição de trajetória nunca consegue mutar a bola real.
    /// </summary>
    public struct BallState
    {
        public Vector3 Position;
        public Vector3 Velocity;

        public BallState(Vector3 position, Vector3 velocity)
        {
            Position = position;
            Velocity = velocity;
        }
    }
}