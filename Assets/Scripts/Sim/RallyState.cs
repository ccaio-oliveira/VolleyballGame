namespace Volley.Sim
{
    /// <summary>State of the current rally. Reacts to events; never runs on its own.</summary>
    public class RallyState
    {
        public RallyPhase Phase = RallyPhase.PreServe;
        public int TouchingSide;             // side in possession (territory)
        public int TouchCount;               // 0..3
        public int LastToucher = -1;         // player index, -1 = nobody
        public int LastTouchSide;            // who touched last (responsibility) — survives net crossings
        public int ServingSide = Court.SideA;
        public string LastReason = "";
        public bool ServeInFlight;

        public void BeginServe(int side)
        {
            Phase = RallyPhase.InPlay;
            TouchingSide = side;
            TouchCount = 0;
            LastToucher = -1;
            LastTouchSide = side;   // the serve counts as a touch
            ServeInFlight = true;
        }

        public void OnNetCrossed(int newSide)
        {
            TouchingSide = newSide;
            TouchCount = 0;
            LastToucher = -1;
            // LastTouchSide stays: flying over is not touching
        }

        /// <summary>Records a touch. Returns true when it was a fault (4th touch).</summary>
        public bool OnTouch(int playerId, int side)
        {
            TouchCount++;
            LastToucher = playerId;
            LastTouchSide = side;
            ServeInFlight = false;   // the serve never passes through here, so this is the reception
            return TouchCount > 3;
        }

        /// <summary>Block touch: marks who touched but does NOT consume one of the three touches.</summary>
        public void OnBlockTouch(int playerId, int side)
        {
            LastToucher = playerId;
            LastTouchSide = side;
        }

        public void AwardPoint(int winnerSide, string reason)
        {
            ServingSide = winnerSide;   // rally point: whoever wins the point serves
            LastReason = reason;
            Phase = RallyPhase.PointOver;
        }
    }
}
