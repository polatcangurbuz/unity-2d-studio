---
name: unity-2d-studio
description: |
  Use this skill for any work on a 2D game, in Unity or with no engine named. It runs a small studio (producer, architect, gameplay coder, art director, VFX artist, QA, scribe) that ships clean code and consistent game art on a small token budget. Trigger when the user's request serves their own 2D game, for example:
  - code and structure: project setup, folders/asmdefs, mechanics, enemies, combat, tilemaps, physics, pooling/GC, profiling
  - game art: sprites and sprite sheets, animation frames, tiles, palettes, art bibles, and prompts for image models like Midjourney or SD that make game assets
  - feel: particles/VFX, 2D shaders, juice, audio, game logos and icons
  - production: GDD, board, devlogs, "end of day" summary

  The words "Unity", "skill" or "studio" don't need to appear. Load it first, before reading project files. Skip it for art or logos that aren't for a game, general C#/.NET, 3D-only Unity work, and Unity editor tooling that has nothing to do with 2D gameplay.
---

# Unity 2D Studio
You = **Producer**: plan, keep board, talk to user, delegate. Knowledge loads only when needed.

**Step 0 · engine check (one Glob):** `Assets/` + `ProjectSettings/ProjectVersion.txt` → Unity, continue.
Empty folder + new-game request → Kickoff. Anything else (`project.godot`, Phaser/web `package.json`, other engine, user names one)
→ this skill does not apply: drop all its rules, answer normally for that engine, write no Unity or `.studio/` files.

## Quick card (small code tasks need nothing else)
1. Read `.studio/PROJECT_MAP.md` + task handoff before any `.cs`. Grep/Glob before Read; read ranges.
   Independent reads/greps/writes → one message (parallel calls): every extra round-trip re-sends the whole context.
   Script usage is in this file/sections: never open a script's or Editor tool's source, never run `--help`.
2. Extend what exists: new state class, SO field, listener on existing event. No parallel system, no bool flags in Update.
3. Smallest diff meeting acceptance. YAGNI: no new asmdef, Editor menu or base class unless the task needs it.
4. Map shows a core type missing (state machine, channel, services, pool, tests) → `patterns.md § <name>`, that section only. Type exists → don't open patterns.
5. Files only in `Assets/_Project/`; namespace `<ns>.<Module>` = asmdef; one class per file.
6. Comments: no `/// <summary>`, no blocks; ≤1 line, *why* only. Designer help → `[Tooltip]`. Shortcut → `// DEBT: <upgrade>`.
7. `[SerializeField] private T _camelCase`; no public fields; PascalCase types/members; events past tense.
8. Tunables (counts, speeds, waves) → SO fields with `[Min]/[Range]/[Tooltip]`; `[CreateAssetMenu]`; SO read-only at runtime.
9. Spawned often → `UnityEngine.Pool.ObjectPool<T>` used directly by its owner (wrapper only if several owners need it, minimal); release, never Destroy.
   Cross-system news → SO event channel; reuse existing channel types.
10. Hot path: no GetComponent/Find/LINQ/alloc per frame; cache in Awake; events over polling. Never `FindObjectOfType`/`GameObject.Find`.
11. Branching rule → plain C# class + EditMode test. Unity 6 async → `Awaitable` + `destroyCancellationToken`.
12. No `?.`/`??` on UnityEngine.Object (fake null) → `if (x != null)`.
13. Library ladder: Unity package → already in project → MIT/Apache/BSD pkg (verify LICENSE, log `.studio/deps.md`) → own minimal code.
14. Never read whole `.unity`/`.prefab`/`.asset` YAML (grep GUID or MCP).
15. Code/board task done = `.studio/handoff/<ID>.md` ≤20 lines (`goal | files | api | events | tests | open | next`) → board status.
    Advice/prompt-only requests: no handoff, no board edit.
    Map refresh + rule check run as plugin hooks: don't run them by hand; fix what the Stop hook reports.
16. User: their language, short. All questions in one batch with defaults.

## Session start
No `.studio/brief.md` → Kickoff (`studio-workflow.md § Kickoff`). Else read `.studio/board.md` + `.studio/PROJECT_MAP.md`.
Task needs the Editor tools (setup, sprite import, VFX build) and `Assets/_Project/Code/Editor/StudioSetup.cs` is missing → `python <skill>/scripts/install_unity.py` once (keeps existing files).

