using System.Collections.Generic;
using NUnit.Framework;

namespace __NS__.Puzzle.Tests
{
    public class DeterminismTests
    {
        private static Level Level => LevelParser.Parse(StepQuery.OpenLevel(9, 30, "red=999 blue=999"));

        [Test]
        public void TrySwap_SameSeedSameMoves_IdenticalSteps()
        {
            var first = new PuzzleGame(Level, 77);
            var moves = new List<Swap>();
            var log = new List<string>();
            for (int i = 0; i < 15 && MoveFinder.TryGetBest(first, out Swap move); i++)
            {
                moves.Add(move);
                log.Add(StepQuery.Dump(first.TrySwap(move.A, move.B)));
            }

            var second = new PuzzleGame(Level, 77);
            var replay = new List<string>();
            foreach (Swap move in moves) replay.Add(StepQuery.Dump(second.TrySwap(move.A, move.B)));

            Assert.That(replay, Is.EqualTo(log));
        }

        [Test]
        public void Clone_SameMove_IdenticalSteps()
        {
            var game = new PuzzleGame(Level, 5);
            PuzzleGame copy = game.Clone();
            MoveFinder.TryGetBest(game, out Swap move);

            string a = StepQuery.Dump(game.TrySwap(move.A, move.B));
            string b = StepQuery.Dump(copy.TrySwap(move.A, move.B));

            Assert.That(b, Is.EqualTo(a));
        }

        [Test]
        public void Clone_PlayedOn_LeavesOriginalUntouched()
        {
            var game = new PuzzleGame(Level, 5);
            string before = game.Board.ToString();
            PuzzleGame copy = game.Clone();
            MoveFinder.TryGetBest(copy, out Swap move);

            copy.TrySwap(move.A, move.B);

            Assert.That(game.Board.ToString(), Is.EqualTo(before));
            Assert.That(game.MovesLeft, Is.EqualTo(30));
        }
    }
}
