using System.Collections.Generic;

namespace __NS__.Puzzle
{
    internal static class BoardFiller
    {
        private const int Attempts = 100;

        // Preset pieces are kept as authored; only empty cells get random colors.
        public static void Fill(Board board, Rng rng, int colors)
        {
            var open = new List<BoardPos>();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var p = new BoardPos(x, y);
                    if (board.CanHoldPiece(p) && !board.HasPiece(p)) open.Add(p);
                }
            }
            if (open.Count == 0) return;

            var safe = new List<int>(colors);
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                foreach (BoardPos p in open) board.ClearPiece(p);
                foreach (BoardPos p in open) board.SetPiece(p, board.NewPiece(PickColor(board, p, rng, colors, safe), Special.None));
                if (!MatchFinder.HasAny(board) && MoveFinder.HasMove(board)) return;
            }
            Shuffler.TryShuffle(board, rng, new StepLog());
        }

        private static int PickColor(Board board, BoardPos p, Rng rng, int colors, List<int> safe)
        {
            safe.Clear();
            for (int c = 0; c < colors; c++)
            {
                if (!MakesLine(board, p, c)) safe.Add(c);
            }
            return safe.Count > 0 ? safe[rng.Next(safe.Count)] : rng.Next(colors);
        }

        private static bool MakesLine(Board board, BoardPos p, int color) =>
            Reach(board, p, color, -1, 0) + Reach(board, p, color, 1, 0) >= 2
            || Reach(board, p, color, 0, -1) + Reach(board, p, color, 0, 1) >= 2;

        private static int Reach(Board board, BoardPos p, int color, int dx, int dy)
        {
            int n = 0;
            var q = new BoardPos(p.X + dx, p.Y + dy);
            while (board.InBounds(q))
            {
                Piece piece = board.PieceAt(q);
                if (piece.IsEmpty || piece.Color != color) break;
                n++;
                q = new BoardPos(q.X + dx, q.Y + dy);
            }
            return n;
        }
    }
}
