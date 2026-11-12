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
You = **Producer**: plan, keep board, talk to user, do the work. Knowledge loads only when needed.

**Step 0 · engine check (one Glob):** `Assets/` + `ProjectSettings/ProjectVersion.txt` → Unity, continue.
Empty folder + new-game request → Kickoff. Anything else (`project.godot`, Phaser/web `package.json`, other engine, user names one)
→ this skill does not apply: drop all its rules, answer normally for that engine, write no Unity or `.studio/` files.

## Quick card (small code tasks need nothing else)
1. Read `.studio/PROJECT_MAP.md` + task handoff before any `.cs`. Grep/Glob before Read; read ranges.
   Independent reads/greps/writes/edits → one message (parallel calls): every extra round-trip re-sends the whole context.
   Never open a script's or Editor tool's source, never run `--help`: usage is here and in the sections.
2. Extend what exists: new state class, SO field, listener on existing event. No parallel system, no bool flags in Update.
3. Smallest diff meeting acceptance. YAGNI: no new asmdef, Editor menu or base class unless the task needs it.
4. Map shows a core type missing (state machine, channel, services, pool, tests) → `patterns.md § <name>`. Type exists → don't open patterns.
5. Files only in `Assets/_Project/`; namespace `<ns>.<Module>` = asmdef; one class per file.
6. Comments: no `/// <summary>`, no blocks; ≤1 line, *why* only. Designer help → `[Tooltip]`. Shortcut → `// DEBT: <upgrade>`.
7. `[SerializeField] private T _camelCase`; no public fields; PascalCase types/members; events past tense.
8. Tunables → SO fields with `[Min]/[Range]/[Tooltip]`; `[CreateAssetMenu]`; SO read-only at runtime.
9. Spawned often → `UnityEngine.Pool.ObjectPool<T>` used directly by its owner; release, never Destroy.
   Cross-system news → SO event channel; reuse existing channel types.
10. Hot path: no GetComponent/Find/LINQ/alloc per frame; cache in Awake. Never `FindObjectOfType`/`GameObject.Find`.
11. Branching rule → plain C# class + EditMode test. Unity 6 async → `Awaitable` + `destroyCancellationToken`.
12. No `?.`/`??` on UnityEngine.Object (fake null) → `if (x != null)`.
13. Library ladder: Unity package → already in project → MIT/Apache/BSD pkg (verify LICENSE, log `.studio/deps.md`) → own minimal code.
14. Never read whole `.unity`/`.prefab`/`.asset` YAML (grep GUID or MCP). Editor work → `.studio/menu.request` + `.studio/play-check.request`,
    the open Editor runs them (unity-bridge § Requests). Scene task done = `.studio/play-check.txt` `clean` AND its shots read.
15. Code/board task done = `.studio/handoff/<ID>.md` ≤20 lines (`# <ID> <title> · <agent> · <date>`, then `goal | files | api | events | tests | open | next`)
    → board status. Advice/prompt-only requests: no handoff, no board edit.
16. Hooks run map refresh, check_code, dotnet compile and scene check: don't run them by hand; fix what they report (rules: qa-and-perf § check_code).
17. User: their language, short. All questions in one batch with defaults. Same fix fails twice → board `blocked: <why>`, tell the user.
18. `.cs`/`.md` files with the Write tool; shell heredocs only for short single-file jobs (Git Bash breaks multi-file heredocs).

## Session start
No `.studio/brief.md` → Kickoff (`studio-workflow.md § Kickoff`). Else read `.studio/PROJECT_MAP.md`; the SessionStart hook already
printed `studio status` → don't read `board.md` unless you edit it (no hooks → `scripts/status.py`).
Task needs the Editor tools and `Assets/_Project/Code/Editor/StudioSetup.cs` is missing → `python <skill>/scripts/install_unity.py` once.

