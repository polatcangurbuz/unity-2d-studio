using NUnit.Framework;

namespace __NS__.Puzzle.Tests
{
    public class ShuffleTests
    {
        // Diagonal stripes: no swap can line up three, but a reshuffle can.
        private const string Stuck = "moves: 10\ncolors: 4\ngoals: red=99\ngrid:\n0 1 2 3\n1 2 3 0\n2 3 0 1\n3 0 1 C9\n";

        [Test]
        public void TurnEnd_NoValidMove_BoardShuffled()
        {
            var game = new PuzzleGame(LevelParser.Parse(Stuck));

            TurnResult result = game.UseBooster(BoosterKind.Hammer, StepQuery.P(3, 3));

            Assert.That(result.Shuffled, Is.True);
            Assert.That(StepQuery.Wave0(result, StepKind.Shuffled), Is.Not.Empty);
        }

        [Test]
        public void TurnEnd_AfterShuffle_HasMoveAndNoMatch()
        {
            var game = new PuzzleGame(LevelParser.Parse(Stuck));

            game.UseBooster(BoosterKind.Hammer, StepQuery.P(3, 3));

            Assert.That(MoveFinder.HasMove(game.Board), Is.True);
            Assert.That(MatchFinder.HasAny(game.Board), Is.False);
        }

        [Test]
        public void UseBooster_Shuffle_KeepsChainedPiece()
        {
            PuzzleGame game = new GridBuilder().Set(0, 0, "L:1").Build();
            int id = game.Board.PieceAt(StepQuery.P(0, 0)).Id;

            TurnResult result = game.UseBooster(BoosterKind.Shuffle, StepQuery.P(0, 0));

            Assert.That(result.Shuffled, Is.True);
            Assert.That(game.Board.PieceAt(StepQuery.P(0, 0)).Id, Is.EqualTo(id));
        }
    }
}
