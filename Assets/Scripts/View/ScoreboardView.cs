using UnityEngine;
using Volley.Bootstrap;
using Volley.Sim;

namespace Volley.View
{
    public class ScoreboardView : MonoBehaviour
    {
        [SerializeField] private GameRoot root;
        
        private GUIStyle _grande, _pequeno;

        private void OnGUI()
        {
            if (root == null) return;

            if (_grande == null)
            {
                _grande = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 42,
                    fontStyle = FontStyle.Bold
                };

                _pequeno = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17
                };

                var m = root.Sim.Match;

                GUI.Label(new Rect(22, 12, 700, 60), $"A {m.PointsOf(Court.SideA)} - {m.PointsOf(Court.SideB)} B", _grande);
                GUI.Label(new Rect(24, 64, 700, 26), $"sets {m.SetsOf(Court.SideA)}-{m.SetsOf(Court.SideB)} · " + $"set {m.SetNumber} até {m.PointsToWin} · " + $"saque: {(root.Sim.Rally.ServingSide == Court.SideA ? "A" : "B")}", _pequeno);

                if (m.Finished)
                {
                    GUI.Label(new Rect(22, 94, 700, 60), $"PARTIDA PARA {(m.MatchWinner == Court.SideA ? "A" : "B")}", _grande);
                }
            }
        }
    }
}