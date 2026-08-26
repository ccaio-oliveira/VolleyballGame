namespace Volley.Sim
{
    public enum AiLevel { Facil, Normal, Dificil, Profissional }

    /// <summary>
    /// Dificuldade da IA. Todos os campos degradam PERCEPÇÃO, não execução:
    /// erro de leitura vira deslocamento, deslocamento vira toque ruim - pela mesma cadeia
    /// que governa o jogador humano.
    /// </summray>
    public struct AiProfile
    {
        public string Nome;
        public float ErroLeitura;
        public float AtrasoLeitura;
        public float ErroBloqueio;
        public float RuidoMira;
        public float TetoQualidade;
        public float ChanceBloqueio;

        public static AiProfile Facil => new AiProfile
        {
            Nome = "Fácil",
            ErroLeitura = 1.40f,
            AtrasoLeitura = 0.30f,
            RuidoMira = 1.80f,
            TetoQualidade = 0.68f,
            ChanceBloqueio = 0.40f
        };

        public static AiProfile Normal => new AiProfile
        {
            Nome = "Normal",
            ErroLeitura = 0.75f,
            AtrasoLeitura = 0.16f,
            RuidoMira = 0.95f,
            TetoQualidade = 0.85f,
            ChanceBloqueio = 0.68f
        };

        public static AiProfile Dificil => new AiProfile
        {
            Nome = "Difícil",
            ErroLeitura = 0.35f,
            AtrasoLeitura = 0.08f,
            ErroBloqueio = 0.20f,
            RuidoMira = 0.45f,
            TetoQualidade = 0.95f,
            ChanceBloqueio = 0.86f
        };

        public static AiProfile Profissional => new AiProfile
        {
            Nome = "Profissional",
            ErroLeitura = 0.10f,
            AtrasoLeitura = 0.03f,
            ErroBloqueio = 0.07f,
            RuidoMira = 0.15f,
            TetoQualidade = 1.00f,
            ChanceBloqueio = 0.97f
        };

        public static AiProfile From(AiLevel n)
        {
            switch (n)
            {
                case AiLevel.Facil: return Facil;
                case AiLevel.Dificil: return Dificil;
                case AiLevel.Profissional: return Profissional;
                default: return Normal;
            }
        }
    }
}