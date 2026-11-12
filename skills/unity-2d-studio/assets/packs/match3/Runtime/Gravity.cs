using System.Collections.Generic;

namespace __NS__.Puzzle
{
    internal static class Gravity
    {
        public static void Run(Board board, Rng rng, int colors, StepLog log)
        {
            var column = new List<int>(board.Height);
            do
            {
                for (int x = 0; x < board.Width; x++) SettleColumn(board, x, rng, colors, log, column);
            }
            while (SlideOne(board, log));
        }

        // Holes are skipped, so a column is the list of its non-hole rows; blockers split it into segments.
        private static void SettleColumn(Board board, int x, Rng rng, int colors, StepLog log, List<int> column)
        {
            column.Clear();
            for (int y = 0; y < board.Height; y++)
            {
                if (!board.IsHole(new BoardPos(x, y))) column.Add(y);
            }
            int i = column.Count - 1;
            while (i >= 0)
            {
                if (board.IsBlocking(new BoardPos(x, column[i])))
                {
                    i--;
                    continue;
                }
                int bottom = i;
                while (i >= 0 && !board.IsBlocking(new BoardPos(x, column[i]))) i--;
                int write = Compact(board, x, column, i + 1, bottom, log);
                if (i < 0) Spawn(board, x, column, write, rng, colors, log);
            }
        }

        private static int Compact(Board board, int x, List<int> column, int top, int bottom, StepLog log)
        {
            int write = bottom;
            for (int r = bottom; r >= top; r--)
            {
                var from = new BoardPos(x, column[r]);
                if (!board.HasPiece(from)) continue;
                if (r != write) Move(board, from, new BoardPos(x, column[write]), log);
                write--;
            }
            return write;
        }

        private static void Spawn(Board board, int x, List<int> column, int write, Rng rng, int colors, StepLog log)
        {
            int rowsAbove = 1;
            for (int r = write; r >= 0; r--)
            {
                var at = new BoardPos(x, column[r]);
                Piece piece = board.NewPiece(rng.Next(colors), Special.None);
                board.SetPiece(at, piece);
                log.Add(new Step { Kind = StepKind.Spawned, A = at, PieceId = piece.Id, Color = piece.Color, Value = rowsAbove++ });
            }
        }

        // Only cells no vertical feed can reach stay empty after settling; fill the lowest one from upper-left, else upper-right.
        private static bool SlideOne(Board board, StepLog log)
        {
            for (int y = board.Height - 1; y >= 1; y--)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var to = new BoardPos(x, y);
                    if (!board.CanHoldPiece(to) || board.HasPiece(to)) continue;
                    if (TrySlide(board, new BoardPos(x - 1, y - 1), to, log)) return true;
                    if (TrySlide(board, new BoardPos(x + 1, y - 1), to, log)) return true;
                }
            }
            return false;
        }

        private static bool TrySlide(Board board, BoardPos from, BoardPos to, StepLog log)
        {
            if (!board.InBounds(from) || board.IsHole(from) || !board.CanMove(from)) return false;
            Move(board, from, to, log);
            return true;
        }

        private static void Move(Board board, BoardPos from, BoardPos to, StepLog log)
        {
            Piece piece = board.PieceAt(from);
            board.ClearPiece(from);
            board.SetPiece(to, piece);
            log.Add(new Step { Kind = StepKind.Fell, A = from, B = to, PieceId = piece.Id, Color = piece.Color, Special = piece.Special });
        }
    }
}
