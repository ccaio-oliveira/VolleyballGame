using UnityEngine;
using Volley.Bootstrap;
using Volley.Sim;

namespace Volley.View
{
    /// <summary>Development scoreboard drawn with IMGUI. Placeholder until a real UI.</summary>
    public class ScoreboardView : MonoBehaviour
    {
        [SerializeField] private GameRoot root;

        private GUIStyle _largeStyle;
        private GUIStyle _smallStyle;

        private void OnGUI()
        {
            if (root == null) return;

            EnsureStyles();

            var match = root.Sim.Match;
            string server = root.Sim.Rally.ServingSide == Court.SideA ? "A" : "B";

            GUI.Label(new Rect(22, 12, 700, 60), $"A {match.PointsOf(Court.SideA)} - {match.PointsOf(Court.SideB)} B", _largeStyle);
            GUI.Label(new Rect(24, 64, 700, 26), $"sets {match.SetsOf(Court.SideA)}-{match.SetsOf(Court.SideB)} · " + $"set {match.SetNumber} até {match.PointsToWin} · " + $"saque: {server}", _smallStyle);

            if (match.Finished)
                GUI.Label(new Rect(22, 94, 700, 60), $"PARTIDA PARA {(match.MatchWinner == Court.SideA ? "A" : "B")}", _largeStyle);
        }

        private void EnsureStyles()
        {
            if (_largeStyle != null) return;

            _largeStyle = new GUIStyle(GUI.skin.label) { fontSize = 42, fontStyle = FontStyle.Bold };
            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 17 };
        }
    }
}
