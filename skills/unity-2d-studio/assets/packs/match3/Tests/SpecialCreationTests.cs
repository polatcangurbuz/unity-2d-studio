using NUnit.Framework;

namespace __NS__.Puzzle.Tests
{
    public class SpecialCreationTests
    {
        private static TurnResult SwapDownInto(GridBuilder grid) => grid.Build().TrySwap(StepQuery.P(2, 1), StepQuery.P(2, 2));

        [Test]
        public void TrySwap_ThreeInRow_MatchesWithoutSpecial()
        {
            var grid = new GridBuilder().Set(0, 2, "0").Set(1, 2, "0").Set(2, 1, "0");

            Step matched = StepQuery.First(SwapDownInto(grid), StepKind.Matched);

            Assert.That(matched.Cells.Count, Is.EqualTo(3));
            Assert.That(matched.Special, Is.EqualTo(Special.None));
        }

        [Test]
        public void TrySwap_FourInRow_CreatesVerticalRocket()
        {
            var grid = new GridBuilder().Set(0, 2, "0").Set(1, 2, "0").Set(3, 2, "0").Set(2, 1, "0");

            Step matched = StepQuery.First(SwapDownInto(grid), StepKind.Matched);

            Assert.That(matched.Special, Is.EqualTo(Special.RocketV));
        }

        [Test]
        public void TrySwap_FourInColumn_CreatesHorizontalRocket()
        {
            var grid = new GridBuilder().Set(2, 0, "0").Set(2, 1, "0").Set(2, 3, "0").Set(1, 2, "0");

            Step matched = StepQuery.First(grid.Build().TrySwap(StepQuery.P(1, 2), StepQuery.P(2, 2)), StepKind.Matched);

            Assert.That(matched.Special, Is.EqualTo(Special.RocketH));
        }

        [Test]
        public void TrySwap_FourInRow_SpecialAppearsAtSwappedCell()
        {
            var grid = new GridBuilder().Set(0, 2, "0").Set(1, 2, "0").Set(3, 2, "0").Set(2, 1, "0");

            Step matched = StepQuery.First(SwapDownInto(grid), StepKind.Matched);

            Assert.That(matched.A, Is.EqualTo(StepQuery.P(2, 2)));
        }

        [Test]
        public void TrySwap_LShape_CreatesBomb()
        {
            var grid = new GridBuilder().Set(0, 2, "0").Set(1, 2, "0").Set(2, 3, "0").Set(2, 4, "0").Set(3, 2, "0");

            Step matched = StepQuery.First(grid.Build().TrySwap(StepQuery.P(3, 2), StepQuery.P(2, 2)), StepKind.Matched);

            Assert.That(matched.Special, Is.EqualTo(Special.Bomb));
            Assert.That(matched.Cells.Count, Is.EqualTo(5));
        }

        [Test]
        public void TrySwap_TShape_CreatesBomb()
        {
            var grid = new GridBuilder().Set(1, 2, "0").Set(3, 2, "0").Set(2, 3, "0").Set(2, 4, "0").Set(2, 1, "0");

            Step matched = StepQuery.First(SwapDownInto(grid), StepKind.Matched);

            Assert.That(matched.Special, Is.EqualTo(Special.Bomb));
        }

        [Test]
        public void TrySwap_FiveInRow_CreatesColorBomb()
        {
            var grid = new GridBuilder().Set(0, 2, "0").Set(1, 2, "0").Set(3, 2, "0").Set(4, 2, "0").Set(2, 1, "0");

            Step matched = StepQuery.First(SwapDownInto(grid), StepKind.Matched);

            Assert.That(matched.Special, Is.EqualTo(Special.ColorBomb));
        }

        [Test]
        public void TrySwap_SpecialCreated_PlacedOnBoardAfterWave()
        {
            var grid = new GridBuilder().Set(0, 2, "0").Set(1, 2, "0").Set(3, 2, "0").Set(2, 1, "0");
            PuzzleGame game = grid.Build();

            TurnResult result = game.TrySwap(StepQuery.P(2, 1), StepQuery.P(2, 2));

            Step matched = StepQuery.First(result, StepKind.Matched);
            Assert.That(result.SpecialsCreated, Is.GreaterThanOrEqualTo(1));
            Assert.That(matched.PieceId, Is.Not.EqualTo(0));
        }
    }
}
