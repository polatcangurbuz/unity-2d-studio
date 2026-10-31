When to read: task defines game data, loads assets, saves, or touches 2D rendering/physics perf. Read only the needed `##` section.
Sections: ScriptableObjects · Loading · Save · Sprites · Tilemaps · Physics · Pooling · Profiler

# Data, assets, 2D optimization

## ScriptableObjects
Use for: config/static data (stats, waves, items, sound defs) · event channels (architecture § Communication) ·
runtime sets (register `OnEnable`, remove `OnDisable`) · strategies (abstract SO + method).
- Assets read-only at runtime; runtime/save state in plain classes, copied from SO at spawn (Play Mode edits persist in editor, not builds).
- `[SerializeField] private` + read-only properties; `[Min]/[Range]/[Tooltip]`; `OnValidate` for cross-field checks.
- `[CreateAssetMenu(menuName = "<Game>/<Domain>/<Name>")]`. Assets in `Assets/_Project/Data/<Domain>/`.
```csharp
[CreateAssetMenu(menuName = "Skyhop/Enemies/Wave")]
public class WaveSO : ScriptableObject
{
    [SerializeField] private List<SpawnGroup> _groups = new();
    public IReadOnlyList<SpawnGroup> Groups => _groups;
}
```

## Loading
| Asset | Load with |
|---|---|
| bootstrap-only tiny (settings SO, loading sprite) | Resources or direct ref |
| levels beyond first few | Addressables, label per level |
| per-level enemy/VFX prefabs | Addressables `level-NN` / `shared` |
| music | Addressables (streaming) |
| always-on UI | direct refs from Bootstrap/UI scene |
- Never large/level assets in `Resources/`.
- Groups by load lifetime; async load; keep handle; `Addressables.Release(handle)` on owner unload. `ISceneService` owns scene handles.
- Pool from a loaded prefab ref, not `InstantiateAsync` per spawn.
- Small game (1-3 scenes, <~100 MB): direct refs OK + `// DEBT: move to Addressables when levels > 3`.

## Save
Plain serializable classes → JSON in `Application.persistentDataPath`; `version` field + migrate on load; write temp file then replace.

## Sprites
- One PPU per game (16/32 pixel art, 100 hi-res); sprite sizes are multiples.
- Pixel art: Point filter, Compression None, no mipmaps, Pixel Perfect Camera. Hi-res: Bilinear, compressed.
- Sprite Atlas V2 per lifetime group (UI, Player, Level_01, Enemies). Consistent pivots (feet for characters); fixed-cell slicing.
- Import rules via `AssetPostprocessor` in `Code/Editor`, keyed by folder.

## Tilemaps
Chunk mode for static ground; separate ground/decoration/collision maps. Tilemap Collider 2D + Composite Collider 2D (static RB). Rule Tiles (2D Tilemap Extras).

## Physics
Layer Collision Matrix: only needed pairs. Triggers for pickups/hitboxes; `OverlapCircle` with buffer for 1-frame checks.
Kinematic movers: `Rigidbody2D.MovePosition` in `FixedUpdate`. Interpolate player RB.

## Pooling
- Pool bullets, VFX, enemies, damage numbers, audio sources: `ObjectPool<T>` used directly by the owner (patterns § Pool). Prewarm at load.
- No wrapper class unless ≥2 owners share identical setup.
- Release on death/despawn, never `Destroy`. Subscribe instance events once at create, not on every Get.
- Target 0 B GC/frame in gameplay. Allocators: LINQ, per-frame string building, closures, non-buffer `Physics2D`/`GetComponents`.

## Profiler
Per milestone: main thread ms vs budget (16.6 ms @60, 30% headroom) · GC/frame = 0 · draw calls/SetPass after atlasing ·
overdraw · physics contacts · texture/audio memory · dev build on target device.
