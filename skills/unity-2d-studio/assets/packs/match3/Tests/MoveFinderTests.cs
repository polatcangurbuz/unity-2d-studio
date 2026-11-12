using System.Linq;
using NUnit.Framework;

namespace __NS__.Puzzle.Tests
{
    public class MoveFinderTests
    {
        [Test]
        public void FindAll_RowSetup_ContainsMatchingSwap()
        {
            Board board = new GridBuilder().Set(0, 2, "0").Set(1, 2, "0").Set(2, 1, "0").Build().Board;

            var moves = MoveFinder.FindAll(board);

            Assert.That(moves.Any(m => m.A == StepQuery.P(2, 1) && m.B == StepQuery.P(2, 2)), Is.True);
        }

        [Test]
        public void HasMove_DiagonalStripes_False()
        {
            Level level = LevelParser.Parse("moves: 5\ncolors: 4\ngoals: red=9\ngrid:\n0 1 2 3\n1 2 3 0\n2 3 0 1\n3 0 1 2\n");

            Assert.That(MoveFinder.HasMove(new PuzzleGame(level).Board), Is.False);
        }

        [Test]
        public void IsValid_SpecialNextToAnyPiece_True()
        {
            Board board = new GridBuilder().Set(2, 2, "3B").Build().Board;

            Assert.That(MoveFinder.IsValid(board, StepQuery.P(2, 2), StepQuery.P(2, 3)), Is.True);
        }

        [Test]
        public void IsValid_ChainedPiece_False()
        {
            Board board = new GridBuilder().Set(0, 2, "0").Set(1, 2, "0").Set(2, 1, "L:0").Build().Board;

            Assert.That(MoveFinder.IsValid(board, StepQuery.P(2, 1), StepQuery.P(2, 2)), Is.False);
        }

        [Test]
        public void TryGetBest_FiveAndThreeAvailable_PicksFive()
        {
            PuzzleGame game = new GridBuilder()
                .Set(0, 2, "0").Set(1, 2, "0").Set(3, 2, "0").Set(4, 2, "0").Set(2, 1, "0")
                .Set(0, 5, "0").Set(1, 5, "0").Set(2, 4, "0")
                .Build();

            MoveFinder.TryGetBest(game, out Swap best);

            Assert.That(new[] { best.A, best.B }, Is.EqualTo(new[] { StepQuery.P(2, 1), StepQuery.P(2, 2) }));
        }

        [Test]
        public void TryGetBest_ApplyingIt_IsAccepted()
        {
            var game = new PuzzleGame(LevelParser.Parse(StepQuery.OpenLevel(9, 20, "red=10")), 4);

            MoveFinder.TryGetBest(game, out Swap best);

            Assert.That(game.TrySwap(best.A, best.B).Accepted, Is.True);
        }
    }
}
