using UnityEngine;

namespace __NS__.Puzzle.View
{
    // Keeps UI out of notches and home bars: resizes this RectTransform to Screen.safeArea when it changes.
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _applied;
        private Vector2Int _screen;

        private void Awake() => _rect = (RectTransform)transform;

        private void Update()
        {
            var area = Screen.safeArea;
            if (area == _applied && _screen.x == Screen.width && _screen.y == Screen.height) return;
            _applied = area;
            _screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            _rect.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            _rect.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            _rect.offsetMin = _rect.offsetMax = Vector2.zero;
        }
    }
}
