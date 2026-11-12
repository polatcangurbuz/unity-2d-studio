using NUnit.Framework;

namespace __NS__.Puzzle.Tests
{
    public class SimulatorTests
    {
        private static Level Level => LevelParser.Parse(StepQuery.OpenLevel(7, 12, "red=15", 3));

        [Test]
        public void Run_GreedyBot_PassRateIsPercentage()
        {
            SimReport report = Simulator.Run(Level, new GreedyBot(), 20);

            Assert.That(report.PassRate, Is.InRange(0.0, 100.0));
            Assert.That(report.Runs, Is.EqualTo(20));
        }

        [Test]
        public void Run_SameArguments_SameReport()
        {
            SimReport a = Simulator.Run(Level, new RandomBot(), 20, 9);
            SimReport b = Simulator.Run(Level, new RandomBot(), 20, 9);

            Assert.That(new[] { a.PassRate, a.SpecialsPerRun, a.AvgCascadesPerMove, a.MovesUsedP50 },
                Is.EqualTo(new[] { b.PassRate, b.SpecialsPerRun, b.AvgCascadesPerMove, b.MovesUsedP50 }));
        }

        [Test]
        public void Run_GreedyVersusRandom_GreedyPassesAtLeastAsOften()
        {
            SimReport greedy = Simulator.Run(Level, new GreedyBot(), 40);
            SimReport random = Simulator.Run(Level, new RandomBot(), 40);

            Assert.That(greedy.PassRate, Is.GreaterThanOrEqualTo(random.PassRate));
        }
    }
}
