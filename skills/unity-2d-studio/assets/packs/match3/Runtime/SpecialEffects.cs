using System.Collections.Generic;

namespace __NS__.Puzzle
{
    internal static class SpecialEffects
    {
        public static bool IsRocket(Special s) => s == Special.RocketH || s == Special.RocketV;

        public static void AddRow(Board board, int y, List<BoardPos> cells)
        {
            if (y < 0 || y >= board.Height) return;
            for (int x = 0; x < board.Width; x++) AddCell(board, new BoardPos(x, y), cells);
        }

        public static void AddColumn(Board board, int x, List<BoardPos> cells)
        {
            if (x < 0 || x >= board.Width) return;
            for (int y = 0; y < board.Height; y++) AddCell(board, new BoardPos(x, y), cells);
        }

        public static void AddArea(Board board, BoardPos center, int radius, List<BoardPos> cells)
        {
            for (int y = center.Y - radius; y <= center.Y + radius; y++)
            {
                for (int x = center.X - radius; x <= center.X + radius; x++) AddCell(board, new BoardPos(x, y), cells);
            }
        }

        public static void AddColor(Board board, int color, List<BoardPos> cells)
        {
            if (color < 0) return;
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var p = new BoardPos(x, y);
                    Piece piece = board.PieceAt(p);
                    if (!piece.IsEmpty && piece.Color == color) cells.Add(p);
                }
            }
        }

        // Ties go to the lowest color index so the result never depends on iteration order.
        public static int MostCommonColor(Board board)
        {
            var counts = new int[Goal.ColorNames.Length];
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    Piece piece = board.PieceAt(new BoardPos(x, y));
                    if (!piece.IsEmpty && piece.Color >= 0) counts[piece.Color]++;
                }
            }
            int best = -1;
            for (int c = 0; c < counts.Length; c++)
            {
                if (counts[c] > 0 && (best < 0 || counts[c] > counts[best])) best = c;
            }
            return best;
        }

        public static List<BoardPos> Distinct(Board board, List<BoardPos> cells)
        {
            var seen = new bool[board.Width * board.Height];
            var result = new List<BoardPos>(cells.Count);
            foreach (BoardPos c in cells)
            {
                int i = c.Y * board.Width + c.X;
                if (seen[i]) continue;
                seen[i] = true;
                result.Add(c);
            }
            return result;
        }

        private static void AddCell(Board board, BoardPos p, List<BoardPos> cells)
        {
            if (board.InBounds(p) && !board.IsHole(p)) cells.Add(p);
        }
    }
}
