using System;
using System.Collections.Generic;
using UnityEngine;

namespace __NS__.Puzzle.View
{
    // Minimal position/scale tween list, ticked by its owner; zero allocations per frame once warmed up.
    public sealed class Tweens
    {
        private struct Tween
        {
            public Transform Target;
            public Vector3 From;
            public Vector3 To;
            public bool IsScale;
            public float Delay;
            public float Duration;
            public float Elapsed;
            public AnimationCurve Curve;
            public Action Done;
        }

        private readonly List<Tween> _active = new List<Tween>(128);

        public bool Busy => _active.Count > 0;

        public void Move(Transform target, Vector3 to, float duration, AnimationCurve curve, float delay = 0f, Action done = null) =>
            Add(target, target.localPosition, to, false, duration, curve, delay, done);

        public void MoveFrom(Transform target, Vector3 from, Vector3 to, float duration, AnimationCurve curve, float delay = 0f) =>
            Add(target, from, to, false, duration, curve, delay, null);

        public void Scale(Transform target, Vector3 from, Vector3 to, float duration, AnimationCurve curve, float delay = 0f, Action done = null) =>
            Add(target, from, to, true, duration, curve, delay, done);

        public void Tick(float deltaTime)
        {
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var t = _active[i];
                if (t.Target == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }

                t.Elapsed += deltaTime;
                var k = Mathf.Clamp01((t.Elapsed - t.Delay) / Mathf.Max(t.Duration, 0.0001f));
                if (t.Elapsed >= t.Delay)
                {
                    // Unclamped so overshoot curves (back/elastic ease) can pass the target and settle.
                    var v = Vector3.LerpUnclamped(t.From, t.To, t.Curve != null ? t.Curve.Evaluate(k) : k);
                    if (t.IsScale) t.Target.localScale = v;
                    else t.Target.localPosition = v;
                }

                if (k < 1f)
                {
                    _active[i] = t;
                    continue;
                }

                _active.RemoveAt(i);
                t.Done?.Invoke();
            }
        }

        private void Add(Transform target, Vector3 from, Vector3 to, bool scale, float duration, AnimationCurve curve, float delay, Action done)
        {
            // A new tween on the same target and channel replaces the old one, so interrupted moves never fight.
            for (var i = _active.Count - 1; i >= 0; i--)
                if (_active[i].Target == target && _active[i].IsScale == scale) _active.RemoveAt(i);
            if (scale) target.localScale = from;
            else target.localPosition = from;
            _active.Add(new Tween { Target = target, From = from, To = to, IsScale = scale, Delay = delay, Duration = duration, Curve = curve, Done = done });
        }
    }
}
