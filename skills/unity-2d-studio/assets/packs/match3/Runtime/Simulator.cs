using System;
using System.Collections.Generic;

namespace __NS__.Puzzle
{
    public static class Simulator
    {
        public static SimReport Run(Level level, IBot bot, int runs) => Run(level, bot, runs, level.Seed);

        // Run i plays seed seedBase + i, so a report is reproducible and every run sees a different board.
        public static SimReport Run(Level level, IBot bot, int runs, int seedBase)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (bot == null) throw new ArgumentNullException(nameof(bot));
            if (runs < 1) throw new ArgumentOutOfRangeException(nameof(runs), "runs must be at least 1");
            var movesUsed = new List<int>(runs);
            int wins = 0;
            long movesLeftOnWin = 0, cascades = 0, totalMoves = 0, specials = 0, shuffles = 0;
            double lossCompletion = 0;
            for (int i = 0; i < runs; i++)
            {
                int seed = unchecked(seedBase + i);
                var game = new PuzzleGame(level, seed);
                bot.Begin(seed);
                while (game.State == GameState.Playing && bot.TryChoose(game, out Swap move))
                {
                    TurnResult result = game.TrySwap(move.A, move.B);
                    if (!result.Accepted) break;
                    cascades += result.Cascades;
                    specials += result.SpecialsCreated;
                    if (result.Shuffled) shuffles++;
                }
                movesUsed.Add(game.MovesUsed);
                totalMoves += game.MovesUsed;
                if (game.State == GameState.Won)
                {
                    wins++;
                    movesLeftOnWin += game.MovesLeft;
                }
                else
                {
                    lossCompletion += game.GoalCompletion;
                }
            }
            movesUsed.Sort();
            int losses = runs - wins;
            return new SimReport
            {
                Runs = runs,
                Wins = wins,
                PassRate = 100.0 * wins / runs,
                AvgMovesLeftOnWin = wins > 0 ? (double)movesLeftOnWin / wins : 0,
                AvgGoalCompletionOnLoss = losses > 0 ? 100.0 * lossCompletion / losses : 0,
                MovesUsedP50 = Percentile(movesUsed, 0.5),
                MovesUsedP90 = Percentile(movesUsed, 0.9),
                AvgCascadesPerMove = totalMoves > 0 ? (double)cascades / totalMoves : 0,
                SpecialsPerRun = (double)specials / runs,
                ShufflesPerRun = (double)shuffles / runs,
            };
        }

        private static int Percentile(List<int> sorted, double q)
        {
            int rank = (int)Math.Ceiling(q * sorted.Count) - 1;
            return sorted[Math.Max(0, Math.Min(sorted.Count - 1, rank))];
        }
    }
}
