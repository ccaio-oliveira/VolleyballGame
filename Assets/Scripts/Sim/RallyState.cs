using PlasticGui.WorkspaceWindow;

namespace Volley.Sim
{
    public enum RallyPhase { PreServe, InPlay, PointOver }

    /// <summary>Estado do rally e do placar. Reage a eventos, nunca roda sozinha.</summary>
    public class RallyState
    {
        public RallyPhase Phase = RallyPhase.PreServe;
        public int TouchingSide;
        public int TouchCount;
        public int LastToucher = -1;
        public int LastTouchSide;
        public int ServingSide = Court.SideA;
        public int ScoreA;
        public int ScoreB;
        public string LastReason = "";

        public void BeginServe(int side)
        {
            Phase = RallyPhase.InPlay;
            TouchingSide = side;
            TouchCount = 0;
            LastToucher = -1;
            LastTouchSide = side;
        }

        public void OnNetCrossed(int newSide)
        {
            TouchingSide = newSide;
            TouchCount = 0;
            LastToucher = -1;
        }

        /// <summary>Registra um toque. Retorna true se foi falta (4º toque).</summary>
        public bool OnTouch(int playerId, int side)
        {
            TouchCount++;
            LastToucher = playerId;
            LastTouchSide = side;
            return TouchCount > 3;
        }

        public void AwardPoint(int winnerSide, string reason)
        {
            if (winnerSide == Court.SideA) ScoreA++;
            else ScoreB++;

            ServingSide = winnerSide;
            LastReason = reason;
            Phase = RallyPhase.PointOver;
        }
    }
}