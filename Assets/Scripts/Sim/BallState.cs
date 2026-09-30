using UnityEngine;

namespace Volley.Sim
{
    /// <summary>
    /// Complete state of the ball at one instant. A struct on purpose: copying is a deep,
    /// cheap copy, so trajectory prediction can never mutate the real ball.
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
