namespace Volley.Sim
{
    // Enums are serialized by Unity as integers. Explicit values keep saved scenes
    // valid if the declaration order ever changes: always append, never insert.

    /// <summary>Permanent function of an athlete in the 5-1 system.</summary>
    public enum PlayerRole
    {
        Setter        = 0,
        Opposite      = 1,
        OutsideHitter = 2,
        MiddleBlocker = 3,
        Libero        = 4,
    }

    /// <summary>What a team is doing right now; decides where each player stands.</summary>
    public enum TeamPhase
    {
        Serve     = 0,
        Reception = 1,
        Attack    = 2,
        Defense   = 3,
    }

    /// <summary>Semantic events emitted by the simulation for audio, camera and effects.</summary>
    public enum SimEventKind
    {
        Serve      = 0,
        Pass       = 1,
        Set        = 2,
        Attack     = 3,
        Block      = 4,
        BallLanded = 5,
        Point      = 6,
        SetWon     = 7,
        MatchWon   = 8,
    }

    public enum RallyPhase
    {
        PreServe  = 0,
        InPlay    = 1,
        PointOver = 2,
    }

    public enum RallyOutcome
    {
        Point    = 0,
        SetWon   = 1,
        MatchWon = 2,
    }

    public enum AiLevel
    {
        Easy   = 0,
        Normal = 1,
        Hard   = 2,
        Pro    = 3,
    }
}
