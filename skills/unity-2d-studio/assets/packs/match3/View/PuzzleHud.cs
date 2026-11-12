using System.Text;
using TMPro;
using UnityEngine;

namespace __NS__.Puzzle.View
{
    // Moves + goals text from board events; no polling. Replace the texts with icons and counters when real UI art exists.
    public class PuzzleHud : MonoBehaviour
    {
        [SerializeField] private BoardView _board;
        [SerializeField] private TMP_Text _moves;
        [SerializeField] private TMP_Text _goals;
        [SerializeField] private TMP_Text _result;

        private readonly StringBuilder _text = new StringBuilder(64);

        private void OnEnable()
        {
            _board.Events.MovesChanged += OnMovesChanged;
            _board.Events.GoalProgressed += OnGoalProgressed;
            _board.Events.LevelEnded += OnLevelEnded;
        }

        private void OnDisable()
        {
            _board.Events.MovesChanged -= OnMovesChanged;
            _board.Events.GoalProgressed -= OnGoalProgressed;
            _board.Events.LevelEnded -= OnLevelEnded;
        }

        private void Start()
        {
            if (_result != null) _result.text = string.Empty;
            if (_board.Game == null) return;
            OnMovesChanged(_board.Game.MovesLeft);
            OnGoalProgressed(0, 0);
        }

        private void OnMovesChanged(int left)
        {
            if (_moves != null) _moves.SetText("{0}", left);
        }

        private void OnGoalProgressed(int index, int value)
        {
            if (_goals == null || _board.Game == null) return;
            var game = _board.Game;
            _text.Clear();
            for (var i = 0; i < game.Level.Goals.Count; i++)
            {
                var g = game.Level.Goals[i];
                if (i > 0) _text.Append("   ");
                _text.Append(g.Name).Append(' ').Append(Mathf.Max(0, g.Target - game.GoalProgress(i)));
            }

            _goals.SetText(_text);
        }

        private void OnLevelEnded(bool won)
        {
            if (_result != null) _result.text = won ? "LEVEL COMPLETE" : "OUT OF MOVES";
        }
    }
}
