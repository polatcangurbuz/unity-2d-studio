using UnityEngine;

namespace __NS__.Puzzle.View
{
    // What the board looks like: swap this asset for a new theme, no code change.
    [CreateAssetMenu(menuName = "Puzzle/Board Skin")]
    public class BoardSkinSO : ScriptableObject
    {
        [Tooltip("One sprite per color index (red green blue yellow purple orange)")]
        [SerializeField] private Sprite[] _pieces = new Sprite[0];
        [SerializeField] private Sprite _rocketH;
        [SerializeField] private Sprite _rocketV;
        [SerializeField] private Sprite _bomb;
        [SerializeField] private Sprite _colorBomb;
        [Tooltip("Cell background; alternate tint below gives a checkerboard")]
        [SerializeField] private Sprite _cell;
        [SerializeField] private Color _cellTint = new Color(1f, 1f, 1f, 0.12f);
        [SerializeField] private Color _cellTintAlt = new Color(1f, 1f, 1f, 0.06f);
        [Tooltip("Index = hp - 1")]
        [SerializeField] private Sprite[] _crate = new Sprite[0];
        [Tooltip("Index = hp - 1")]
        [SerializeField] private Sprite[] _ice = new Sprite[0];
        [Tooltip("Index = hp - 1")]
        [SerializeField] private Sprite[] _chain = new Sprite[0];
        [Tooltip("Specials keep the piece colour as a tint so players can still read them")]
        [SerializeField] private Color[] _specialTints = new Color[0];

        public Sprite Cell => _cell;
        public Color CellTint => _cellTint;
        public Color CellTintAlt => _cellTintAlt;

        public Sprite Piece(int color, Special special)
        {
            switch (special)
            {
                case Special.RocketH: return _rocketH;
                case Special.RocketV: return _rocketV;
                case Special.Bomb: return _bomb;
                case Special.ColorBomb: return _colorBomb;
                default: return color >= 0 && color < _pieces.Length ? _pieces[color] : null;
            }
        }

        public Color Tint(int color, Special special) =>
            special == Special.None || special == Special.ColorBomb || color < 0 || color >= _specialTints.Length ? Color.white : _specialTints[color];

        public Sprite Blocker(BlockerLayer layer, int hp)
        {
            var set = layer == BlockerLayer.Crate ? _crate : layer == BlockerLayer.Ice ? _ice : _chain;
            return hp <= 0 || set.Length == 0 ? null : set[Mathf.Min(hp, set.Length) - 1];
        }
    }
}
