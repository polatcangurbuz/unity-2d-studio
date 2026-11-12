using System;

namespace __NS__.Puzzle
{
    public readonly struct BoardPos : IEquatable<BoardPos>
    {
        public readonly int X;
        public readonly int Y;

        public BoardPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool IsAdjacent(BoardPos other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y) == 1;

        public bool Equals(BoardPos other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is BoardPos other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ Y;

        public override string ToString() => $"({X},{Y})";

        public static bool operator ==(BoardPos a, BoardPos b) => a.Equals(b);

        public static bool operator !=(BoardPos a, BoardPos b) => !a.Equals(b);
    }
}
