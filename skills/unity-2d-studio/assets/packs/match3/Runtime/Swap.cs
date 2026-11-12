namespace __NS__.Puzzle
{
    public readonly struct Swap
    {
        public readonly BoardPos A;
        public readonly BoardPos B;

        public Swap(BoardPos a, BoardPos b)
        {
            A = a;
            B = b;
        }

        public override string ToString() => $"{A}->{B}";
    }
}
