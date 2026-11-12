using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace __NS__.Puzzle.Tests
{
    public static class StepQuery
    {
        public static BoardPos P(int x, int y) => new BoardPos(x, y);

        // Later waves depend on random refills, so most assertions look at wave 0 only.
        public static List<Step> Wave0(TurnResult result, StepKind kind) =>
            result.Steps.Where(s => s.Kind == kind && s.Wave == 0).ToList();

        public static Step First(TurnResult result, StepKind kind) => result.Steps.First(s => s.Kind == kind);

        public static List<BoardPos> Row(int y, int width) => Enumerable.Range(0, width).Select(x => P(x, y)).ToList();

        public static List<BoardPos> Column(int x, int height) => Enumerable.Range(0, height).Select(y => P(x, y)).ToList();

        public static string Dump(TurnResult result)
        {
            var sb = new StringBuilder();
            foreach (Step s in result.Steps) sb.Append(s).Append('\n');
            return sb.ToString();
        }

        public static string OpenLevel(int size, int moves, string goals, int seed = 1)
        {
            var sb = new StringBuilder();
            sb.Append("moves: ").Append(moves).Append("\ncolors: 5\nseed: ").Append(seed).Append("\ngoals: ").Append(goals).Append("\ngrid:\n");
            for (int y = 0; y < size; y++) sb.Append(string.Join(" ", Enumerable.Repeat(".", size))).Append('\n');
            return sb.ToString();
        }
    }
}
