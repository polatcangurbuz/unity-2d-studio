using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace __NS__.Puzzle.View
{
    // Turns the core's ordered steps into timed "beats": steps of one beat animate together, the next beat waits for them.
    public sealed class StepPlayer
    {
        private enum Beat { Swap, Clear, Fire, Transform, Settle, Shuffle, Other }

        private readonly BoardLayout _layout;
        private readonly PieceViews _views;
        private readonly Tweens _tweens;
        private readonly BoardFeelSO _feel;
        private readonly Transform _space;
        private readonly BoardEvents _events;
        private readonly Dictionary<BoardPos, BoardPos> _mergeInto = new Dictionary<BoardPos, BoardPos>();
        private readonly List<Step> _pendingSpecials = new List<Step>();

        public StepPlayer(BoardLayout layout, PieceViews views, Tweens tweens, BoardFeelSO feel, Transform space, BoardEvents events)
        {
            _layout = layout;
            _views = views;
            _tweens = tweens;
            _feel = feel;
            _space = space;
            _events = events;
        }

        public async Awaitable Play(TurnResult result, CancellationToken token)
        {
            var steps = result.Steps;
            var i = 0;
            while (i < steps.Count)
            {
                var beat = Classify(steps[i].Kind);
                var wave = steps[i].Wave;
                var cleared = 0;
                if (beat == Beat.Settle) SpawnPendingSpecials();
                for (; i < steps.Count && steps[i].Wave == wave && Classify(steps[i].Kind) == beat; i++)
                    if (Apply(steps[i])) cleared++;
                if (cleared > 0) _events.RaiseWaveCleared(wave, cleared);
                while (_tweens.Busy) await Awaitable.NextFrameAsync(token);
                _mergeInto.Clear();
                if (beat == Beat.Fire && _feel.SpecialHold > 0f) await Awaitable.WaitForSecondsAsync(_feel.SpecialHold, token);
                if (beat == Beat.Settle && _feel.WavePause > 0f) await Awaitable.WaitForSecondsAsync(_feel.WavePause, token);
            }

            SpawnPendingSpecials();
            while (_tweens.Busy) await Awaitable.NextFrameAsync(token);
        }

        private static Beat Classify(StepKind kind)
        {
            switch (kind)
            {
                case StepKind.Swapped:
                case StepKind.SwapRejected: return Beat.Swap;
                case StepKind.Matched:
                case StepKind.Cleared:
                case StepKind.BlockerHit:
                case StepKind.GoalProgressed: return Beat.Clear;
                case StepKind.SpecialFired: return Beat.Fire;
                case StepKind.Transformed: return Beat.Transform;
                case StepKind.Fell:
                case StepKind.Spawned: return Beat.Settle;
                case StepKind.Shuffled: return Beat.Shuffle;
                default: return Beat.Other;
            }
        }

        // Returns true when a piece was cleared (counted for the wave's sound/combo).
        private bool Apply(Step s)
        {
            switch (s.Kind)
            {
                case StepKind.Swapped:
                    Move(s.PieceId, s.B, _feel.SwapTime, _feel.SwapEase);
                    Move(s.Value, s.A, _feel.SwapTime, _feel.SwapEase);
                    return false;
                case StepKind.SwapRejected:
                    Bounce(s.PieceId, s.A, s.B);
                    Bounce(s.Value, s.B, s.A);
                    _events.RaiseSwapRejected(World(s.A), World(s.B));
                    return false;
                case StepKind.Matched:
                    if (s.Special == Special.None || s.PieceId == 0) return false;
                    // The group gathers into the new special: reads as "these made that".
                    foreach (var c in s.Cells) _mergeInto[c] = s.A;
                    _pendingSpecials.Add(s);
                    return false;
                case StepKind.Cleared:
                    Clear(s);
                    return true;
                case StepKind.BlockerHit:
                    HitBlocker(s);
                    return false;
                case StepKind.SpecialFired:
                    _events.RaiseSpecialFired(World(s.A), s.Special, s.Partner);
                    return false;
                case StepKind.Transformed:
                    _views.Restyle(s.PieceId, s.Color, s.Special);
                    if (_views.TryGet(s.PieceId, out var turned)) Punch(turned, 1.25f);
                    return false;
                case StepKind.Fell:
                    Fall(s.PieceId, _layout.ToLocal(s.A), s.B);
                    return false;
                case StepKind.Spawned:
                    var top = _layout.TopRow(s.A.X);
                    var from = _layout.ToLocal(s.A.X, top - s.Value);
                    _views.Spawn(s.PieceId, s.Color, Special.None, from);
                    Fall(s.PieceId, from, s.A);
                    return false;
                case StepKind.Shuffled:
                    Move(s.PieceId, s.B, 0.35f, _feel.SwapEase);
                    _events.RaiseShuffled();
                    return false;
                case StepKind.GoalProgressed:
                    _events.RaiseGoalProgressed(s.Index, s.Value);
                    return false;
                case StepKind.LevelEnded:
                    _events.RaiseLevelEnded(s.Value == 1);
                    return false;
                default:
                    return false;
            }
        }

        private void Clear(Step s)
        {
            _events.RaisePieceCleared(World(s.A), s.Color, s.Special);
            if (!_views.TryGet(s.PieceId, out var root)) return;
            var id = s.PieceId;
            var big = Vector3.one * _feel.PopScale;
            // Swell (anticipation), then collapse; the release waits for the collapse.
            _tweens.Scale(root, Vector3.one, big, _feel.PopTime * 0.35f, null, 0f,
                () => _tweens.Scale(root, big, Vector3.zero, _feel.PopTime * 0.65f, null, 0f, () => _views.Release(id)));
            if (_mergeInto.TryGetValue(s.A, out var into)) _tweens.Move(root, _layout.ToLocal(into), _feel.PopTime, null);
        }

        private void HitBlocker(Step s)
        {
            _events.RaiseBlockerHit(World(s.A), s.Layer, s.Value);
            if (s.Value > 0)
            {
                _views.SetBlocker(s.A, s.Layer, s.Value);
                if (_views.TryGetBlocker(s.A, s.Layer, out var hit)) Punch(hit, 1.15f);
                return;
            }

            if (!_views.TryGetBlocker(s.A, s.Layer, out var root)) return;
            var cell = s.A;
            var layer = s.Layer;
            _tweens.Scale(root, Vector3.one, Vector3.zero, _feel.PopTime, null, 0f, () => _views.SetBlocker(cell, layer, 0));
        }

        private void SpawnPendingSpecials()
        {
            foreach (var s in _pendingSpecials)
            {
                var root = _views.Spawn(s.PieceId, s.Color, s.Special, _layout.ToLocal(s.A));
                _tweens.Scale(root, Vector3.zero, Vector3.one, _feel.PopTime, _feel.SwapEase);
            }

            _pendingSpecials.Clear();
        }

        private void Fall(int id, Vector3 from, BoardPos to)
        {
            if (!_views.TryGet(id, out var root)) return;
            var target = _layout.ToLocal(to);
            var cells = Vector3.Distance(from, target) / _layout.CellSize;
            _tweens.MoveFrom(root, from, target, Mathf.Max(0.08f, cells / _feel.FallSpeed), _feel.FallEase, to.X * _feel.ColumnStagger);
        }

        private void Move(int id, BoardPos to, float time, AnimationCurve ease)
        {
            if (_views.TryGet(id, out var root)) _tweens.Move(root, _layout.ToLocal(to), time, ease);
        }

        // Half-way toward the other cell and back: "that swap doesn't match".
        private void Bounce(int id, BoardPos from, BoardPos toward)
        {
            if (!_views.TryGet(id, out var root)) return;
            var home = _layout.ToLocal(from);
            var half = Vector3.Lerp(home, _layout.ToLocal(toward), 0.5f);
            _tweens.Move(root, half, _feel.SwapTime * 0.6f, null, 0f, () => _tweens.Move(root, home, _feel.SwapTime * 0.6f, _feel.SwapEase));
        }

        private void Punch(Transform root, float size) =>
            _tweens.Scale(root, Vector3.one * size, Vector3.one, _feel.PopTime, _feel.SwapEase);

        private Vector3 World(BoardPos p) => _space.TransformPoint(_layout.ToLocal(p));
    }
}
