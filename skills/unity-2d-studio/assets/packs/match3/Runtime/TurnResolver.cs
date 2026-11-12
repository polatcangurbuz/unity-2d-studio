using System.Collections.Generic;

namespace __NS__.Puzzle
{
    internal sealed class TurnResolver
    {
        private const int MaxWaves = 100;

        private readonly PuzzleGame _game;
        private readonly Board _board;
        private readonly StepLog _log = new StepLog();
        private readonly Blaster _blaster;
        private bool _shuffled;

        public TurnResolver(PuzzleGame game)
        {
            _game = game;
            _board = game.Board;
            _blaster = new Blaster(game, _log);
        }

        public TurnResult Swap(BoardPos a, BoardPos b)
        {
            if (_game.State != GameState.Playing || !CanSwap(a, b)) return Result(false);
            Piece pa = _board.PieceAt(a);
            Piece pb = _board.PieceAt(b);
            _board.SwapPieces(a, b);
            bool special = pa.Special != Special.None || pb.Special != Special.None;
            List<MatchGroup> groups = special ? null : MatchFinder.Find(_board, b, a);
            if (!special && groups.Count == 0)
            {
                _board.SwapPieces(a, b);
                _log.Add(new Step { Kind = StepKind.SwapRejected, A = a, B = b, PieceId = pa.Id, Value = pb.Id });
                return Result(false);
            }
            _log.Add(new Step { Kind = StepKind.Swapped, A = a, B = b, PieceId = pa.Id, Value = pb.Id });
            _game.MovesLeft--;
            if (special) _blaster.FireSwapped(a, b);
            else _blaster.ClearGroups(groups);
            return Finish();
        }

        public TurnResult Booster(BoosterKind kind, BoardPos at)
        {
            if (_game.State != GameState.Playing) return Result(false);
            if (kind == BoosterKind.Shuffle)
            {
                _shuffled = Shuffler.TryShuffle(_board, _game.Rng, _log);
                return Finish();
            }
            if (!_board.InBounds(at) || (kind == BoosterKind.Hammer && _board.IsHole(at))) return Result(false);
            switch (kind)
            {
                case BoosterKind.Hammer:
                    _blaster.Hit(at, ClearCause.Booster);
                    break;
                case BoosterKind.Row:
                    for (int x = 0; x < _board.Width; x++) _blaster.Hit(new BoardPos(x, at.Y), ClearCause.Booster);
                    break;
                case BoosterKind.Column:
                    for (int y = 0; y < _board.Height; y++) _blaster.Hit(new BoardPos(at.X, y), ClearCause.Booster);
                    break;
            }
            _blaster.FireAll();
            return Finish();
        }

        private TurnResult Finish()
        {
            Cascade();
            EndTurn();
            return Result(true);
        }

        private void Cascade()
        {
            while (true)
            {
                Gravity.Run(_board, _game.Rng, _game.Level.Colors, _log);
                if (_log.Wave >= MaxWaves) return;
                List<MatchGroup> groups = MatchFinder.Find(_board, null, null);
                if (groups.Count == 0) return;
                _log.Wave++;
                _blaster.ClearGroups(groups);
            }
        }

        private void EndTurn()
        {
            if (_game.AllGoalsMet) End(true);
            else if (_game.MovesLeft <= 0) End(false);
            else if (!MoveFinder.HasMove(_board))
            {
                if (Shuffler.TryShuffle(_board, _game.Rng, _log)) _shuffled = true;
                else End(false);
            }
        }

        private void End(bool won)
        {
            _game.State = won ? GameState.Won : GameState.Lost;
            _log.Add(new Step { Kind = StepKind.LevelEnded, Value = won ? 1 : 0 });
        }

        private bool CanSwap(BoardPos a, BoardPos b) =>
            _board.InBounds(a) && _board.InBounds(b) && a.IsAdjacent(b) && _board.CanMove(a) && _board.CanMove(b);

        private TurnResult Result(bool accepted) =>
            new TurnResult(accepted, _log.Steps, _log.Wave, _blaster.Cleared, _blaster.SpecialsCreated, _shuffled, _game.State, _game.MovesLeft);
    }
}
