using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace __NS__.Puzzle.Sim
{
    internal static class Program
    {
        private const string Usage =
            "usage: dotnet run --project Sim~ -- <level.txt> [more levels...] [--runs 500] [--bot greedy|random] [--seed N]";

        private static int Main(string[] args)
        {
            var files = new List<string>();
            int runs = 500;
            string botName = "greedy";
            int? seed = null;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "-h" || a == "--help")
                {
                    Console.WriteLine(Usage);
                    return 0;
                }
                if (a == "--runs" || a == "--bot" || a == "--seed")
                {
                    if (i + 1 >= args.Length) return Fail($"{a} needs a value");
                    string v = args[++i];
                    if (a == "--bot") botName = v.ToLowerInvariant();
                    else if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) return Fail($"{a} needs a number, got '{v}'");
                    else if (a == "--runs") runs = Math.Max(1, n);
                    else seed = n;
                    continue;
                }
                if (a.StartsWith("--", StringComparison.Ordinal)) return Fail($"unknown option {a}");
                files.Add(a);
            }
            if (files.Count == 0) return Fail("no level file given");
            if (botName != "greedy" && botName != "random") return Fail($"unknown bot '{botName}' (greedy|random)");

            foreach (string file in files)
            {
                Level level;
                try
                {
                    level = LevelParser.Parse(File.ReadAllText(file));
                }
                catch (Exception e) when (e is LevelParseException || e is IOException)
                {
                    Console.Error.WriteLine($"{file}: {e.Message}");
                    return 1;
                }
                IBot bot = botName == "random" ? new RandomBot() : (IBot)new GreedyBot();
                var clock = Stopwatch.StartNew();
                SimReport r = Simulator.Run(level, bot, runs, seed ?? level.Seed);
                double seconds = clock.Elapsed.TotalSeconds;
                if (files.Count == 1) PrintFull(file, bot, level, r, seconds);
                else PrintLine(file, r);
            }
            return 0;
        }

        private static void PrintFull(string file, IBot bot, Level level, SimReport r, double seconds)
        {
            Console.WriteLine($"level: {Path.GetFileName(file)} ({level.Width}x{level.Height}, {level.Moves} moves, {bot.Name} bot, {r.Runs} runs)");
            Console.WriteLine(F("pass rate: {0:0.0} %", r.PassRate));
            Console.WriteLine(F("avg moves left on win: {0:0.0}", r.AvgMovesLeftOnWin));
            Console.WriteLine(F("avg goal completion on loss: {0:0.0} %", r.AvgGoalCompletionOnLoss));
            Console.WriteLine(F("moves used p50: {0}", r.MovesUsedP50));
            Console.WriteLine(F("moves used p90: {0}", r.MovesUsedP90));
            Console.WriteLine(F("avg cascades/move: {0:0.00}", r.AvgCascadesPerMove));
            Console.WriteLine(F("specials created/run: {0:0.0}", r.SpecialsPerRun));
            Console.WriteLine(F("shuffles/run: {0:0.00}", r.ShufflesPerRun));
            Console.WriteLine(F("time: {0:0.00} s", seconds));
        }

        private static void PrintLine(string file, SimReport r)
        {
            Console.WriteLine(F("{0,-24} pass {1,5:0.0}%  left-on-win {2,4:0.0}  loss-goal {3,5:0.0}%  p50 {4,2}  p90 {5,2}  casc {6:0.00}  spec {7,4:0.0}  shuf {8:0.00}",
                Path.GetFileName(file), r.PassRate, r.AvgMovesLeftOnWin, r.AvgGoalCompletionOnLoss, r.MovesUsedP50, r.MovesUsedP90,
                r.AvgCascadesPerMove, r.SpecialsPerRun, r.ShufflesPerRun));
        }

        private static string F(string format, params object[] values) => string.Format(CultureInfo.InvariantCulture, format, values);

        private static int Fail(string message)
        {
            Console.Error.WriteLine($"error: {message}");
            Console.Error.WriteLine(Usage);
            return 2;
        }
    }
}
