using System.Collections.Generic;

namespace __NS__.Puzzle
{
    public sealed class TurnResult
    {
        internal TurnResult(bool accepted, IReadOnlyList<Step> steps, int cascades, int cleared, int specialsCreated,
            bool shuffled, GameState state, int movesLeft)
        {
            Accepted = accepted;
            Steps = steps;
            Cascades = cascades;
            Cleared = cleared;
            SpecialsCreated = specialsCreated;
            Shuffled = shuffled;
            State = state;
            MovesLeft = movesLeft;
        }

        public bool Accepted { get; }

        public IReadOnlyList<Step> Steps { get; }

        public int Cascades { get; }

        public int Cleared { get; }

        public int SpecialsCreated { get; }

        public bool Shuffled { get; }

        public GameState State { get; }

        public int MovesLeft { get; }
    }
}