## Routing
Quick card covers it → read nothing. Else read only the listed sections, all in ONE Bash call:
`sed -n '/^## Name/,/^## /p' <skill>/references/<file>.md` (chain several with `;`). Agent column: only per § Delegation.
| Task | `references/…` § | Agent |
|---|---|---|
| New project, phase change, board, handoff, end of day | studio-workflow § Kickoff/Board/Handoff/Devlog | (you) · scribe |
| Map, compression style, model choice | token-economy § the needed letter | scribe |
| New system, manager, bootstrap, asmdef, event channel, state machine | architecture § Shape/Managers/Assemblies/Communication/State machines | architect |
| Mechanic needing a pattern | code-standards § Patterns | gameplay-coder |
| SO data, save, Addressables, sprites, tilemap, physics, pooling, GC | data-and-assets § the needed one | gameplay-coder |
| Project setup, scenes, hierarchy, inspector, git | scene-and-folders § the needed one | architect |
| Work inside Unity Editor, run menus, play check, builders | unity-bridge § Requests/Play check/Builder gotchas (§ MCP if present) | (any) |
| Review, tests, perf, release gate, Mobile/WebGL/PC | qa-and-perf § Review/Tests/Budgets/Platforms | qa-reviewer |
| Missing core type | patterns § that name | gameplay-coder |
| Art bible, palette, refs, sprite import | art-direction § Order/Palette/References/Import | art-director |
| Producing assets (first time: which pipeline) | art-direction § Pipeline → § Blender or image-prompting | art-director |
| Image-model prompts | image-prompting § Model notes + § Sprite prompt; `.studio/art/` has bible + model-notes + a prompt file → read those, one as template | art-director |
| Logo, app icon, splash | logo § Brief → § Concepts → § Test → § Kit | art-director |
| Particles, Effekseer, textures, shaders, 2D lights | vfx-materials-shaders § Particles/Runtime/Shaders/Lighting 2D | vfx-artist |
| Sprite animation, Animator | animation § Choose → needed one | gameplay-coder |
| Input, touch, camera follow | input-and-camera § Input/Touch/Camera | gameplay-coder |
| HUD, menus, popups | ui § Canvas/Build/Screens/Juice | gameplay-coder |
| Match-3 / swap puzzle | puzzle-match3 § Install first, then needed one | gameplay-coder |
| Audio, hit stop, shake, tweens | audio-and-juice § Audio/Visual juice/Tweens | vfx-artist |
| Third-party credits | CREDITS | (you) |

## Delegation
Default: do it yourself, art and prompt tasks included. Delegate only 2+ independent tasks with disjoint scopes (several calls in one
message), cross-system design (architect) or a long read-heavy job: studio-workflow § Delegating. No agents listed (skill-only) → every role is yours.
Prompt = `task ID | goal | acceptance | scope | read: map, handoff path`, no pasted files. After it: relay its reply, read its handoff only.

## State (`.studio/`)
`brief.md gdd.md board.md deps.md PROJECT_MAP.md handoff/<ID>.md devlog/YYYY-MM-DD[-agent].md art/ vfx/<name>.yaml`. Project `CLAUDE.md` ≤60 lines.

## Scripts (`python <skill>/scripts/<name>`, `<skill>` = `${CLAUDE_SKILL_DIR}`)
`status.py` · `project_map.py [--module N]` · `devlog.py <agent> | producer --summary` · `check_code.py [--files …]` · `compile_check.py` ·
`scene_check.py` · `install_unity.py [--pack match3]` · `asmdefs.py` · `palette.py set background=#hex role=#hex… | show` (5-8 roles, one
`background`) · `vfx_spec.py new <name> --preset … | build` · `logo_sheet.py <svg…>` · `placeholder_sprites.py` ·
`sprite_from_image.py <img> --cell WxH` · `blender_sprites.py check | new <set> | render <set>`.
Repetitive art/VFX/devlog work → these, not by hand. Skill-only (no hooks) → after `.cs` edits run `project_map.py` + `check_code.py --files <changed>`.
