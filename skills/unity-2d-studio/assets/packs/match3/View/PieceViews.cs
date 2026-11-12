using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace __NS__.Puzzle.View
{
    // Pooled pieces (by id) and blockers (by cell + layer): tweened root at scale 1, sprite child fitted to the cell.
    public sealed class PieceViews
    {
        private const int PieceOrder = 10;
        private const float Fill = 0.92f;
        private readonly Transform _root;
        private readonly BoardSkinSO _skin;
        private readonly BoardLayout _layout;
        private readonly ObjectPool<SpriteRenderer> _pool;
        private readonly Dictionary<int, SpriteRenderer> _pieces = new Dictionary<int, SpriteRenderer>(128);
        private readonly Dictionary<(BoardPos, BlockerLayer), SpriteRenderer> _blockers = new Dictionary<(BoardPos, BlockerLayer), SpriteRenderer>();

        public PieceViews(Transform root, BoardSkinSO skin, BoardLayout layout, int prewarm)
        {
            _root = root;
            _skin = skin;
            _layout = layout;
            _pool = new ObjectPool<SpriteRenderer>(Create, r => r.transform.parent.gameObject.SetActive(true),
                r => r.transform.parent.gameObject.SetActive(false), r => Object.Destroy(r.transform.parent.gameObject), false, prewarm);
            var warm = new List<SpriteRenderer>(prewarm);
            for (var i = 0; i < prewarm; i++) warm.Add(_pool.Get());
            foreach (var r in warm) _pool.Release(r);
        }

        public Transform Spawn(int id, int color, Special special, Vector3 local)
        {
            var r = _pool.Get();
            var root = r.transform.parent;
            root.localPosition = local;
            root.localScale = Vector3.one;
            r.sortingOrder = PieceOrder;
            _pieces[id] = r;
            Restyle(id, color, special);
            return root;
        }

        public bool TryGet(int id, out Transform root)
        {
            var found = _pieces.TryGetValue(id, out var r);
            root = found ? r.transform.parent : null;
            return found;
        }

        public void Restyle(int id, int color, Special special)
        {
            if (!_pieces.TryGetValue(id, out var r)) return;
            Show(r, _skin.Piece(color, special));
            r.color = _skin.Tint(color, special);
        }

        public void Release(int id)
        {
            if (!_pieces.TryGetValue(id, out var r)) return;
            _pieces.Remove(id);
            _pool.Release(r);
        }

        // Crates cover the cell (no piece), ice lies under the piece, chains over it.
        public void SetBlocker(BoardPos cell, BlockerLayer layer, int hp)
        {
            var key = (cell, layer);
            _blockers.TryGetValue(key, out var r);
            if (hp <= 0)
            {
                if (r == null) return;
                _blockers.Remove(key);
                _pool.Release(r);
                return;
            }

            if (r == null)
            {
                r = _pool.Get();
                r.transform.parent.localPosition = _layout.ToLocal(cell);
                r.transform.parent.localScale = Vector3.one;
                r.color = Color.white;
                r.sortingOrder = layer == BlockerLayer.Ice ? PieceOrder - 1 : PieceOrder + 1;
                _blockers[key] = r;
            }

            Show(r, _skin.Blocker(layer, hp));
        }

        public bool TryGetBlocker(BoardPos cell, BlockerLayer layer, out Transform root)
        {
            var found = _blockers.TryGetValue((cell, layer), out var r);
            root = found ? r.transform.parent : null;
            return found;
        }

        private void Show(SpriteRenderer r, Sprite sprite)
        {
            r.sprite = sprite;
            var size = sprite != null ? Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y) : 1f;
            r.transform.localScale = Vector3.one * (_layout.CellSize * Fill / Mathf.Max(size, 0.0001f));
        }

        private SpriteRenderer Create()
        {
            var root = new GameObject("Piece").transform;
            root.SetParent(_root, false);
            var child = new GameObject("Sprite");
            child.transform.SetParent(root, false);
            var r = child.AddComponent<SpriteRenderer>();
            // Refills start above the board: the board's SpriteMask hides them until they enter.
            r.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            return r;
        }
    }
}
