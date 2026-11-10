When to read: something must happen inside the Unity Editor (objects, tests, Console, settings). Pure C# edits don't need this file.
Sections: Detect · MCP · No MCP · Builder gotchas · YAML

# Unity bridge

## Detect
Once per session: ToolSearch "unity" / "scene" / "console" (names vary; don't hard-code). Record `bridge: mcp | menu-scripts` in `.studio/brief.md`.

## MCP
- Use for scenes/GameObjects/prefabs, components/refs, menu items (`Tools/Studio/Setup Project`), refresh, tests, Console.
- After each compile/scene change: read Console (errors/warnings) → fix → repeat until clean.
- Query scene structure via MCP, not YAML. Batch edits per call. Same failure twice → stop, tell producer.
- Install: Package Manager → git URL `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#<release-tag>`
  (or `openupm add com.coplaydev.unity-mcp`), then Window → MCP for Unity → Configure All Detected Clients.
  Unity 2021.3+, Python 3.10+ with `uv`. MIT → log `.studio/deps.md`. Check README for current tag.

## No MCP
- Write C# directly (compiles on Unity focus).
- Click-work the user can do in ≤5 steps (create SO via Create menu, assign fields) → give the step list. No menu script.
- Repetitive/structural editor work → idempotent `[MenuItem("Tools/Studio/<Action>")]` script in `Assets/_Project/Code/Editor/` + ask for one click.
  Builders: `using Studio.EditorTools;` (namespace of all Studio Editor tools), then `StudioEditorUtil` first:
  `BuildScene(path, scene => { …create/load assets and objects here… })` (new scene → build → save, safe order), `Wire(obj, ("_field", value)…)` (private [SerializeField], Object[]/enum/int/float/bool/Vector2/3/Color/string; null/destroyed value → LogError),
  `LoadOrCreate<T>`, `EnsureFolder`, `EnsureLayer`, `AddToBuildSettings`, `Separator`, `NewActor(name, sprite, order, layer, out visual)`,
  `SavePrefab`, `OpenOrCreateScene`, `BakeTilemapColliders(tilemap, composite = true)`. Placeholder art: `placeholder_sprites.py`, not pixel grids in C#.
- Console: `StudioConsoleBridge` writes compile errors + Error/Exception logs (play mode too) to `.studio/console.log`, each line
  `[HH:mm:ss]`-stamped; reset each compile; each Play replaces the previous Play's lines (`play start`); `mark build <scene>` from
  `BuildScene`; `package-…` lines come from Unity packages, not project code. Test Runner summaries → `.studio/test-results.txt`.
  The bridge only runs once it compiled cleanly: before that, `compile_check.py --editor-log` reads Unity's own Editor.log, and the
  Stop hook's `compile_check.py` (dotnet build of Unity's `.csproj`) catches compile errors before the user switches to Unity. After code changes tell the user "switch to Unity" (it compiles on focus),
  then read those files. Never ask the user to paste logs. Tests: user runs Test Runner → EditMode → Run All, or batch:
  `"<Unity.exe>" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults .studio/test-results.xml -quit`
  then grep `result="Failed"`.

## Builder gotchas
- `EditorSceneManager.NewScene(…, Single)` unloads unreferenced assets: SOs/prefabs loaded before it wire as null (spawner without prefab,
  events that never fire). Use `BuildScene` (or `OpenOrCreateScene`) and load assets inside it.
- `CompositeCollider2D` auto-adds a dynamic `Rigidbody2D`: add the Rigidbody2D first, set `bodyType = Static`, then the composite.
- Tilemap colliders: `SetTile` all tiles, then `BakeTilemapColliders` (body, colliders, ProcessTilemapChanges, GenerateGeometry) before `SaveScene`;
  colliders added before tiles otherwise save an empty composite (player falls through).
- Unity 6: `TilemapCollider2D.compositeOperation = Collider2D.CompositeOperation.Merge`, not `usedByComposite`.
- URP 2D Renderer: Lit sprites are black without a Global `Light2D` in the scene.
- URP `PixelPerfectCamera` (`UnityEngine.Rendering.Universal`) ≠ `com.unity.2d.pixel-perfect` (`UnityEngine.U2D`); with URP use the first.
- Unity 6: `Rigidbody2D.linearVelocity`, not `velocity` (obsolete).
- `Wire` needs exact serialized names (`_speed`, `<Speed>k__BackingField` for `[field: SerializeField]`).

## YAML
`.unity` `.prefab` `.asset` `.controller`: never read whole.
- Script usages: GUID from `<Script>.cs.meta` → `grep -rl "<guid>" Assets/_Project --include=*.prefab --include=*.unity`.
- SO value: grep field name in that one `.asset`.
- Hand-edit only trivial text; structure via MCP or Editor script.
