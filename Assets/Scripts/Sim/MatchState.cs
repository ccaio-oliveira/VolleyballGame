namespace Volley.Sim
{
    /// <summary>Match score. Knows nothing about the ball or the players.</summary>
    public class MatchState
    {
        public int[] Points = new int[2];    // index 0 = side A, 1 = side B
        public int[] SetsWon = new int[2];
        public int SetNumber = 1;
        public bool Finished;
        public int MatchWinner;

        /// <summary>25 points per set, 15 in the fifth (tie-break).</summary>
        public int PointsToWin => (SetNumber == 5) ? 15 : 25;

        public int PointsOf(int side) => Points[Index(side)];

        public int SetsOf(int side) => SetsWon[Index(side)];

        public RallyOutcome AddPoint(int side)
        {
            if (Finished) return RallyOutcome.Point;

            int mine = Index(side);
            int theirs = 1 - mine;
            Points[mine]++;

            // a set only ends with the target reached AND a two-point lead
            if (Points[mine] < PointsToWin) return RallyOutcome.Point;
            if (Points[mine] - Points[theirs] < 2) return RallyOutcome.Point;

            SetsWon[mine]++;
            Points[0] = Points[1] = 0;

            if (SetsWon[mine] == 3)
            {
                Finished = true;
                MatchWinner = side;
                return RallyOutcome.MatchWon;
            }

            SetNumber++;
            return RallyOutcome.SetWon;
        }

        private static int Index(int side) => side == Court.SideA ? 0 : 1;
    }
}
