using UnityEngine;

namespace __NS__.Puzzle.View
{
    // Every timing that makes the board feel good, in one asset designers tune in Play Mode.
    [CreateAssetMenu(menuName = "Puzzle/Board Feel")]
    public class BoardFeelSO : ScriptableObject
    {
        [Header("Swap")]
        [Tooltip("Seconds for two pieces to trade places")]
        [SerializeField, Range(0.05f, 0.5f)] private float _swapTime = 0.16f;
        [Tooltip("Ease of the swap; a slight overshoot reads as snappy")]
        [SerializeField] private AnimationCurve _swapEase = BackOut(1.06f);

        [Header("Clear")]
        [Tooltip("Anticipation: pieces swell before they pop")]
        [SerializeField, Range(1f, 1.5f)] private float _popScale = 1.2f;
        [Tooltip("Seconds from swell to gone")]
        [SerializeField, Range(0.05f, 0.5f)] private float _popTime = 0.18f;
        [Tooltip("Pause after a special fires, before its cells pop (lets the effect read)")]
        [SerializeField, Range(0f, 0.4f)] private float _specialHold = 0.12f;

        [Header("Fall")]
        [Tooltip("Cells per second while falling")]
        [SerializeField, Min(1f)] private float _fallSpeed = 14f;
        [Tooltip("Landing curve; ending above 1 then back gives a small bounce")]
        [SerializeField] private AnimationCurve _fallEase = BackOut(1.04f);
        [Tooltip("Delay between neighbouring columns so refills ripple instead of dropping as a block")]
        [SerializeField, Range(0f, 0.05f)] private float _columnStagger = 0.015f;

        [Header("Cascade")]
        [Tooltip("Pause between cascade waves")]
        [SerializeField, Range(0f, 0.3f)] private float _wavePause = 0.05f;
        [Tooltip("Pitch added per cascade wave for the clear sound (combo build-up)")]
        [SerializeField, Range(0f, 0.2f)] private float _pitchPerWave = 0.06f;

        [Header("Hint")]
        [Tooltip("Idle seconds before the best move wiggles")]
        [SerializeField, Min(1f)] private float _hintDelay = 5f;

        public float SwapTime => _swapTime;
        public AnimationCurve SwapEase => _swapEase;
        public float PopScale => _popScale;
        public float PopTime => _popTime;
        public float SpecialHold => _specialHold;
        public float FallSpeed => _fallSpeed;
        public AnimationCurve FallEase => _fallEase;
        public float ColumnStagger => _columnStagger;
        public float WavePause => _wavePause;
        public float PitchPerWave => _pitchPerWave;
        public float HintDelay => _hintDelay;

        // Overshoots a few percent and settles: the snappy "landing" polished puzzle games use instead of a linear stop.
        private static AnimationCurve BackOut(float peak) =>
            new AnimationCurve(new Keyframe(0f, 0f, 0f, 2.2f), new Keyframe(0.72f, peak, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));
    }
}
