using NUnit.Framework;
using UnityEngine;
using Volley.Sim;

namespace Volley.Tests
{
    public class BallPhysicsTests
    {
        private const float Dt = 1f / 60f;

        /// <summary>Reference serve: 2.7 m contact, 18 m/s at 12 degrees from behind the end line.</summary>
        private static BallState ReferenceServe()
        {
            const float height = 2.7f;
            const float speed = 18f;
            const float angle = 12f;

            float rad = angle * Mathf.Deg2Rad;
            Vector3 from = new Vector3(0f, height, -9.5f);
            Vector3 velocity = new Vector3(0f, Mathf.Sin(rad), Mathf.Cos(rad)) * speed;
            return new BallState(from, velocity);
        }

        [Test]
        public void ReferenceServe_ClearsTheTape()
        {
            BallState s = ReferenceServe();
            BallState previous;

            do
            {
                previous = s;
                s = BallPhysics.Step(s, Dt);
            }
            while (s.Position.z < 0f && s.Position.y > Court.BallRadius);

            Assert.That(s.Position.z, Is.GreaterThanOrEqualTo(0f), "a bola nem chegou na rede");

            float t = -previous.Position.z / (s.Position.z - previous.Position.z);
            Vector3 crossing = Vector3.Lerp(previous.Position, s.Position, t);

            Assert.That(crossing.y, Is.GreaterThan(Court.NetHeightMen), $"passou por baixo da fita, a {crossing.y:F2} m");
        }

        [Test]
        public void ReferenceServe_LandsDeepInBounds()
        {
            BallState s = ReferenceServe();

            bool landed = BallPhysics.PredictLanding(s, Court.BallRadius, Dt, 6f, out Vector3 hit, out _);

            Assert.IsTrue(landed, "a bola tinha que tocar o chão em menos de 6 s");
            Assert.IsTrue(Court.IsInBounds(hit), $"caiu fora, em {hit}");
            Assert.That(hit.z, Is.InRange(5.5f, 7.5f), "não é mais um saque de fundo");
        }

        [Test]
        public void Prediction_StaysStableDuringFlight()
        {
            BallState s = ReferenceServe();

            BallPhysics.PredictLanding(s, Court.BallRadius, Dt, 6f, out Vector3 before, out _);

            for (int i = 0; i < 20; i++)
                s = BallPhysics.Step(s, Dt);

            BallPhysics.PredictLanding(s, Court.BallRadius, Dt, 6f, out Vector3 after, out _);

            Assert.That(Vector3.Distance(before, after), Is.LessThan(0.01f),
                "predição divergiu da simulação — o marcador andaria na tela");
        }

        [Test]
        public void Drag_ShortensTheRange()
        {
            BallState s = ReferenceServe();

            float vy0 = s.Velocity.y;
            float vz0 = s.Velocity.z;
            float h = s.Position.y - Court.BallRadius;

            // vacuum range: positive root of 4.905t² - vy0·t - h = 0
            float t = (vy0 + Mathf.Sqrt(vy0 * vy0 + 4f * 4.905f * h)) / (2f * 4.905f);
            float vacuumRange = vz0 * t;

            BallPhysics.PredictLanding(s, Court.BallRadius, Dt, 6f, out Vector3 hit, out _);
            float actualRange = hit.z - s.Position.z;

            Assert.That(actualRange, Is.LessThan(vacuumRange * 0.9f),
                $"o arrasto não está agindo: real {actualRange:F2} m " +
                $"contra {vacuumRange:F2} m no vácuo");
        }
    }
}
