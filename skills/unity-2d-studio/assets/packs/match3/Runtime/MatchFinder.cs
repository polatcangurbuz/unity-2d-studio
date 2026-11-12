using System;
using System.Collections.Generic;

namespace __NS__.Puzzle
{
    public static class MatchFinder
    {
        public static bool HasAny(Board board)
        {
            int w = board.Width, h = board.Height;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int c = ColorAt(board, x, y);
                    if (c < 0) continue;
                    if (x + 2 < w && ColorAt(board, x + 1, y) == c && ColorAt(board, x + 2, y) == c) return true;
                    if (y + 2 < h && ColorAt(board, x, y + 1) == c && ColorAt(board, x, y + 2) == c) return true;
                }
            }
            return false;
        }

        internal static int ColorAt(Board board, int x, int y)
        {
            Piece p = board.PieceAt(new BoardPos(x, y));
            return p.IsEmpty ? -1 : p.Color;
        }

        // first/second: swapped cells, preferred spots for a created special (first wins).
        internal static List<MatchGroup> Find(Board board, BoardPos? first, BoardPos? second)
        {
            int n = board.Width * board.Height;
            var runs = new List<Run>();
            int[] hRun = NewMarks(n);
            int[] vRun = NewMarks(n);
            ScanRuns(board, true, runs, hRun);
            ScanRuns(board, false, runs, vRun);
            var groups = new List<MatchGroup>();
            if (runs.Count == 0) return groups;

            var parent = new int[runs.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            for (int i = 0; i < n; i++)
            {
                if (hRun[i] >= 0 && vRun[i] >= 0) parent[Root(parent, hRun[i])] = Root(parent, vRun[i]);
            }

            int[] groupOfRoot = NewMarks(runs.Count);
            var members = new List<List<int>>();
            for (int r = 0; r < runs.Count; r++)
            {
                int root = Root(parent, r);
                if (groupOfRoot[root] < 0)
                {
                    groupOfRoot[root] = members.Count;
                    members.Add(new List<int>());
                }
                members[groupOfRoot[root]].Add(r);
            }

            var seen = new bool[n];
            foreach (List<int> runIds in members) groups.Add(Build(board, runs, runIds, hRun, vRun, seen, first, second));
            return groups;
        }

        private static MatchGroup Build(Board board, List<Run> runs, List<int> runIds, int[] hRun, int[] vRun, bool[] seen,
            BoardPos? first, BoardPos? second)
        {
            Run longest = runs[runIds[0]];
            var group = new MatchGroup(longest.Color);
            int maxH = 0, maxV = 0;
            BoardPos? cross = null;
            foreach (int id in runIds)
            {
                Run run = runs[id];
                if (run.Horizontal) maxH = Math.Max(maxH, run.Length);
                else maxV = Math.Max(maxV, run.Length);
                if (run.Length > longest.Length) longest = run;
                for (int k = 0; k < run.Length; k++)
                {
                    BoardPos cell = run.Cell(k);
                    int idx = cell.Y * board.Width + cell.X;
                    if (!seen[idx])
                    {
                        seen[idx] = true;
                        group.Cells.Add(cell);
                    }
                    if (cross == null && hRun[idx] >= 0 && vRun[idx] >= 0) cross = cell;
                }
            }

            int max = Math.Max(maxH, maxV);
            Special created = Special.None;
            if (max >= 5) created = Special.ColorBomb;
            else if (maxH >= 3 && maxV >= 3) created = Special.Bomb;
            else if (max == 4) created = longest.Horizontal ? Special.RocketV : Special.RocketH;

            BoardPos pos = created == Special.Bomb && cross.HasValue ? cross.Value : longest.Cell(longest.Length / 2);
            if (second.HasValue && group.Cells.Contains(second.Value)) pos = second.Value;
            if (first.HasValue && group.Cells.Contains(first.Value)) pos = first.Value;
            // A chained piece survives its match, so the special needs a free cell of the group.
            if (created != Special.None && board.ChainHp(pos) > 0)
            {
                Special shape = created;
                created = Special.None;
                foreach (BoardPos c in group.Cells)
                {
                    if (board.ChainHp(c) > 0) continue;
                    pos = c;
                    created = shape;
                    break;
                }
            }
            group.Created = created;
            group.CreatePos = pos;
            return group;
        }

        private static void ScanRuns(Board board, bool horizontal, List<Run> runs, int[] marks)
        {
            int outer = horizontal ? board.Height : board.Width;
            int inner = horizontal ? board.Width : board.Height;
            for (int o = 0; o < outer; o++)
            {
                int i = 0;
                while (i < inner)
                {
                    int c = horizontal ? ColorAt(board, i, o) : ColorAt(board, o, i);
                    if (c < 0)
                    {
                        i++;
                        continue;
                    }
                    int len = 1;
                    while (i + len < inner && (horizontal ? ColorAt(board, i + len, o) : ColorAt(board, o, i + len)) == c) len++;
                    if (len >= 3)
                    {
                        var run = new Run(horizontal ? i : o, horizontal ? o : i, len, horizontal, c);
                        for (int k = 0; k < len; k++)
                        {
                            BoardPos cell = run.Cell(k);
                            marks[cell.Y * board.Width + cell.X] = runs.Count;
                        }
                        runs.Add(run);
                    }
                    i += len;
                }
            }
        }

        private static int Root(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        private static int[] NewMarks(int n)
        {
            var marks = new int[n];
            for (int i = 0; i < n; i++) marks[i] = -1;
            return marks;
        }

        private readonly struct Run
        {
            public readonly int X;
            public readonly int Y;
            public readonly int Length;
            public readonly bool Horizontal;
            public readonly int Color;

            public Run(int x, int y, int length, bool horizontal, int color)
            {
                X = x;
                Y = y;
                Length = length;
                Horizontal = horizontal;
                Color = color;
            }

            public BoardPos Cell(int k) => Horizontal ? new BoardPos(X + k, Y) : new BoardPos(X, Y + k);
        }
    }
}
