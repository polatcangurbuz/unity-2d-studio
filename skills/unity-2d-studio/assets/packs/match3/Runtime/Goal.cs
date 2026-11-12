namespace __NS__.Puzzle
{
    public readonly struct Goal
    {
        public static readonly string[] ColorNames = { "red", "green", "blue", "yellow", "purple", "orange" };

        public readonly GoalKind Kind;
        public readonly int Color;
        public readonly int Target;

        public Goal(GoalKind kind, int color, int target)
        {
            Kind = kind;
            Color = color;
            Target = target;
        }

        public string Name => Kind == GoalKind.Color ? ColorNames[Color] : Kind.ToString().ToLowerInvariant();

        public override string ToString() => $"{Name}={Target}";
    }
}
