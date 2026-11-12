using System;
using System.Collections.Generic;
using System.Globalization;

namespace __NS__.Puzzle
{
    public static class LevelParser
    {
        private static readonly char[] Space = { ' ', '\t' };

        public static Level Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            int moves = 0, colors = 0, seed = 1, goalsLine = 0, gridLine = 0;
            string goalsText = null;
            var rows = new List<string[]>();
            var rowLines = new List<int>();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int comment = line.IndexOf("//", StringComparison.Ordinal);
                if (comment >= 0) line = line.Substring(0, comment);
                line = line.Trim();
                if (line.Length == 0) continue;
                int ln = i + 1;
                if (gridLine > 0)
                {
                    rows.Add(line.Split(Space, StringSplitOptions.RemoveEmptyEntries));
                    rowLines.Add(ln);
                    continue;
                }
                int colon = line.IndexOf(':');
                if (colon < 0) throw new LevelParseException(ln, $"expected 'key: value' but got '{line}'");
                string key = line.Substring(0, colon).Trim().ToLowerInvariant();
                string value = line.Substring(colon + 1).Trim();
                switch (key)
                {
                    case "moves": moves = ParseInt(value, ln, key, 1, 999); break;
                    case "colors": colors = ParseInt(value, ln, key, 3, Goal.ColorNames.Length); break;
                    case "seed": seed = ParseInt(value, ln, key, int.MinValue, int.MaxValue); break;
                    case "goals": goalsText = value; goalsLine = ln; break;
                    case "grid": gridLine = ln; break;
                    default: throw new LevelParseException(ln, $"unknown key '{key}' (expected moves, colors, seed, goals, grid)");
                }
            }
            if (moves == 0) throw new LevelParseException(0, "missing 'moves:' line");
            if (colors == 0) throw new LevelParseException(0, "missing 'colors:' line");
            if (goalsText == null) throw new LevelParseException(0, "missing 'goals:' line");
            if (gridLine == 0) throw new LevelParseException(0, "missing 'grid:' line");
            if (rows.Count == 0) throw new LevelParseException(gridLine, "grid has no rows");
            List<Goal> goals = ParseGoals(goalsText, goalsLine, colors);
            Board layout = ParseGrid(rows, rowLines, colors);
            return new Level(moves, colors, seed, goals, layout);
        }

        private static int ParseInt(string value, int ln, string key, int min, int max)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v >= min && v <= max) return v;
            throw new LevelParseException(ln, $"'{key}' needs a whole number in {min}..{max}, got '{value}'");
        }

        private static List<Goal> ParseGoals(string text, int ln, int colors)
        {
            var goals = new List<Goal>();
            foreach (string part in text.Split(Space, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) throw new LevelParseException(ln, $"goal '{part}' must look like name=count");
                string name = part.Substring(0, eq).ToLowerInvariant();
                int target = ParseInt(part.Substring(eq + 1), ln, name, 1, 9999);
                int color = Array.IndexOf(Goal.ColorNames, name);
                if (color >= colors) throw new LevelParseException(ln, $"goal '{name}' needs colors: {color + 1} or more");
                if (color >= 0) goals.Add(new Goal(GoalKind.Color, color, target));
                else if (name == "crate") goals.Add(new Goal(GoalKind.Crate, -1, target));
                else if (name == "ice") goals.Add(new Goal(GoalKind.Ice, -1, target));
                else if (name == "chain") goals.Add(new Goal(GoalKind.Chain, -1, target));
                else throw new LevelParseException(ln, $"unknown goal '{name}' (use {string.Join(", ", Goal.ColorNames)}, crate, ice, chain)");
            }
            if (goals.Count == 0) throw new LevelParseException(ln, "goals needs at least one name=count");
            return goals;
        }

        private static Board ParseGrid(List<string[]> rows, List<int> rowLines, int colors)
        {
            int width = rows[0].Length;
            var board = new Board(width, rows.Count);
            for (int y = 0; y < rows.Count; y++)
            {
                if (rows[y].Length != width)
                    throw new LevelParseException(rowLines[y], $"row has {rows[y].Length} cells, expected {width} like the first row");
                for (int x = 0; x < width; x++) ParseCell(board, new BoardPos(x, y), rows[y][x], rowLines[y], colors);
            }
            return board;
        }

        private static void ParseCell(Board board, BoardPos p, string token, int ln, int colors)
        {
            string cell = token;
            string piece = null;
            int colon = token.IndexOf(':');
            if (colon >= 0)
            {
                cell = token.Substring(0, colon);
                piece = token.Substring(colon + 1);
            }
            else if (char.IsDigit(token[0]) || token[0] == '*')
            {
                cell = ".";
                piece = token;
            }
            if (cell.Length == 0) throw BadToken(ln, token);
            string hp = cell.Substring(1);
            switch (cell[0])
            {
                case '.':
                    if (hp.Length > 0) throw BadToken(ln, token);
                    break;
                case '#':
                    if (hp.Length > 0 || piece != null) throw BadToken(ln, token);
                    board.SetHole(p, true);
                    return;
                case 'C':
                    if (piece != null) throw new LevelParseException(ln, $"crate '{token}' cannot hold a piece");
                    board.SetCrate(p, ParseHp(hp, ln, token, false));
                    return;
                case 'I':
                    board.SetIce(p, ParseHp(hp, ln, token, false));
                    break;
                case 'L':
                    board.SetChain(p, ParseHp(hp, ln, token, true));
                    break;
                default:
                    throw BadToken(ln, token);
            }
            if (piece != null) board.SetPiece(p, ParsePiece(board, piece, ln, token, colors));
        }

        private static int ParseHp(string text, int ln, string token, bool optional)
        {
            if (text.Length == 0 && optional) return 1;
            if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int hp) && hp >= 1 && hp <= 9) return hp;
            throw new LevelParseException(ln, $"cell '{token}' needs hp 1..9, e.g. C2");
        }

        private static Piece ParsePiece(Board board, string text, int ln, string token, int colors)
        {
            if (text == "*") return board.NewPiece(-1, Special.ColorBomb);
            int end = 0;
            while (end < text.Length && char.IsDigit(text[end])) end++;
            if (end == 0 || end > 1) throw BadToken(ln, token);
            int color = text[0] - '0';
            if (color >= colors) throw new LevelParseException(ln, $"piece color {color} in '{token}' is outside 0..{colors - 1}");
            Special special;
            switch (text.Substring(end))
            {
                case "": special = Special.None; break;
                case "H": special = Special.RocketH; break;
                case "V": special = Special.RocketV; break;
                case "B": special = Special.Bomb; break;
                default: throw BadToken(ln, token);
            }
            return board.NewPiece(color, special);
        }

        private static LevelParseException BadToken(int ln, string token) =>
            new LevelParseException(ln, $"unknown cell '{token}' (use . # C<n> I<n> L[n], optional :<color>[H|V|B] or :*)");
    }
}
