using UnityEngine;

namespace Volley.Sim
{
    /// <summary>Resolução dos toques: recepção, levantamento e ataque.</summary>
    public partial class MatchSim
    {
        // ================= toques =================
        public void TryReceive()
        {
            if (!BallLive || TouchArmed) return;
            if (Rally.TouchingSide != Players[ActiveIndex].Side) return;
            if (!IsHuman[ActiveIndex]) return;
            if (ActiveIndex != HumanIndex) return;

            if (!HasContact) 
            { 
                OnLog?.Invoke("sem ponto de contato");
                return;
            }

            float q = TouchTiming.Quality(TimeToContact, Window);

            if (q <= 0f)
            {
                OnLog?.Invoke($"fora da janela - {TimeToContact:+0.00;-0.00} s");
                return;
            }

            TouchArmed = true;
            ArmedQuality = q;
            _armedAim = MoveInput;

            OnLog?.Invoke($"{TouchTiming.Label(q)} q={q:F2} " + $"erro={TimeToContact:+0.00;-0.00}s");
        }

        public void TryBlock()
        {
            int i = ControlledIndex;
            if (!BallLive || !IsHuman[i]) return;
            if (Players[i].BlockTimer > 0f) return;

            if (Rally.ServeInFlight)
            {
                OnLog?.Invoke("não se bloqueia saque");
                return;
            }

            if (Mathf.Abs(Players[i].Position.z) > BlockZone)
            {
                OnLog?.Invoke("longe demais da rede pra bloquear");
                return;
            }

            Players[i].BlockTimer = BlockDuration;
            OnLog?.Invoke($"[#{i}] salta");
        }

        private void ResolveTouch(int i, float q)
        {
            TouchArmed = false;
            _resolvedTouch = Rally.TouchCount;
            _resolvedSide = Rally.TouchingSide;

            Vector2 flat = new Vector2(Ball.Position.x - Players[i].Position.x, Ball.Position.z - Players[i].Position.z);

            if (flat.magnitude > Attrs[i].Reach)
            {
                OnLog?.Invoke($"[#{i} {Players[i].Role}] não alcançou - {flat.magnitude:F2} m");
                return;
            }

            int side = Players[i].Side;

            switch (Rally.TouchCount)
            {
                case 0:
                    {
                        Vector2 d = RandomInCircle(MaxPassError * (1f - q));
                        Vector3 alvo = SetterSpotOf(side) + new Vector3(d.x, 0f, d.y);
                        LaunchTo(KeepOffNet(alvo, side), 65f, i, $"passe q={q:F2} {d.magnitude:F2} m do levantador");
                        break;
                    }
                case 1:
                    {
                        Vector2 mira = IsHuman[i] ? _armedAim : AiSetAim(i);
                        int alvoIdx = EscolherAtacante(i, mira);
                        if (alvoIdx < 0)
                        {
                            Vector2 f = RandomInCircle(MaxSetError * (1f - q));
                            LaunchTo(KeepOffNet(AttackSpotOf(side) + new Vector3(f.x, 0f, f.y), side), 70f, i, $"levantamento sem atacante q={q:F2}");
                            break;
                        }

                        bool meio = EffectiveRole(alvoIdx) == PlayerRole.Central;

                        // bola de meio é mais rasteira e mais perto da rede: mais rápida
                        float alturaZ = meio ? 0.9f : 1.3f;
                        float angulo = meio ? 52f : 70f;

                        Vector3 baseAlvo = new Vector3(Players[alvoIdx].Base.x, 3.00f, alturaZ * side);
                        Vector2 d = RandomInCircle(MaxSetError * (1f - q));
                        Vector3 alvo = baseAlvo + new Vector3(d.x, 0f, d.y);

                        LaunchTo(KeepOffNet(alvo, side), angulo, i, $"levantamento -> #{alvoIdx} {EffectiveRole(alvoIdx)} " + $"q={q:F2} {d.magnitude:F2}m do alvo");
                        break;
                    }
                default:
                    {
                        if (!IsFront(i) && Mathf.Abs(Ball.Position.z) < Court.AttackLine && Ball.Position.y > NetHeight)
                        {
                            EndRally(-side, "ataque de fundo à frente da linha de 3m");
                            break;
                        }

                        Vector2 mira = IsHuman[i] ? _armedAim : AiAim(i);
                        float baseX = Mathf.Clamp(mira.x * SpikeAimRange, -3.8f, 3.8f);

                        Vector2 d = RandomInCircle(MaxSpikeError * (1f - q));
                        Vector3 alvo = new Vector3(baseX + d.x, Court.BallRadius, (-SpikeDepth * side) + d.y);

                        float folga = Mathf.Lerp(0.60f, 0.10f, q);

                        if (BallPhysics.SolveFlattestLegal(Ball.Position, alvo, NetHeight, folga, 1f / 60f, out Vector3 v, -35f, 40f, 1f))
                        {
                            float ang = Mathf.Asin(v.normalized.y) * Mathf.Rad2Deg;
                            Ball = new BallState(Ball.Position, v);
                            Rally.OnTouch(i, side);
                            OnLog?.Invoke($"ATAQUE q={q:F2} {v.magnitude:F1} m/s a {ang:F0}º" + $" alvo x={baseX:F1} [toque {Rally.TouchCount}/3]");
                        } else
                        {
                            OnLog?.Invoke("sem angulo legal pro ataque");
                        }
                        break;
                    }
            }
        }

        /// <summary>Escolhe pra qual atacante vai a bola, pela mira lateral.</summary>
        private int EscolherAtacante(int levantador, Vector2 mira)
        {
            int side = Players[levantador].Side;
            int b = TeamBase(side);

            float desejadoX = Mathf.Clamp(mira.x * 3.6f, -3.8f, 3.8f);

            int alvo = -1;
            float melhor = float.MaxValue;

            for (int k = 0; k < 6; k++)
            {
                int j = b + k;
                if (j == levantador) continue;
                if (!IsFront(j)) continue;
                if (EffectiveRole(j) == PlayerRole.Libero) continue;

                float d = Mathf.Abs(Players[j].Base.x - desejadoX);
                if (d < melhor)
                {
                    melhor = d;
                    alvo = j;
                }
            }
            return alvo;
        }

        /// <summary>Resolve a balística de um toque e registra na máquina de estados.</summary>
        private void LaunchTo(Vector3 alvo, float angle, int playerIndex, string msg)
        {
            if (BallPhysics.SolveLaunch(Ball.Position, alvo, angle, 1f / 60f, out Vector3 v))
            {
                Ball = new BallState(Ball.Position, v);
                Rally.OnTouch(playerIndex, Players[playerIndex].Side);
                NovaLeitura();
                OnLog?.Invoke($"{msg} [toque {Rally.TouchCount}/3]");
            } else
            {
                OnLog?.Invoke($"solver falhou: de y={Ball.Position.y:F2} para {alvo}");
            }
        }

        /// <summary>Levantamento nunca é colocado em cima da rede nem do outro lado.</summary>
        private static Vector3 KeepOffNet(Vector3 alvo, int side)
        {
            const float minOff = 0.6f;
            if (Court.SideOf(alvo.z) != side || Mathf.Abs(alvo.z) < minOff)
            {
                alvo.z = minOff * side;
            }

            return alvo;
        }
    }
}
