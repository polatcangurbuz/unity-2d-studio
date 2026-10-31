When to read: something must happen inside the Unity Editor (objects, tests, Console, settings). Pure C# edits don't need this file.
Sections: Detect · MCP · No MCP · YAML

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
- Console: ask user once for errors only. Tests: user runs Test Runner → EditMode → Run All, or batch:
  `"<Unity.exe>" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults .studio/test-results.xml -quit`
  then grep `result="Failed"`.

## YAML
`.unity` `.prefab` `.asset` `.controller`: never read whole.
- Script usages: GUID from `<Script>.cs.meta` → `grep -rl "<guid>" Assets/_Project --include=*.prefab --include=*.unity`.
- SO value: grep field name in that one `.asset`.
- Hand-edit only trivial text; structure via MCP or Editor script.
