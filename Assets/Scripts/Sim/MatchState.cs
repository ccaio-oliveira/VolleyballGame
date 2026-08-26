namespace Volley.Sim
{
    public enum RallyOutcome { Ponto, Set, Partida }

    /// <summary>Placar da partida. Não sabe nada de bola nem de jogadores.</summary>
    public class MatchState
    {
        public int[] Points = new int[2];
        public int[] SetsWon = new int[2];
        public int SetNumber = 1;
        public bool Finished;
        public int MatchWinner;

        private static int Idx(int side) => side == Court.SideA ? 0 : 1;

        public int PointsToWin => (SetNumber == 5) ? 15 : 25;

        public int PointsOf(int side) => Points[Idx(side)];
        public int SetsOf(int side) => SetsWon[Idx(side)];

        public RallyOutcome AddPoint(int side)
        {
            if (Finished) return RallyOutcome.Ponto;

            int eu = Idx(side), ele = 1 - eu;
            Points[eu]++;

            // set só termina com o alvo atingido E dois de vantagem
            if (Points[eu] < PointsToWin) return RallyOutcome.Ponto;
            if (Points[eu] - Points[ele] < 2) return RallyOutcome.Ponto;

            SetsWon[eu]++;
            Points[0] = Points[1] = 0;

            if (SetsWon[eu] == 3)
            {
                Finished = true;
                MatchWinner = side;
                return RallyOutcome.Partida;
            }

            SetNumber++;
            return RallyOutcome.Set;
        }
    }
}