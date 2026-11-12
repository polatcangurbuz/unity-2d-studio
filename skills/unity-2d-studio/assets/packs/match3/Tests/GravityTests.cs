using System.Linq;
using NUnit.Framework;

namespace __NS__.Puzzle.Tests
{
    public class GravityTests
    {
        private static bool FellFromTo(TurnResult result, int pieceId, BoardPos from, BoardPos to) =>
            StepQuery.Wave0(result, StepKind.Fell).Any(s => s.PieceId == pieceId && s.A == from && s.B == to);

        [Test]
        public void Gravity_CellBelowCleared_PieceFallsStraight()
        {
            PuzzleGame game = new GridBuilder().Build();
            int id = game.Board.PieceAt(StepQuery.P(2, 4)).Id;

            TurnResult result = game.UseBooster(BoosterKind.Hammer, StepQuery.P(2, 5));

            Assert.That(FellFromTo(result, id, StepQuery.P(2, 4), StepQuery.P(2, 5)), Is.True, StepQuery.Dump(result));
        }

        [Test]
        public void Gravity_HoleBelow_PieceFallsAcrossHole()
        {
            PuzzleGame game = new GridBuilder().Set(2, 4, "#").Build();
            int id = game.Board.PieceAt(StepQuery.P(2, 3)).Id;

            TurnResult result = game.UseBooster(BoosterKind.Hammer, StepQuery.P(2, 5));

            Assert.That(FellFromTo(result, id, StepQuery.P(2, 3), StepQuery.P(2, 5)), Is.True, StepQuery.Dump(result));
        }

        [Test]
        public void Gravity_CellUnderCrate_FilledDiagonallyFromUpperLeft()
        {
            PuzzleGame game = new GridBuilder().Set(2, 4, "C9").Build();
            int id = game.Board.PieceAt(StepQuery.P(1, 4)).Id;

            TurnResult result = game.UseBooster(BoosterKind.Hammer, StepQuery.P(2, 5));

            Assert.That(FellFromTo(result, id, StepQuery.P(1, 4), StepQuery.P(2, 5)), Is.True, StepQuery.Dump(result));
        }

        [Test]
        public void Gravity_ChainedPieceAboveGap_DoesNotFall()
        {
            PuzzleGame game = new GridBuilder().Set(2, 4, "L:2").Build();
            int id = game.Board.PieceAt(StepQuery.P(2, 4)).Id;

            TurnResult result = game.UseBooster(BoosterKind.Hammer, StepQuery.P(2, 5));

            Assert.That(StepQuery.Wave0(result, StepKind.Fell).Any(s => s.PieceId == id), Is.False);
        }

        [Test]
        public void Gravity_TopCellCleared_SpawnsOneRowAbove()
        {
            PuzzleGame game = new GridBuilder().Build();

            TurnResult result = game.UseBooster(BoosterKind.Hammer, StepQuery.P(2, 0));

            Step spawned = StepQuery.Wave0(result, StepKind.Spawned).Single();
            Assert.That(spawned.A, Is.EqualTo(StepQuery.P(2, 0)));
            Assert.That(spawned.Value, Is.EqualTo(1));
        }

        [Test]
        public void Gravity_AfterTurn_EveryOpenCellHasPiece()
        {
            PuzzleGame game = new GridBuilder().Set(2, 4, "C9").Set(3, 1, "#").Build();

            game.UseBooster(BoosterKind.Row, StepQuery.P(0, 5));

            for (int y = 0; y < 6; y++)
            {
                for (int x = 0; x < 6; x++)
                {
                    BoardPos p = StepQuery.P(x, y);
                    if (game.Board.CanHoldPiece(p)) Assert.That(game.Board.HasPiece(p), Is.True, p.ToString());
                }
            }
        }
    }
}
