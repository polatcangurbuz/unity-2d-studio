using System.Linq;
using NUnit.Framework;

namespace __NS__.Puzzle.Tests
{
    public class BlockerTests
    {
        // Red row (0,2)-(2,2) made by dropping the red at (2,1) into (2,2).
        private static GridBuilder RowMatch() => new GridBuilder().Set(0, 2, "0").Set(1, 2, "0").Set(2, 1, "0");

        private static TurnResult Play(PuzzleGame game) => game.TrySwap(StepQuery.P(2, 1), StepQuery.P(2, 2));

        [Test]
        public void Crate_NextToMatch_LosesOneHp()
        {
            TurnResult result = Play(RowMatch().Set(1, 3, "C2").Build());

            Step hit = StepQuery.Wave0(result, StepKind.BlockerHit).Single(s => s.Layer == BlockerLayer.Crate);
            Assert.That(hit.A, Is.EqualTo(StepQuery.P(1, 3)));
            Assert.That(hit.Value, Is.EqualTo(1));
        }

        [Test]
        public void Crate_LastHp_CountsTowardGoal()
        {
            PuzzleGame game = RowMatch().Set(1, 3, "C1").Build(goals: "crate=2");

            Play(game);

            Assert.That(game.GoalProgress(0), Is.EqualTo(1));
            Assert.That(game.Board.CrateHp(StepQuery.P(1, 3)), Is.EqualTo(0));
        }

        [Test]
        public void Ice_PieceOnTopCleared_LosesHp()
        {
            PuzzleGame game = RowMatch().Set(0, 2, "I2:0").Build();

            TurnResult result = Play(game);

            Step hit = StepQuery.Wave0(result, StepKind.BlockerHit).Single(s => s.Layer == BlockerLayer.Ice);
            Assert.That(hit.Value, Is.EqualTo(1));
        }

        [Test]
        public void Chain_InMatch_BreaksWithoutClearingPiece()
        {
            PuzzleGame game = RowMatch().Set(0, 2, "L:0").Build();
            int id = game.Board.PieceAt(StepQuery.P(0, 2)).Id;

            TurnResult result = Play(game);

            Assert.That(StepQuery.Wave0(result, StepKind.BlockerHit).Any(s => s.Layer == BlockerLayer.Chain && s.Value == 0), Is.True);
            Assert.That(StepQuery.Wave0(result, StepKind.Cleared).Any(s => s.PieceId == id), Is.False);
        }

        [Test]
        public void Chain_ChainedPiece_CannotBeSwapped()
        {
            PuzzleGame game = new GridBuilder().Set(2, 2, "L:3").Build();

            TurnResult result = game.TrySwap(StepQuery.P(2, 2), StepQuery.P(3, 2));

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Steps, Is.Empty);
        }

        [Test]
        public void Chain_HitBySpecial_Breaks()
        {
            PuzzleGame game = new GridBuilder().Set(0, 2, "3H").Set(4, 2, "L:1").Build();

            TurnResult result = game.UseBooster(BoosterKind.Hammer, StepQuery.P(0, 2));

            Step hit = StepQuery.Wave0(result, StepKind.BlockerHit).Single();
            Assert.That(hit.A, Is.EqualTo(StepQuery.P(4, 2)));
            Assert.That(hit.Layer, Is.EqualTo(BlockerLayer.Chain));
        }
    }
}