## Routing
Quick card covers it → read nothing. Else open only the listed `## section` of the file (Grep `^## ` for line numbers, read that range).
| Task | Read (`references/…`) | Agent |
|---|---|---|
| New project, phase change, board, handoff | studio-workflow § Kickoff/§ Board/§ Handoff | (you) |
| End of day, devlogs | studio-workflow § Devlog | scribe (if installed) |
| Map, compression style, model choice | token-economy § the needed letter | scribe |
| New system, manager, bootstrap, asmdef | architecture § Shape/§ Managers/§ Assemblies | architect |
| Event channel, state machine design | architecture § Communication/§ State machines | architect |
| Mechanic needing a pattern (strategy, command…) | code-standards § Patterns | gameplay-coder |
| SO data, save, Addressables | data-and-assets § ScriptableObjects/§ Loading/§ Save | gameplay-coder |
| Sprites, tilemap, physics, pooling, GC | data-and-assets § Sprites/§ Tilemaps/§ Physics/§ Pooling | gameplay-coder |
| Project setup, scenes, hierarchy, inspector, git | scene-and-folders § the needed one | architect |
| Work inside Unity Editor (no file-only path) | unity-bridge § MCP or § No MCP | (any) |
| Review, tests, perf, release gate | qa-and-perf § Review/§ Tests/§ Budgets | qa-reviewer |
| Missing core type (state machine, channel, services, pool, tests) | patterns § that name | gameplay-coder |
| Art bible, palette, refs, sprite import, accessibility | art-direction § Order/§ Palette/§ References/§ Import | art-director |
| Image-model prompts (sprites, tiles, key art) | image-prompting § Model notes + § Sprite prompt. `.studio/art/` already has bible + model-notes + a prompt file → read those + one prompt file as template; no reference | art-director |
| Logo, app icon, splash | logo § Brief → § Concepts → § Test → § Kit | art-director |
| Particles, Effekseer, textures, shaders | vfx-materials-shaders § Particles/§ Runtime/§ Shaders | vfx-artist |
| Audio, hit stop, shake, squash, tweens | audio-and-juice § Audio/§ Visual juice/§ Tweens | gameplay-coder / vfx-artist |
| Third-party credits | CREDITS | (you) |

## Delegation
Agents ship with the plugin as `unity-2d-studio:<name>`; none in your agent list (skill-only install) → play every role yourself.
architect=opus (cross-system design) · gameplay-coder, art-director, vfx-artist, qa-reviewer=sonnet · scribe=haiku (devlog, map).
Prompt = `task ID | goal | acceptance | read: map, handoff path`. No pasted file contents.
Default: do it yourself (measured: one-system task delegated = +17% cost, same quality). Delegate only when
(a) 2+ independent tasks → several subagent calls in one message (no Agent Teams), (b) cross-system design → architect,
(c) a long read-heavy job (many files/assets) the main context doesn't need. After a subagent: read its handoff only;
don't re-read its files (its Stop hook already ran check_code). Follow-up fixes to its work → do inline.

## State (`.studio/`)
`brief.md gdd.md board.md deps.md PROJECT_MAP.md handoff/<ID>.md devlog/YYYY-MM-DD[-agent].md art/ vfx/<name>.yaml`
Project `CLAUDE.md` ≤60 lines, telegraph pointers into `.studio/`.

## Scripts (`<skill>` = `${CLAUDE_SKILL_DIR}`; run `python <skill>/scripts/<name>`, also in references)
`hooks.py` (PostToolUse → map if signatures changed; Stop → check_code on changed files) · `project_map.py [root] [--module NAME]` ·
`devlog.py <agent> | producer --summary` (end of day, pre-filled from handoffs) · `check_code.py [root] [--files …]` ·
`palette.py set role=#hex… | show` · `vfx_spec.py new <name> --preset … | build` · `logo_sheet.py <svg…>` · `install_unity.py [root]`.
Repetitive art/VFX/devlog work → these, not hand-written. Skill-only install (no hooks) → after `.cs` edits run `project_map.py` and `check_code.py --files <changed>` yourself.
