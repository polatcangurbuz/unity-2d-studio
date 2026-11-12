using UnityEngine;

namespace __NS__.Puzzle.View
{
    // Feedback that only listens: colour bursts per cleared piece, shake on specials, clear sound rising with the cascade.
    public class BoardJuice : MonoBehaviour
    {
        [SerializeField] private BoardView _board;
        [SerializeField] private BoardFeelSO _feel;
        [Tooltip("One shared system; pieces call Emit on it, nothing is instantiated")]
        [SerializeField] private ParticleSystem _burst;
        [Tooltip("Burst colour per piece colour index")]
        [SerializeField] private Color[] _colors = new Color[0];
        [SerializeField, Range(1, 30)] private int _particlesPerPiece = 8;
        [SerializeField] private Transform _shakeTarget;
        [SerializeField, Range(0f, 0.5f)] private float _shakeStrength = 0.15f;
        [SerializeField, Range(0.05f, 0.6f)] private float _shakeTime = 0.25f;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip _clearClip;
        [SerializeField] private AudioClip _specialClip;
        [Tooltip("Accessibility: players can turn shake off in settings")]
        [SerializeField] private bool _shakeEnabled = true;

        private Vector3 _shakeHome;
        private float _shakeLeft;

        public bool ShakeEnabled { get => _shakeEnabled; set => _shakeEnabled = value; }

        private void Awake()
        {
            if (_shakeTarget != null) _shakeHome = _shakeTarget.localPosition;
        }

        private void OnEnable()
        {
            _board.Events.PieceCleared += OnPieceCleared;
            _board.Events.WaveCleared += OnWaveCleared;
            _board.Events.SpecialFired += OnSpecialFired;
        }

        private void OnDisable()
        {
            _board.Events.PieceCleared -= OnPieceCleared;
            _board.Events.WaveCleared -= OnWaveCleared;
            _board.Events.SpecialFired -= OnSpecialFired;
        }

        private void Update()
        {
            if (_shakeTarget == null || _shakeLeft <= 0f) return;
            _shakeLeft -= Time.deltaTime;
            var k = Mathf.Clamp01(_shakeLeft / _shakeTime);
            _shakeTarget.localPosition = _shakeLeft > 0f ? _shakeHome + (Vector3)(Random.insideUnitCircle * (_shakeStrength * k)) : _shakeHome;
        }

        private void OnPieceCleared(Vector3 world, int color, Special special)
        {
            if (_burst == null) return;
            var p = new ParticleSystem.EmitParams
            {
                position = world,
                applyShapeToPosition = true,
                startColor = color >= 0 && color < _colors.Length ? _colors[color] : Color.white,
            };
            _burst.Emit(p, special == Special.None ? _particlesPerPiece : _particlesPerPiece * 2);
        }

        private void OnWaveCleared(int wave, int count)
        {
            if (_audio == null || _clearClip == null) return;
            _audio.pitch = 1f + wave * _feel.PitchPerWave;
            _audio.PlayOneShot(_clearClip);
        }

        private void OnSpecialFired(Vector3 world, Special special, Special partner)
        {
            if (_audio != null && _specialClip != null) _audio.PlayOneShot(_specialClip);
            if (!_shakeEnabled || _shakeTarget == null) return;
            // Combos (two specials) shake harder than a single one.
            _shakeLeft = partner == Special.None ? _shakeTime : _shakeTime * 1.6f;
        }
    }
}
