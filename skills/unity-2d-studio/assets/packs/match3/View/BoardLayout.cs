using UnityEngine;

namespace __NS__.Puzzle.View
{
    // Cell <-> local position. Board centred on its transform; row 0 is the top row, like the core.
    public sealed class BoardLayout
    {
        private readonly Board _board;

        public BoardLayout(Board board, float cellSize)
        {
            _board = board;
            CellSize = cellSize;
        }

        public float CellSize { get; }
        public Vector2 Size => new Vector2(_board.Width * CellSize, _board.Height * CellSize);

        public Vector3 ToLocal(BoardPos p) => ToLocal(p.X, p.Y);

        public Vector3 ToLocal(float x, float y) =>
            new Vector3((x - (_board.Width - 1) * 0.5f) * CellSize, ((_board.Height - 1) * 0.5f - y) * CellSize, 0f);

        public bool TryCell(Vector3 local, out BoardPos cell)
        {
            var x = Mathf.RoundToInt(local.x / CellSize + (_board.Width - 1) * 0.5f);
            var y = Mathf.RoundToInt((_board.Height - 1) * 0.5f - local.y / CellSize);
            cell = new BoardPos(x, y);
            return _board.InBounds(cell) && !_board.IsHole(cell);
        }

        // First real cell of the column: refills enter above it.
        public int TopRow(int x)
        {
            for (var y = 0; y < _board.Height; y++)
                if (!_board.IsHole(new BoardPos(x, y))) return y;
            return 0;
        }
    }
}
