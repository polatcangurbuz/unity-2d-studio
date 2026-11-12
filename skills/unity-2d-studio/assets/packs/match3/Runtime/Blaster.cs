using System.Collections.Generic;

namespace __NS__.Puzzle
{
    internal sealed class Blaster
    {
        private readonly PuzzleGame _game;
        private readonly Board _board;
        private readonly StepLog _log;
        private readonly Queue<(BoardPos Pos, Piece Piece)> _fires = new Queue<(BoardPos, Piece)>();

        public Blaster(PuzzleGame game, StepLog log)
        {
            _game = game;
            _board = game.Board;
            _log = log;
        }

        public int Cleared { get; private set; }

        public int SpecialsCreated { get; private set; }

        public void Hit(BoardPos p, ClearCause cause)
        {
            if (!_board.InBounds(p) || _board.IsHole(p)) return;
            int crate = _board.CrateHp(p);
            if (crate > 0)
            {
                _board.SetCrate(p, crate - 1);
                HitLayer(p, BlockerLayer.Crate, crate - 1);
                return;
            }
            Piece piece = _board.PieceAt(p);
            if (piece.IsEmpty) return;
            int chain = _board.ChainHp(p);
            if (chain > 0)
            {
                _board.SetChain(p, chain - 1);
                HitLayer(p, BlockerLayer.Chain, chain - 1);
                return;
            }
            Remove(p, piece, cause);
            if (piece.Special != Special.None) _fires.Enqueue((p, piece));
        }

        // Specials removed from the board fire here, so each one fires exactly once.
        public void FireAll()
        {
            while (_fires.Count > 0)
            {
                (BoardPos pos, Piece piece) = _fires.Dequeue();
                Fire(pos, piece);
            }
        }

        public void ClearGroups(List<MatchGroup> groups)
        {
            var created = new List<(BoardPos Pos, Piece Piece)>();
            var crates = new List<BoardPos>();
            foreach (MatchGroup g in groups)
            {
                Piece special = g.Created == Special.None ? Piece.None
                    : _board.NewPiece(g.Created == Special.ColorBomb ? -1 : g.Color, g.Created);
                _log.Add(new Step
                {
                    Kind = StepKind.Matched, A = g.CreatePos, Color = g.Color, Special = g.Created, PieceId = special.Id, Cells = g.Cells,
                });
                crates.Clear();
                foreach (BoardPos c in g.Cells)
                {
                    Hit(c, ClearCause.Match);
                    CollectCrates(c, crates);
                }
                foreach (BoardPos c in crates) Hit(c, ClearCause.Match);
                if (!special.IsEmpty) created.Add((g.CreatePos, special));
            }
            FireAll();
            // New specials appear after the wave's blasts so they cannot be caught by them.
            foreach ((BoardPos pos, Piece piece) in created)
            {
                if (_board.HasPiece(pos)) continue;
                _board.SetPiece(pos, piece);
                SpecialsCreated++;
            }
        }

        // The dragged piece now sits at `to`; a combo or special effect is centred there.
        public void FireSwapped(BoardPos from, BoardPos to)
        {
            Piece moved = _board.PieceAt(to);
            Piece other = _board.PieceAt(from);
            if (moved.Special != Special.None && other.Special != Special.None)
            {
                Combo(to, moved, from, other);
            }
            else
            {
                bool movedIsSpecial = moved.Special != Special.None;
                BoardPos at = movedIsSpecial ? to : from;
                Piece special = movedIsSpecial ? moved : other;
                Piece plain = movedIsSpecial ? other : moved;
                if (special.Special == Special.ColorBomb)
                {
                    Remove(at, special, ClearCause.Special);
                    var cells = new List<BoardPos>();
                    SpecialEffects.AddColor(_board, plain.Color, cells);
                    Blast(at, Special.ColorBomb, Special.None, plain.Color, cells);
                }
                else
                {
                    Hit(at, ClearCause.Special);
                }
            }
            FireAll();
        }

        private void Fire(BoardPos origin, Piece piece)
        {
            var cells = new List<BoardPos>();
            int color = -1;
            switch (piece.Special)
            {
                case Special.RocketH: SpecialEffects.AddRow(_board, origin.Y, cells); break;
                case Special.RocketV: SpecialEffects.AddColumn(_board, origin.X, cells); break;
                case Special.Bomb: SpecialEffects.AddArea(_board, origin, 1, cells); break;
                default:
                    color = SpecialEffects.MostCommonColor(_board);
                    SpecialEffects.AddColor(_board, color, cells);
                    break;
            }
            cells.Remove(origin);
            Blast(origin, piece.Special, Special.None, color, cells);
        }

