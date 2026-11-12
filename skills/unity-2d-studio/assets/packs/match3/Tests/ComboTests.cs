using System.Linq;
using NUnit.Framework;

namespace __NS__.Puzzle.Tests
{
    public class ComboTests
    {
        private static TurnResult SwapRight(GridBuilder grid, int x, int y) =>
            grid.Build().TrySwap(StepQuery.P(x, y), StepQuery.P(x + 1, y));

        [Test]
        public void RocketPlusRocket_Swapped_HitsRowAndColumn()
        {
            TurnResult result = SwapRight(new GridBuilder().Set(2, 2, "3H").Set(3, 2, "4V"), 2, 2);

            Step fired = StepQuery.First(result, StepKind.SpecialFired);
            var expected = StepQuery.Row(2, 6).Union(StepQuery.Column(3, 6));
            Assert.That(fired.Cells, Is.EquivalentTo(expected));
        }

        [Test]
        public void RocketPlusRocket_Swapped_CentredOnDropCell()
        {
            TurnResult result = SwapRight(new GridBuilder().Set(2, 2, "3H").Set(3, 2, "4V"), 2, 2);

            Step fired = StepQuery.First(result, StepKind.SpecialFired);
            Assert.That(fired.A, Is.EqualTo(StepQuery.P(3, 2)));
            Assert.That(fired.Partner, Is.EqualTo(Special.RocketV));
        }

        [Test]
        public void RocketPlusBomb_Swapped_HitsThreeRowsAndThreeColumns()
        {
            TurnResult result = SwapRight(new GridBuilder().Set(2, 2, "3H").Set(3, 2, "4B"), 2, 2);

            Step fired = StepQuery.First(result, StepKind.SpecialFired);
            Assert.That(fired.Cells.Count, Is.EqualTo(27));
        }

        [Test]
        public void BombPlusBomb_Swapped_HitsFiveByFive()
        {
            TurnResult result = SwapRight(new GridBuilder().Set(2, 3, "3B").Set(3, 3, "4B"), 2, 3);

            Step fired = StepQuery.First(result, StepKind.SpecialFired);
            Assert.That(fired.Cells.Count, Is.EqualTo(25));
        }

        [Test]
        public void ColorBombPlusRocket_Swapped_TransformsMostCommonColor()
        {
            GridBuilder grid = new GridBuilder().Set(2, 2, "*").Set(3, 2, "4H").Set(5, 5, "1");

            TurnResult result = SwapRight(grid, 2, 2);

            var transformed = StepQuery.Wave0(result, StepKind.Transformed);
            Assert.That(transformed.Count, Is.EqualTo(10));
            Assert.That(transformed.All(s => s.Color == 1 && s.Special != Special.None), Is.True);
        }

        [Test]
        public void ColorBombPlusBomb_Swapped_TransformedPiecesAllFire()
        {
            GridBuilder grid = new GridBuilder().Set(2, 2, "*").Set(3, 2, "4B").Set(5, 5, "1");

            TurnResult result = SwapRight(grid, 2, 2);

            int bombsFired = StepQuery.Wave0(result, StepKind.SpecialFired).Count(s => s.Special == Special.Bomb);
            Assert.That(bombsFired, Is.EqualTo(10));
        }

        [Test]
        public void ColorBombPlusColorBomb_Swapped_HitsWholeBoard()
        {
            TurnResult result = SwapRight(new GridBuilder().Set(2, 2, "*").Set(3, 2, "*"), 2, 2);

            Step fired = StepQuery.First(result, StepKind.SpecialFired);
            Assert.That(fired.Cells.Count, Is.EqualTo(36));
        }
    }
}
