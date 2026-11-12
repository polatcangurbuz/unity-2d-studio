using System.Collections.Generic;

namespace __NS__.Puzzle
{
    internal static class Shuffler
    {
        private const int Attempts = 200;

        public static bool TryShuffle(Board board, Rng rng, StepLog log)
        {
            var cells = new List<BoardPos>();
            var original = new List<Piece>();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var p = new BoardPos(x, y);
                    if (!board.CanMove(p)) continue;
                    cells.Add(p);
                    original.Add(board.PieceAt(p));
                }
            }
            if (cells.Count < 2) return false;

            Piece[] order = original.ToArray();
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                for (int i = order.Length - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    Piece t = order[i];
                    order[i] = order[j];
                    order[j] = t;
                }
                for (int i = 0; i < cells.Count; i++) board.SetPiece(cells[i], order[i]);
                if (MatchFinder.HasAny(board) || !MoveFinder.HasMove(board)) continue;
                Report(cells, original, order, log);
                return true;
            }
            for (int i = 0; i < cells.Count; i++) board.SetPiece(cells[i], original[i]);
            return false;
        }

        private static void Report(List<BoardPos> cells, List<Piece> original, Piece[] order, StepLog log)
        {
            var from = new Dictionary<int, BoardPos>(cells.Count);
            for (int i = 0; i < cells.Count; i++) from[original[i].Id] = cells[i];
            for (int i = 0; i < cells.Count; i++)
            {
                BoardPos start = from[order[i].Id];
                if (start == cells[i]) continue;
                log.Add(new Step { Kind = StepKind.Shuffled, A = start, B = cells[i], PieceId = order[i].Id, Color = order[i].Color });
            }
        }
    }
}
