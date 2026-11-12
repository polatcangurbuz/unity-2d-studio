using System.Linq;
using NUnit.Framework;

namespace __NS__.Puzzle.Tests
{
    public class SpecialEffectTests
    {
        private static Step HammerFirstFired(GridBuilder grid, int x, int y) =>
            StepQuery.First(grid.Build().UseBooster(BoosterKind.Hammer, StepQuery.P(x, y)), StepKind.SpecialFired);

        [Test]
        public void RocketH_Fired_HitsWholeRow()
        {
            Step fired = HammerFirstFired(new GridBuilder().Set(2, 2, "3H"), 2, 2);

            var expected = StepQuery.Row(2, 6).Where(p => p != StepQuery.P(2, 2));
            Assert.That(fired.Cells, Is.EquivalentTo(expected));
        }

        [Test]
        public void RocketV_Fired_HitsWholeColumn()
        {
            Step fired = HammerFirstFired(new GridBuilder().Set(2, 2, "3V"), 2, 2);

            var expected = StepQuery.Column(2, 6).Where(p => p != StepQuery.P(2, 2));
            Assert.That(fired.Cells, Is.EquivalentTo(expected));
        }

        [Test]
        public void Bomb_Fired_HitsThreeByThree()
        {
            Step fired = HammerFirstFired(new GridBuilder().Set(2, 2, "3B"), 2, 2);

            Assert.That(fired.Cells.Count, Is.EqualTo(8));
            Assert.That(fired.Cells, Has.Member(StepQuery.P(1, 1)));
            Assert.That(fired.Cells, Has.Member(StepQuery.P(3, 3)));
        }

        [Test]
        public void ColorBomb_SwappedWithPiece_ClearsEveryPieceOfThatColor()
        {
            PuzzleGame game = new GridBuilder().Set(2, 2, "*").Build();

            TurnResult result = game.TrySwap(StepQuery.P(2, 2), StepQuery.P(3, 2));

            Step fired = StepQuery.First(result, StepKind.SpecialFired);
            Assert.That(fired.Color, Is.EqualTo(4));
            Assert.That(fired.Cells.Count, Is.EqualTo(9));
        }

        [Test]
        public void Rocket_SwappedWithPlainPiece_FiresWithoutMatch()
        {
            PuzzleGame game = new GridBuilder().Set(2, 2, "3H").Build();

            TurnResult result = game.TrySwap(StepQuery.P(2, 2), StepQuery.P(2, 3));

            Step fired = StepQuery.First(result, StepKind.SpecialFired);
            Assert.That(result.Accepted, Is.True);
            Assert.That(fired.A, Is.EqualTo(StepQuery.P(2, 3)));
        }

        [Test]
        public void Rocket_SwappedWithPlainPiece_SpendsMove()
        {
            PuzzleGame game = new GridBuilder().Set(2, 2, "3H").Build(moves: 10);

            game.TrySwap(StepQuery.P(2, 2), StepQuery.P(2, 3));

            Assert.That(game.MovesLeft, Is.EqualTo(9));
        }

        [Test]
        public void ChainReaction_RocketHitsRocket_EachFiresOnce()
        {
            GridBuilder grid = new GridBuilder().Set(1, 2, "3H").Set(4, 2, "1H");

            TurnResult result = grid.Build().UseBooster(BoosterKind.Hammer, StepQuery.P(1, 2));

            var origins = StepQuery.Wave0(result, StepKind.SpecialFired).Select(s => s.A).ToList();
            Assert.That(origins, Is.EqualTo(new[] { StepQuery.P(1, 2), StepQuery.P(4, 2) }));
        }

        [Test]
        public void ChainReaction_BombInRocketRow_Fires()
        {
            GridBuilder grid = new GridBuilder().Set(0, 3, "3V").Set(0, 0, "2B");

            TurnResult result = grid.Build().UseBooster(BoosterKind.Hammer, StepQuery.P(0, 3));

            var kinds = StepQuery.Wave0(result, StepKind.SpecialFired).Select(s => s.Special).ToList();
            Assert.That(kinds, Is.EqualTo(new[] { Special.RocketV, Special.Bomb }));
        }
    }
}