        private void Combo(BoardPos at, Piece moved, BoardPos otherPos, Piece other)
        {
            Remove(at, moved, ClearCause.Special);
            Remove(otherPos, other, ClearCause.Special);
            Special a = moved.Special;
            Special b = other.Special;
            var cells = new List<BoardPos>();
            if (a == Special.ColorBomb && b == Special.ColorBomb)
            {
                SpecialEffects.AddArea(_board, at, _board.Width + _board.Height, cells);
                Blast(at, a, b, -1, cells);
                return;
            }
            if (a == Special.ColorBomb || b == Special.ColorBomb)
            {
                Transform(at, a == Special.ColorBomb ? b : a);
                return;
            }
            if (SpecialEffects.IsRocket(a) && SpecialEffects.IsRocket(b))
            {
                SpecialEffects.AddRow(_board, at.Y, cells);
                SpecialEffects.AddColumn(_board, at.X, cells);
            }
            else if (SpecialEffects.IsRocket(a) || SpecialEffects.IsRocket(b))
            {
                for (int d = -1; d <= 1; d++)
                {
                    SpecialEffects.AddRow(_board, at.Y + d, cells);
                    SpecialEffects.AddColumn(_board, at.X + d, cells);
                }
            }
            else
            {
                SpecialEffects.AddArea(_board, at, 2, cells);
            }
            Blast(at, a, b, -1, SpecialEffects.Distinct(_board, cells));
        }

        // Colorbomb + rocket/bomb: every free piece of the most common color becomes that special, then all fire.
        private void Transform(BoardPos at, Special kind)
        {
            int color = SpecialEffects.MostCommonColor(_board);
            var cells = new List<BoardPos>();
            SpecialEffects.AddColor(_board, color, cells);
            _log.Add(new Step { Kind = StepKind.SpecialFired, A = at, Special = Special.ColorBomb, Partner = kind, Color = color, Cells = cells });
            foreach (BoardPos c in cells)
            {
                Piece piece = _board.PieceAt(c);
                if (piece.Special != Special.None || _board.ChainHp(c) > 0) continue;
                Special s = SpecialEffects.IsRocket(kind) ? ((c.X + c.Y) % 2 == 0 ? Special.RocketH : Special.RocketV) : kind;
                _board.SetPiece(c, new Piece(piece.Id, piece.Color, s));
                _log.Add(new Step { Kind = StepKind.Transformed, A = c, PieceId = piece.Id, Color = piece.Color, Special = s });
            }
            foreach (BoardPos c in cells) Hit(c, ClearCause.Special);
        }

        private void Blast(BoardPos origin, Special kind, Special partner, int color, List<BoardPos> cells)
        {
            _log.Add(new Step { Kind = StepKind.SpecialFired, A = origin, Special = kind, Partner = partner, Color = color, Cells = cells });
            foreach (BoardPos c in cells) Hit(c, ClearCause.Special);
        }

        private void Remove(BoardPos p, Piece piece, ClearCause cause)
        {
            _board.ClearPiece(p);
            _log.Add(new Step { Kind = StepKind.Cleared, A = p, PieceId = piece.Id, Color = piece.Color, Special = piece.Special, Cause = cause });
            Cleared++;
            if (piece.Color >= 0) Progress(GoalKind.Color, piece.Color);
            int ice = _board.IceHp(p);
            if (ice <= 0) return;
            _board.SetIce(p, ice - 1);
            HitLayer(p, BlockerLayer.Ice, ice - 1);
        }

        private void HitLayer(BoardPos p, BlockerLayer layer, int hpLeft)
        {
            _log.Add(new Step { Kind = StepKind.BlockerHit, A = p, Layer = layer, Value = hpLeft });
            if (hpLeft > 0) return;
            GoalKind kind = layer == BlockerLayer.Crate ? GoalKind.Crate : layer == BlockerLayer.Ice ? GoalKind.Ice : GoalKind.Chain;
            Progress(kind, -1);
        }

        private void Progress(GoalKind kind, int color)
        {
            if (_game.TryAddProgress(kind, color, out int index, out int value))
                _log.Add(new Step { Kind = StepKind.GoalProgressed, Index = index, Value = value, Color = color });
        }

        private void CollectCrates(BoardPos c, List<BoardPos> crates)
        {
            AddCrate(new BoardPos(c.X - 1, c.Y), crates);
            AddCrate(new BoardPos(c.X + 1, c.Y), crates);
            AddCrate(new BoardPos(c.X, c.Y - 1), crates);
            AddCrate(new BoardPos(c.X, c.Y + 1), crates);
        }

        private void AddCrate(BoardPos p, List<BoardPos> crates)
        {
            if (_board.InBounds(p) && _board.CrateHp(p) > 0 && !crates.Contains(p)) crates.Add(p);
        }
    }
}
