using System;

namespace __NS__.Puzzle
{
    public sealed class PuzzleGame
    {
        private readonly int[] _progress;

        public PuzzleGame(Level level)
            : this(level, level.Seed)
        {
        }

        public PuzzleGame(Level level, int seed)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Board = level.Layout.Clone();
            Rng = new Rng(seed);
            MovesLeft = level.Moves;
            _progress = new int[level.Goals.Count];
            BoardFiller.Fill(Board, Rng, level.Colors);
        }

        private PuzzleGame(PuzzleGame source)
        {
            Level = source.Level;
            Board = source.Board.Clone();
            Rng = source.Rng.Clone();
            MovesLeft = source.MovesLeft;
            State = source.State;
            _progress = (int[])source._progress.Clone();
        }

        public Level Level { get; }

        public Board Board { get; }

        public int MovesLeft { get; internal set; }

        public int MovesUsed => Level.Moves - MovesLeft;

        public GameState State { get; internal set; }

        public bool AllGoalsMet
        {
            get
            {
                for (int i = 0; i < _progress.Length; i++)
                {
                    if (_progress[i] < Level.Goals[i].Target) return false;
                }
                return true;
            }
        }

        // Average fraction of each goal reached, 0..1.
        public float GoalCompletion
        {
            get
            {
                if (_progress.Length == 0) return 1f;
                float sum = 0f;
                for (int i = 0; i < _progress.Length; i++) sum += Math.Min(1f, _progress[i] / (float)Level.Goals[i].Target);
                return sum / _progress.Length;
            }
        }

        internal Rng Rng { get; }

        public PuzzleGame Clone() => new PuzzleGame(this);

        public int GoalProgress(int goalIndex) => _progress[goalIndex];

        public int Remaining(GoalKind kind, int color)
        {
            int left = 0;
            for (int i = 0; i < _progress.Length; i++)
            {
                Goal g = Level.Goals[i];
                if (g.Kind == kind && (kind != GoalKind.Color || g.Color == color)) left += Math.Max(0, g.Target - _progress[i]);
            }
            return left;
        }

        public TurnResult TrySwap(BoardPos a, BoardPos b) => new TurnResolver(this).Swap(a, b);

        public TurnResult UseBooster(BoosterKind kind, BoardPos at) => new TurnResolver(this).Booster(kind, at);

        internal bool TryAddProgress(GoalKind kind, int color, out int index, out int value)
        {
            for (int i = 0; i < _progress.Length; i++)
            {
                Goal g = Level.Goals[i];
                if (g.Kind != kind || (kind == GoalKind.Color && g.Color != color) || _progress[i] >= g.Target) continue;
                _progress[i]++;
                index = i;
                value = _progress[i];
                return true;
            }
            index = -1;
            value = 0;
            return false;
        }
    }
}
