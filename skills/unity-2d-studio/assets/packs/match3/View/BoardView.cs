using System;
using UnityEngine;

namespace __NS__.Puzzle.View
{
    // Thin adapter: input → PuzzleGame → StepPlayer. Rules live in the core; reactions (audio, VFX, HUD) subscribe to Events.
    public class BoardView : MonoBehaviour
    {
        [SerializeField] private TextAsset _level;
        [SerializeField] private BoardFeelSO _feel;
        [SerializeField] private BoardSkinSO _skin;
        [SerializeField] private SwipeInput _input;
        [Tooltip("World units per cell")]
        [SerializeField, Min(0.1f)] private float _cellSize = 1f;

        private readonly Tweens _tweens = new Tweens();
        private BoardLayout _layout;
        private PieceViews _views;
        private StepPlayer _player;
        private bool _busy;
        private bool _hasSelection;
        private BoardPos _selected;
        private float _idle;

        public BoardEvents Events { get; } = new BoardEvents();
        public PuzzleGame Game { get; private set; }
        public BoardLayout Layout => _layout;

        private void Awake()
        {
            if (_level != null) Load(_level.text);
        }

        private void OnEnable()
        {
            if (_input == null) return;
            _input.Pressed += OnPressed;
            _input.Swiped += OnSwiped;
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.Pressed -= OnPressed;
            _input.Swiped -= OnSwiped;
        }

        // Level select / retry: rebuilds the board from level text (LevelParser format).
        public void Load(string levelText)
        {
            for (var i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            Game = new PuzzleGame(LevelParser.Parse(levelText));
            var board = Game.Board;
            _layout = new BoardLayout(board, _cellSize);
            _views = new PieceViews(transform, _skin, _layout, board.Width * board.Height + 16);
            _player = new StepPlayer(_layout, _views, _tweens, _feel, transform, Events);
            BuildCells(board);
            for (var y = 0; y < board.Height; y++)
            for (var x = 0; x < board.Width; x++)
            {
                var p = new BoardPos(x, y);
                if (board.IsHole(p)) continue;
                _views.SetBlocker(p, BlockerLayer.Crate, board.CrateHp(p));
                _views.SetBlocker(p, BlockerLayer.Ice, board.IceHp(p));
                _views.SetBlocker(p, BlockerLayer.Chain, board.ChainHp(p));
                var piece = board.PieceAt(p);
                if (!piece.IsEmpty) _views.Spawn(piece.Id, piece.Color, piece.Special, _layout.ToLocal(p));
            }

            Events.RaiseMovesChanged(Game.MovesLeft);
        }

        public async void UseBooster(BoosterKind kind, BoardPos at)
        {
            if (!CanAct()) return;
            await Run(() => Game.UseBooster(kind, at));
        }

        private void Update()
        {
            _tweens.Tick(Time.deltaTime);
            if (_busy || Game == null || Game.State != GameState.Playing) return;
            _idle += Time.deltaTime;
            if (_idle < _feel.HintDelay) return;
            _idle = 0f;
            if (MoveFinder.TryGetBest(Game, out var hint)) Wiggle(hint);
        }

        private void OnPressed(Vector3 world)
        {
            if (!CanAct() || !_layout.TryCell(transform.InverseTransformPoint(world), out var cell)) return;
            if (_hasSelection && cell.IsAdjacent(_selected))
            {
                _hasSelection = false;
                TrySwap(_selected, cell);
                return;
            }

            _hasSelection = true;
            _selected = cell;
        }

        private void OnSwiped(Vector3 startWorld, Vector2Int dir)
        {
            if (!CanAct() || !_layout.TryCell(transform.InverseTransformPoint(startWorld), out var from)) return;
            _hasSelection = false;
            // Screen up is row - 1 in the core (row 0 is the top).
            var to = new BoardPos(from.X + dir.x, from.Y - dir.y);
            if (Game.Board.InBounds(to) && !Game.Board.IsHole(to)) TrySwap(from, to);
        }

        private async void TrySwap(BoardPos a, BoardPos b) => await Run(() => Game.TrySwap(a, b));

        private async Awaitable Run(Func<TurnResult> turn)
        {
            _busy = true;
            _idle = 0f;
            Events.RaiseBusyChanged(true);
            try
            {
                var result = turn();
                await _player.Play(result, destroyCancellationToken);
                if (result.Accepted) Events.RaiseMovesChanged(result.MovesLeft);
            }
            catch (OperationCanceledException)
            {
                // Destroyed mid-animation (scene change); nothing to finish.
            }
            finally
            {
                _busy = false;
                Events.RaiseBusyChanged(false);
            }
        }

        private bool CanAct() => !_busy && Game != null && Game.State == GameState.Playing;

        private void Wiggle(Swap move)
        {
            foreach (var p in new[] { move.A, move.B })
                if (_views.TryGet(Game.Board.PieceAt(p).Id, out var root))
                    _tweens.Scale(root, Vector3.one * 1.15f, Vector3.one, 0.4f, _feel.SwapEase);
        }

        private void BuildCells(Board board)
        {
            if (_skin.Cell == null) return;
            for (var y = 0; y < board.Height; y++)
            for (var x = 0; x < board.Width; x++)
            {
                if (board.IsHole(new BoardPos(x, y))) continue;
                var cell = new GameObject("Cell").AddComponent<SpriteRenderer>();
                cell.transform.SetParent(transform, false);
                cell.transform.localPosition = _layout.ToLocal(x, y);
                cell.sprite = _skin.Cell;
                var size = Mathf.Max(_skin.Cell.bounds.size.x, _skin.Cell.bounds.size.y);
                cell.transform.localScale = Vector3.one * (_cellSize / Mathf.Max(size, 0.0001f));
                cell.color = (x + y) % 2 == 0 ? _skin.CellTint : _skin.CellTintAlt;
                cell.sortingOrder = 0;
            }
        }
    }
}
