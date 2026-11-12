using System.Collections.Generic;

namespace __NS__.Puzzle
{
    public sealed class Level
    {
        internal Level(int moves, int colors, int seed, IReadOnlyList<Goal> goals, Board layout)
        {
            Moves = moves;
            Colors = colors;
            Seed = seed;
            Goals = goals;
            Layout = layout;
        }

        public int Moves { get; }

        public int Colors { get; }

        public int Seed { get; }

        public IReadOnlyList<Goal> Goals { get; }

        // Layers plus any preset pieces; games clone it, never mutate it.
        public Board Layout { get; }

        public int Width => Layout.Width;

        public int Height => Layout.Height;
    }
}
