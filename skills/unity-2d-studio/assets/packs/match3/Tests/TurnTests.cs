using System.Linq;
using NUnit.Framework;

namespace __NS__.Puzzle.Tests
{
    public class TurnTests
    {
        private static GridBuilder RowMatch() => new GridBuilder().Set(0, 2, "0").Set(1, 2, "0").Set(2, 1, "0");

        private static TurnResult Play(PuzzleGame game) => game.TrySwap(StepQuery.P(2, 1), StepQuery.P(2, 2));

        [Test]
        public void TrySwap_NoMatch_DoesNotSpendMove()
        {
            PuzzleGame game = new GridBuilder().Build(moves: 10);

            TurnResult result = game.TrySwap(StepQuery.P(0, 0), StepQuery.P(1, 0));

            Assert.That(result.Accepted, Is.False);
            Assert.That(game.MovesLeft, Is.EqualTo(10));
        }

        [Test]
        public void TrySwap_NoMatch_EmitsOnlySwapRejected()
        {
            PuzzleGame game = new GridBuilder().Build();
            int before = game.Board.PieceAt(StepQuery.P(0, 0)).Id;

            TurnResult result = game.TrySwap(StepQuery.P(0, 0), StepQuery.P(1, 0));

            Assert.That(result.Steps.Select(s => s.Kind), Is.EqualTo(new[] { StepKind.SwapRejected }));
            Assert.That(game.Board.PieceAt(StepQuery.P(0, 0)).Id, Is.EqualTo(before));
        }

        [Test]
        public void TrySwap_NotAdjacent_NotAccepted()
        {
            PuzzleGame game = new GridBuilder().Build();

            TurnResult result = game.TrySwap(StepQuery.P(0, 0), StepQuery.P(2, 0));

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Steps, Is.Empty);
        }

        [Test]
        public void TrySwap_Match_SpendsOneMove()
        {
            PuzzleGame game = RowMatch().Build(moves: 10);

            Play(game);

            Assert.That(game.MovesLeft, Is.EqualTo(9));
        }

        [Test]
        public void TrySwap_Match_FirstStepIsSwapped()
        {
            TurnResult result = Play(RowMatch().Build());

            Assert.That(result.Steps[0].Kind, Is.EqualTo(StepKind.Swapped));
        }

        [Test]
        public void UseBooster_Hammer_DoesNotSpendMove()
        {
            PuzzleGame game = new GridBuilder().Build(moves: 10);

            TurnResult result = game.UseBooster(BoosterKind.Hammer, StepQuery.P(3, 3));

            Assert.That(result.Accepted, Is.True);
            Assert.That(game.MovesLeft, Is.EqualTo(10));
        }

        [Test]
        public void UseBooster_Row_ClearsWholeRow()
        {
            TurnResult result = new GridBuilder().Build().UseBooster(BoosterKind.Row, StepQuery.P(4, 3));

            var cleared = StepQuery.Wave0(result, StepKind.Cleared).Where(s => s.Cause == ClearCause.Booster).Select(s => s.A);
            Assert.That(cleared, Is.EquivalentTo(StepQuery.Row(3, 6)));
        }

        [Test]
        public void UseBooster_Column_ClearsWholeColumn()
        {
            TurnResult result = new GridBuilder().Build().UseBooster(BoosterKind.Column, StepQuery.P(4, 3));

            var cleared = StepQuery.Wave0(result, StepKind.Cleared).Where(s => s.Cause == ClearCause.Booster).Select(s => s.A);
            Assert.That(cleared, Is.EquivalentTo(StepQuery.Column(4, 6)));
        }

        [Test]
        public void TrySwap_GoalsMet_LevelWon()
        {
            PuzzleGame game = RowMatch().Build(goals: "red=3");

            TurnResult result = Play(game);

            Assert.That(game.State, Is.EqualTo(GameState.Won));
            Assert.That(result.Steps.Last().Kind, Is.EqualTo(StepKind.LevelEnded));
            Assert.That(result.Steps.Last().Value, Is.EqualTo(1));
        }

        [Test]
        public void TrySwap_LastMoveGoalsUnmet_LevelLost()
        {
            PuzzleGame game = RowMatch().Build(moves: 1, goals: "red=99");

            TurnResult result = Play(game);

            Assert.That(game.State, Is.EqualTo(GameState.Lost));
            Assert.That(result.Steps.Last().Value, Is.EqualTo(0));
        }

        [Test]
        public void TrySwap_AfterLevelEnded_NotAccepted()
        {
            PuzzleGame game = RowMatch().Set(4, 4, "3H").Build(moves: 1);
            Play(game);

            TurnResult result = game.TrySwap(StepQuery.P(4, 4), StepQuery.P(4, 5));

            Assert.That(result.Accepted, Is.False);
        }

        [Test]
        public void TrySwap_RedCleared_ProgressStepReported()
        {
            TurnResult result = Play(RowMatch().Build(goals: "red=10"));

            var progress = StepQuery.Wave0(result, StepKind.GoalProgressed).Select(s => s.Value);
            Assert.That(progress, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void TrySwap_Cascades_StepWavesNeverDecrease()
        {
            var game = new PuzzleGame(LevelParser.Parse(StepQuery.OpenLevel(8, 30, "red=999")), 11);

            for (int turn = 0; turn < 20 && MoveFinder.TryGetBest(game, out Swap move); turn++)
            {
                TurnResult result = game.TrySwap(move.A, move.B);
                for (int i = 1; i < result.Steps.Count; i++) Assert.That(result.Steps[i].Wave, Is.GreaterThanOrEqualTo(result.Steps[i - 1].Wave));
            }
        }
    }
}
