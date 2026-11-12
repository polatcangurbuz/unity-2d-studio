using System.Globalization;
using System.Text;

namespace __NS__.Puzzle.Tests
{
    // Background colors 1..4 never form a line, so 0 (red) is free for each test's own shapes.
    public sealed class GridBuilder
    {
        private readonly string[,] _cells;

        public GridBuilder(int width = 6, int height = 6)
        {
            _cells = new string[height, width];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++) _cells[y, x] = (1 + (x + 2 * y) % 4).ToString(CultureInfo.InvariantCulture);
            }
        }

        public GridBuilder Set(int x, int y, string token)
        {
            _cells[y, x] = token;
            return this;
        }

        public string ToLevelText(int moves, string goals)
        {
            var sb = new StringBuilder();
            sb.Append("moves: ").Append(moves).Append('\n');
            sb.Append("colors: 5\nseed: 7\n");
            sb.Append("goals: ").Append(goals).Append('\n');
            sb.Append("grid:\n");
            for (int y = 0; y < _cells.GetLength(0); y++)
            {
                for (int x = 0; x < _cells.GetLength(1); x++) sb.Append(x == 0 ? "" : " ").Append(_cells[y, x]);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        public PuzzleGame Build(int moves = 20, string goals = "red=99") => new PuzzleGame(LevelParser.Parse(ToLevelText(moves, goals)));
    }
}
