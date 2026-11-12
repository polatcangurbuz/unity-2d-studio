using System.Collections.Generic;

namespace __NS__.Puzzle
{
    public static class MoveFinder
    {
        public static List<Swap> FindAll(Board board)
        {
            var moves = new List<Swap>();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var p = new BoardPos(x, y);
                    var right = new BoardPos(x + 1, y);
                    var down = new BoardPos(x, y + 1);
                    if (IsValid(board, p, right)) moves.Add(new Swap(p, right));
                    if (IsValid(board, p, down)) moves.Add(new Swap(p, down));
                }
            }
            return moves;
        }

        public static bool HasMove(Board board)
        {
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var p = new BoardPos(x, y);
                    if (IsValid(board, p, new BoardPos(x + 1, y)) || IsValid(board, p, new BoardPos(x, y + 1))) return true;
                }
            }
            return false;
        }

        // Valid = both pieces free and adjacent, and either one is a special or the swap makes a line of 3+.
        public static bool IsValid(Board board, BoardPos a, BoardPos b)
        {
            if (!board.InBounds(a) || !board.InBounds(b) || !a.IsAdjacent(b) || !board.CanMove(a) || !board.CanMove(b)) return false;
            Piece pa = board.PieceAt(a);
            Piece pb = board.PieceAt(b);
            if (pa.Special != Special.None || pb.Special != Special.None) return true;
            if (pa.Color == pb.Color) return false;
            board.SwapPieces(a, b);
            bool ok = LongestLine(board, a) >= 3 || LongestLine(board, b) >= 3;
            board.SwapPieces(a, b);
            return ok;
        }

        public static bool TryGetBest(PuzzleGame game, out Swap best)
        {
            best = default;
            int bestScore = int.MinValue;
            foreach (Swap move in FindAll(game.Board))
            {
                int score = Score(game, move);
                if (score <= bestScore) continue;
                bestScore = score;
                best = move;
            }
            return bestScore > int.MinValue;
        }

        // One-ply heuristic: specials and goal-relevant cells score high; lower moves get a small bonus for cascades.
        public static int Score(PuzzleGame game, Swap move)
        {
            Board board = game.Board;
            Piece pa = board.PieceAt(move.A);
            Piece pb = board.PieceAt(move.B);
            int depth = move.A.Y > move.B.Y ? move.A.Y : move.B.Y;
            if (pa.Special != Special.None || pb.Special != Special.None) return SpecialScore(game, pa, pb) + depth;
            board.SwapPieces(move.A, move.B);
            int score = LineScore(game, move.A) + LineScore(game, move.B);
            board.SwapPieces(move.A, move.B);
            return score + depth;
        }

        private static int SpecialScore(PuzzleGame game, Piece pa, Piece pb)
        {
            if (pa.Special != Special.None && pb.Special != Special.None) return 400;
            Piece special = pa.Special != Special.None ? pa : pb;
            Piece plain = pa.Special != Special.None ? pb : pa;
            if (special.Special != Special.ColorBomb) return 150;
            return game.Remaining(GoalKind.Color, plain.Color) > 0 ? 300 : 200;
        }

        private static int LineScore(PuzzleGame game, BoardPos p)
        {
            Board board = game.Board;
            int color = board.PieceAt(p).Color;
            int left = Reach(board, p, color, -1, 0), right = Reach(board, p, color, 1, 0);
            int up = Reach(board, p, color, 0, -1), down = Reach(board, p, color, 0, 1);
            int h = left + right + 1, v = up + down + 1;
            bool hLine = h >= 3, vLine = v >= 3;
            if (!hLine && !vLine) return 0;
            int score = 0;
            if (h >= 5 || v >= 5) score += 150;
            else if (hLine && vLine) score += 100;
            else if (h == 4 || v == 4) score += 70;
            if (hLine)
            {
                for (int x = p.X - left; x <= p.X + right; x++) score += CellScore(game, new BoardPos(x, p.Y), color);
            }
            if (vLine)
            {
                for (int y = p.Y - up; y <= p.Y + down; y++)
                {
                    if (!hLine || y != p.Y) score += CellScore(game, new BoardPos(p.X, y), color);
                }
            }
            return score;
        }

        private static int CellScore(PuzzleGame game, BoardPos p, int color)
        {
            Board board = game.Board;
            int score = 10;
            if (game.Remaining(GoalKind.Color, color) > 0) score += 30;
            if (board.IceHp(p) > 0 && game.Remaining(GoalKind.Ice, -1) > 0) score += 30;
            if (board.ChainHp(p) > 0 && game.Remaining(GoalKind.Chain, -1) > 0) score += 30;
            if (game.Remaining(GoalKind.Crate, -1) <= 0) return score;
            score += CrateScore(board, new BoardPos(p.X - 1, p.Y)) + CrateScore(board, new BoardPos(p.X + 1, p.Y));
            score += CrateScore(board, new BoardPos(p.X, p.Y - 1)) + CrateScore(board, new BoardPos(p.X, p.Y + 1));
            return score;
        }

        private static int CrateScore(Board board, BoardPos p) => board.InBounds(p) && board.CrateHp(p) > 0 ? 30 : 0;

        private static int LongestLine(Board board, BoardPos p)
        {
            Piece piece = board.PieceAt(p);
            if (piece.IsEmpty || piece.Color < 0) return 0;
            int h = 1 + Reach(board, p, piece.Color, -1, 0) + Reach(board, p, piece.Color, 1, 0);
            int v = 1 + Reach(board, p, piece.Color, 0, -1) + Reach(board, p, piece.Color, 0, 1);
            return h > v ? h : v;
        }

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
