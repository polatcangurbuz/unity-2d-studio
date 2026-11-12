using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace __NS__.Puzzle.View
{
    // Pointer (mouse or touch) → world-space press and drag direction. Knows nothing about the board.
    public class SwipeInput : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [Tooltip("Drag distance in world units that counts as a swipe")]
        [SerializeField, Min(0.05f)] private float _swipeDistance = 0.35f;

        private bool _wasDown;
        private bool _swiped;
        private Vector3 _start;

        public event Action<Vector3> Pressed;
        public event Action<Vector3, Vector2Int> Swiped;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
        }

        private void Update()
        {
            if (!TryReadPointer(out var down, out var screen)) return;
            var world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
            if (down && !_wasDown)
            {
                _start = world;
                _swiped = false;
                Pressed?.Invoke(world);
            }
            else if (down && !_swiped)
            {
                var d = world - _start;
                if (d.magnitude >= _swipeDistance)
                {
                    _swiped = true;
                    var dir = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? new Vector2Int(Math.Sign(d.x), 0) : new Vector2Int(0, Math.Sign(d.y));
                    Swiped?.Invoke(_start, dir);
                }
            }

            _wasDown = down;
        }

        private static bool TryReadPointer(out bool down, out Vector2 screen)
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = Pointer.current;
            down = pointer != null && pointer.press.isPressed;
            screen = pointer != null ? pointer.position.ReadValue() : Vector2.zero;
            return pointer != null;
#else
            down = Input.GetMouseButton(0);
            screen = Input.mousePosition;
            return true;
#endif
        }
    }
}
