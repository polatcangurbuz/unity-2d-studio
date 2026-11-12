namespace __NS__.Puzzle
{
    public sealed class SimReport
    {
        public int Runs { get; internal set; }

        public int Wins { get; internal set; }

        public double PassRate { get; internal set; }

        public double AvgMovesLeftOnWin { get; internal set; }

        public double AvgGoalCompletionOnLoss { get; internal set; }

        public int MovesUsedP50 { get; internal set; }

        public int MovesUsedP90 { get; internal set; }

        public double AvgCascadesPerMove { get; internal set; }

        public double SpecialsPerRun { get; internal set; }

        public double ShufflesPerRun { get; internal set; }
    }
}
