using System;
using UnityEngine;

namespace __NS__.Puzzle.View
{
    // Everything audio, VFX, haptics and HUD react to. The board never calls them; they subscribe here.
    public sealed class BoardEvents
    {
        public event Action<Vector3, int, Special> PieceCleared;
        public event Action<int, int> WaveCleared;
        public event Action<Vector3, Special, Special> SpecialFired;
        public event Action<Vector3, BlockerLayer, int> BlockerHit;
        public event Action<Vector3, Vector3> SwapRejected;
        public event Action Shuffled;
        public event Action<int> MovesChanged;
        public event Action<int, int> GoalProgressed;
        public event Action<bool> LevelEnded;
        public event Action<bool> BusyChanged;

        internal void RaisePieceCleared(Vector3 world, int color, Special special) => PieceCleared?.Invoke(world, color, special);
        internal void RaiseWaveCleared(int wave, int count) => WaveCleared?.Invoke(wave, count);
        internal void RaiseSpecialFired(Vector3 world, Special special, Special partner) => SpecialFired?.Invoke(world, special, partner);
        internal void RaiseBlockerHit(Vector3 world, BlockerLayer layer, int hpLeft) => BlockerHit?.Invoke(world, layer, hpLeft);
        internal void RaiseSwapRejected(Vector3 a, Vector3 b) => SwapRejected?.Invoke(a, b);
        internal void RaiseShuffled() => Shuffled?.Invoke();
        internal void RaiseMovesChanged(int left) => MovesChanged?.Invoke(left);
        internal void RaiseGoalProgressed(int index, int value) => GoalProgressed?.Invoke(index, value);
        internal void RaiseLevelEnded(bool won) => LevelEnded?.Invoke(won);
        internal void RaiseBusyChanged(bool busy) => BusyChanged?.Invoke(busy);
    }
}
