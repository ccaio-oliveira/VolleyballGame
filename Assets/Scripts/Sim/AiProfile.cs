namespace Volley.Sim
{
    /// <summary>
    /// AI difficulty. Every field degrades PERCEPTION, not execution: a read error turns
    /// into displacement and displacement into a poor touch — through the same chain that
    /// governs the human player.
    /// </summary>
    public struct AiProfile
    {
        public string Name;            // display name
        public float ReadError;        // meters of error when reading the contact point
        public float ReadDelay;        // seconds before reacting to a new ball
        public float BlockError;       // meters of lateral error when setting up the block
        public float AimNoise;         // meters of noise when choosing a target
        public float QualityCap;       // maximum quality of a touch
        public float BlockChance;      // probability of going up to block

        public static AiProfile Easy => new AiProfile
        {
            Name = "Fácil",
            ReadError = 1.40f,
            ReadDelay = 0.30f,
            BlockError = 0.90f,
            AimNoise = 1.80f,
            QualityCap = 0.68f,
            BlockChance = 0.40f,
        };

        public static AiProfile Normal => new AiProfile
        {
            Name = "Normal",
            ReadError = 0.75f,
            ReadDelay = 0.16f,
            BlockError = 0.45f,
            AimNoise = 0.95f,
            QualityCap = 0.85f,
            BlockChance = 0.68f,
        };

        public static AiProfile Hard => new AiProfile
        {
            Name = "Difícil",
            ReadError = 0.35f,
            ReadDelay = 0.08f,
            BlockError = 0.20f,
            AimNoise = 0.45f,
            QualityCap = 0.95f,
            BlockChance = 0.86f,
        };

        public static AiProfile Pro => new AiProfile
        {
            Name = "Profissional",
            ReadError = 0.10f,
            ReadDelay = 0.03f,
            BlockError = 0.07f,
            AimNoise = 0.15f,
            QualityCap = 1.00f,
            BlockChance = 0.97f,
        };

        public static AiProfile From(AiLevel level)
        {
            switch (level)
            {
                case AiLevel.Easy: return Easy;
                case AiLevel.Hard: return Hard;
                case AiLevel.Pro:  return Pro;
                default:           return Normal;
            }
        }
    }
}
