using System.Collections.Generic;
using NUnit.Framework;

namespace __NS__.Puzzle.Tests
{
    public class BoardSetupTests
    {
        private static Level Open9 => LevelParser.Parse(StepQuery.OpenLevel(9, 20, "red=10"));

        [Test]
        public void NewGame_ManySeeds_StartsWithoutMatches()
        {
            Level level = Open9;

            for (int seed = 1; seed <= 50; seed++) Assert.That(MatchFinder.HasAny(new PuzzleGame(level, seed).Board), Is.False, $"seed {seed}");
        }

        [Test]
        public void NewGame_ManySeeds_HasValidMove()
        {
            Level level = Open9;

            for (int seed = 1; seed <= 50; seed++) Assert.That(MoveFinder.HasMove(new PuzzleGame(level, seed).Board), Is.True, $"seed {seed}");
        }

        [Test]
        public void NewGame_AllCells_HaveUniquePieceIds()
        {
            Board board = new PuzzleGame(Open9, 3).Board;
            var ids = new HashSet<int>();

            for (int y = 0; y < 9; y++)
            {
                for (int x = 0; x < 9; x++) Assert.That(ids.Add(board.PieceAt(StepQuery.P(x, y)).Id), Is.True);
            }
        }

        [Test]
        public void NewGame_SameSeed_SameBoard()
        {
            Level level = Open9;

            Assert.That(new PuzzleGame(level, 5).Board.ToString(), Is.EqualTo(new PuzzleGame(level, 5).Board.ToString()));
        }

        [Test]
        public void NewGame_LayersFromLevel_KeepHolesEmpty()
        {
            Level level = LevelParser.Parse("moves: 5\ncolors: 5\ngoals: red=3\ngrid:\n. . . .\n. # . .\n. . C1 .\n. . . .\n");

            Board board = new PuzzleGame(level).Board;

            Assert.That(board.HasPiece(StepQuery.P(1, 1)), Is.False);
            Assert.That(board.HasPiece(StepQuery.P(2, 2)), Is.False);
        }
    }
}
