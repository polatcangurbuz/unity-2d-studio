using System;
using System.Text;

namespace __NS__.Puzzle
{
    public sealed class Board
    {
        private readonly bool[] _holes;
        private readonly int[] _crates;
        private readonly int[] _ice;
        private readonly int[] _chains;
        private readonly Piece[] _pieces;
        private int _nextId = 1;

        public Board(int width, int height)
        {
            if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width), "board needs at least 1x1 cells");
            Width = width;
            Height = height;
            int n = width * height;
            _holes = new bool[n];
            _crates = new int[n];
            _ice = new int[n];
            _chains = new int[n];
            _pieces = new Piece[n];
        }

        private Board(Board source)
        {
            Width = source.Width;
            Height = source.Height;
            _holes = (bool[])source._holes.Clone();
            _crates = (int[])source._crates.Clone();
            _ice = (int[])source._ice.Clone();
            _chains = (int[])source._chains.Clone();
            _pieces = (Piece[])source._pieces.Clone();
            _nextId = source._nextId;
        }

        public int Width { get; }

        public int Height { get; }

        public Board Clone() => new Board(this);

        public bool InBounds(BoardPos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        public bool IsHole(BoardPos p) => _holes[Index(p)];

        public int CrateHp(BoardPos p) => _crates[Index(p)];

        public int IceHp(BoardPos p) => _ice[Index(p)];

        public int ChainHp(BoardPos p) => _chains[Index(p)];

        public Piece PieceAt(BoardPos p) => _pieces[Index(p)];

        public bool HasPiece(BoardPos p) => !_pieces[Index(p)].IsEmpty;

        public bool CanMove(BoardPos p)
        {
            int i = Index(p);
            return !_pieces[i].IsEmpty && _chains[i] == 0;
        }

        // Crates and chained pieces stop gravity; holes do not.
        public bool IsBlocking(BoardPos p)
        {
            int i = Index(p);
            return _crates[i] > 0 || (!_pieces[i].IsEmpty && _chains[i] > 0);
        }

        public bool CanHoldPiece(BoardPos p)
        {
            int i = Index(p);
            return !_holes[i] && _crates[i] == 0;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    char c = _holes[i] ? '#' : _crates[i] > 0 ? 'C' : _pieces[i].IsEmpty ? '.'
                        : _pieces[i].Special == Special.ColorBomb ? '*' : (char)('0' + _pieces[i].Color);
                    sb.Append(c);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        internal void SetHole(BoardPos p, bool hole) => _holes[Index(p)] = hole;

        internal void SetCrate(BoardPos p, int hp) => _crates[Index(p)] = hp;

        internal void SetIce(BoardPos p, int hp) => _ice[Index(p)] = hp;

        internal void SetChain(BoardPos p, int hp) => _chains[Index(p)] = hp;

        internal void SetPiece(BoardPos p, Piece piece) => _pieces[Index(p)] = piece;

        internal void ClearPiece(BoardPos p) => _pieces[Index(p)] = Piece.None;

        internal Piece NewPiece(int color, Special special) => new Piece(_nextId++, color, special);

        internal void SwapPieces(BoardPos a, BoardPos b)
        {
            int ia = Index(a);
            int ib = Index(b);
            Piece t = _pieces[ia];
            _pieces[ia] = _pieces[ib];
            _pieces[ib] = t;
        }

        private int Index(BoardPos p) => p.Y * Width + p.X;
    }
}
