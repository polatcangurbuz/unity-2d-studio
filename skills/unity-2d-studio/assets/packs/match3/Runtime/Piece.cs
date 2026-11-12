namespace __NS__.Puzzle
{
    public readonly struct Piece
    {
        public static readonly Piece None = new Piece(0, -1, Special.None);

        public readonly int Id;
        public readonly int Color;
        public readonly Special Special;

        internal Piece(int id, int color, Special special)
        {
            Id = id;
            Color = color;
            Special = special;
        }

        public bool IsEmpty => Id == 0;

        public override string ToString() => IsEmpty ? "-" : $"#{Id} c{Color} {Special}";
    }
}
