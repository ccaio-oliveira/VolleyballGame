using UnityEngine;

namespace Volley.Sim
{
    /// <summary>Detecção por travessia: rede, teto, chão e bloqueio.</summary>
    public partial class MatchSim
    {
        // ================= detecção =================
        private bool DetectNetCrossing()
        {
            float z0 = _prev.Position.z;
            float z1 = Ball.Position.z;

            bool crossed = (z0 < 0f && z1 >= 0f) || (z0 > 0f && z1 <= 0f);
            if (!crossed) return false;

            float f = -z0 / (z1 - z0);
            Vector3 cross = Vector3.Lerp(_prev.Position, Ball.Position, f);

            if (cross.y < NetHeight)
            {
                EndRally(-Rally.LastTouchSide, $"na rede (y={cross.y:F2})");
                return true;
            }

            if (Mathf.Abs(cross.x) > Court.HalfWidth)
            {
                EndRally(-Rally.LastTouchSide, $"fora das antenas (x={cross.x:F2})");
                return true;
            }

            int defSide = Court.SideOf(z1);
            int blocker = Rally.ServeInFlight ? -1 : FindBlocker(cross, defSide);

            if (blocker >= 0)
            {
                ApplyBlock(blocker, cross, defSide);
                return false;
            }

            Rally.OnNetCrossed(Court.SideOf(z1));
            OnLog?.Invoke($"cruzou a rede a {cross.y:F2} m -> " + $"posse do lado {SideName(Rally.TouchingSide)}");
            return false;
        }

        private bool DetectCeiling()
        {
            if (_prev.Position.y < Court.Ceiling && Ball.Position.y >= Court.Ceiling)
            {
                EndRally(-Rally.LastTouchSide, "bateu no teto");
                return true;
            }
            return false;
        }

        private bool DetectGround()
        {
            float y0 = _prev.Position.y;
            float y1 = Ball.Position.y;

            if (!(y0 > Court.BallRadius && y1 <= Court.BallRadius)) return false;

            float denom = y0 - y1;
            float f = denom > 1e-6f ? (y0 - Court.BallRadius) / denom : 0f;
            Vector3 p = Vector3.Lerp(_prev.Position, Ball.Position, f);

            if (Court.IsInBounds(p))
                EndRally(-Court.SideOf(p.z), $"quicou no lado {SideName(Court.SideOf(p.z))} " + $"em x={p.x:F2} z={p.z:F2}");
            else
                EndRally(-Rally.LastTouchSide, $"fora em x={p.x:F2} z={p.z:F2}");

            return true;
        }

        private int FindBlocker(Vector3 cross, int defSide)
        {
            int b = TeamBase(defSide);

            for (int k = 0; k < 6; k++)
            {
                int i = b + k;
                if (Players[i].BlockTimer <= 0f) continue;
                if (!IsFront(i)) continue;

                float topo = Attrs[i].BlockReach;
                float baixo = Mathf.Max(NetHeight, topo - BlockHandSpan);

                if (Mathf.Abs(cross.x - Players[i].Position.x) <= BlockHalfWidth && cross.y >= baixo && cross.y <= topo)
                {
                    return i;
                }
            }

            return -1;
        }

        private void ApplyBlock(int i, Vector3 cross, int defSide)
        {
            int atkSide = -defSide;

            // margem 1 = bola passou rente à fita, mãos muito acima -> murro
            // margem 0 = bola passou no topo do alcance -> raspão
            float margem = (Attrs[i].BlockReach - cross.y) / BlockHandSpan;

            Rally.OnBlockTouch(i, defSide);

            float speed = Ball.Velocity.magnitude;
            Vector3 pos = cross;

            if (margem > 0.55f)
            {
                // BLOQUEIO: desce cravado do lado de quem atacou
                pos.z = 0.05f * atkSide;
                Vector3 v = new Vector3(Ball.Velocity.x * 0.25f, -speed * 0.30f, -Ball.Velocity.z * 0.28f);

                Ball = new BallState(pos, v);
                Rally.OnNetCrossed(atkSide);

                OnLog?.Invoke($"BLOQUEIO #{i} margem={margem:F2} " + $"a bola volta pro lado {SideName(atkSide)}");
            } else
            {
                // RASPÃO: passa, mas lenta e alta - vira bola defensável
                pos.z = 0.05f * defSide;
                Vector3 v = Ball.Velocity * 0.42f;
                v.y = Mathf.Abs(v.y) + 1.6f;

                Ball = new BallState(pos, v);
                Rally.OnNetCrossed(defSide);

                OnLog?.Invoke($"raspão no bloqueio #{i} margem={margem:F2} " + $"lado {SideName(defSide)} com 3 toques");
            }
            
            NovaLeitura();
        }
    }
}
