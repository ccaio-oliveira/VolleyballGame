using NUnit.Framework;
using UnityEngine;
using Volley.Sim;

namespace Volley.Tests
{
    public class BallPhysicsTests
    {
        private const float Dt = 1f / 60f;

        /// <summary>Reproduz exatamente o saque padrão do GameRoot.</summary>
        private static BallState SaquePadrao()
        {
            const float altura = 2.7f;
            const float speed = 18f;
            const float angulo = 12f;

            float rad = angulo * Mathf.Deg2Rad;
            Vector3 from = new Vector3(0f, altura, -9.5f);
            Vector3 vel = new Vector3(0f, Mathf.Sin(rad), Mathf.Cos(rad)) * speed;
            return new BallState(from, vel);
        }

        [Test]
        public void SaquePadrao_PassaAcimaDaFita()
        {
            BallState s = SaquePadrao();
            BallState prev;

            do
            {
                prev = s;
                s = BallPhysics.Step(s, Dt);
            }
            while (s.Position.z < 0f && s.Position.y > Court.BallRadius);

            Assert.That(s.Position.z, Is.GreaterThanOrEqualTo(0f), "a bola nem chegou na rede");

            float f = -prev.Position.z / (s.Position.z - prev.Position.z);
            Vector3 cross = Vector3.Lerp(prev.Position, s.Position, f);

            Assert.That(cross.y, Is.GreaterThan(Court.NetHeightMen), $"passou por baixo da fita, a {cross.y:F2} m");
        }

        [Test]
        public void SaquePadrao_CaiDentroNoFundo()
        {
            BallState s = SaquePadrao();

            bool tocou = BallPhysics.PredictLanding(
                s, Court.BallRadius, Dt, 6f, out Vector3 hit, out _);

            Assert.IsTrue(tocou, "a bola tinha que tocar o chão em menos de 6 s");
            Assert.IsTrue(Court.IsInBounds(hit), $"caiu fora, em {hit}");
            Assert.That(hit.z, Is.InRange(5.5f, 7.5f), "não é mais um saque de fundo");
        }

        [Test]
        public void Predicao_NaoMudaDuranteOVoo()
        {
            BallState s = SaquePadrao();

            BallPhysics.PredictLanding(s, Court.BallRadius, Dt, 6f, out Vector3 antes, out _);

            for (int i = 0; i < 20; i++)
                s = BallPhysics.Step(s, Dt);

            BallPhysics.PredictLanding(s, Court.BallRadius, Dt, 6f, out Vector3 depois, out _);

            Assert.That(Vector3.Distance(antes, depois), Is.LessThan(0.01f),
                "predição divergiu da simulação — o marcador andaria na tela");
        }

        [Test]
        public void Arrasto_EncurtaOAlcance()
        {
            BallState s = SaquePadrao();

            float vy0 = s.Velocity.y;
            float vz0 = s.Velocity.z;
            float h   = s.Position.y - Court.BallRadius;

            // alcance no vácuo: raiz positiva de 4.905t² - vy0·t - h = 0
            float t = (vy0 + Mathf.Sqrt(vy0 * vy0 + 4f * 4.905f * h)) / (2f * 4.905f);
            float alcanceVacuo = vz0 * t;

            BallPhysics.PredictLanding(s, Court.BallRadius, Dt, 6f, out Vector3 hit, out _);
            float alcanceReal = hit.z - s.Position.z;

            Assert.That(alcanceReal, Is.LessThan(alcanceVacuo * 0.9f),
                $"o arrasto não está agindo: real {alcanceReal:F2} m " +
                $"contra {alcanceVacuo:F2} m no vácuo");
        }
    }
}