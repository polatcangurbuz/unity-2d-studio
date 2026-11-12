# Unity 2D Studio

![version](https://img.shields.io/badge/version-1.3.0-blue)
![license](https://img.shields.io/badge/license-MIT-green)
![unity](https://img.shields.io/badge/Unity-6%20%2B%20URP%2017-black)

**A Claude Code plugin that runs a small 2D game studio inside your Unity project.**
You talk to one producer; it plans, writes clean C#, builds scenes, **plays them to check its own work**, keeps the art consistent
and writes the devlog. It reads only what a task needs, so it does more for less money than plain Claude Code.

```
You:    "Start a new 2D match-3 game, portrait, mobile."
Studio: one batch of questions → folders, asmdefs, scenes, board → builds the level → plays it in the Editor →
        "play check clean, 60 fps, 0 GC/frame" + screenshots it actually looked at
```

## Why

Claude is a good programmer but it is blind in Unity. It can't see the Console, can't press Play and can't tell whether the
sprite it asked for matches the other forty. Unity 2D Studio closes those gaps:

| Problem | What the studio does |
|---|---|
| Claude can't see compile or runtime errors | Editor bridge writes them to `.studio/console.log` with the `file:line` that caused them |
| "It compiles" isn't "it works" | **Play check**: the Editor plays the scene for N seconds and reports errors, fps, GC and up to 4 screenshots |
| "Now click Tools > … and press Play" | The open Editor runs `Tools/…` builders and play checks on request: no clicks |
| Big projects eat the context | A regex project map + one reference section per task, not whole folders |
| Every sprite looks different | Palette roles, art bible, a style anchor approved first, one preview board per set |
| Code drifts into a mess | Rules (asmdefs, no `public` fields, no `Find` in `Update`, pooling…) checked by hooks on every change |
| Sessions forget what happened | Board, handoffs and an end-of-day devlog in `.studio/`, checked against the files on disk |

## Quick start

Needs **Claude Code**, **Unity 6** (URP 17 tested) and **Python 3.8+**. Optional: the .NET SDK (compiles changed C# before
you switch to Unity) and **Blender** (free 3D-rendered sprites).

```
/plugin marketplace add polatcangurbuz/unity-2d-studio
/plugin install unity-2d-studio@unity-2d-studio
```

Open Claude Code in your Unity project folder and just talk; English or Turkish both work:

- `Start a new 2D platformer` → one batch of questions, then setup (folders, asmdefs, Bootstrap scene, git files)
- `Add a double jump` · `Build an enemy wave system` · `Add an explosion effect`
- `Make a palette and sprite prompts, I'll use Midjourney` · `Render the gem sprites`
- `End of day` → devlog from the day's handoffs

Commands: `/unity-2d-studio:status`, `/unity-2d-studio:review [ID]`, `/unity-2d-studio:end-of-day`.

## What's inside

**A studio, not a prompt.** One producer does the work and reads one reference section per task. Subagents are used only where
they pay off: the scribe (Haiku) for end of day, parallel independent tasks and cross-system design. Roles: architect,
gameplay coder, art director, VFX artist, QA reviewer, scribe.

**Sees and plays Unity.** The console bridge records errors. The play check runs in the open Editor, even in the background, or in
batch mode, and writes a verdict (`clean | errors N | check watch`), fps, worst frame, GC and the start→end state of watched objects.
A scene task counts as done only after a clean play check and a look at the shots.

**Builder helpers for real 2D games.** Flipbook animation and Animators, uGUI/TMP HUDs with safe area, Light2D, Cinemachine 2D
follow (only if you already use it), tilemap colliders, and Mobile, WebGL and PC presets. Input System only.

**Art you can keep consistent.** At the first art task the studio checks what you have and asks once which pipeline to use:

| Pipeline | Best for | Cost |
|---|---|---|
| `prompt` | anything; you generate in your own tool, `sprite_from_image.py` cleans it into a palette-mapped sheet | lowest tokens |
| `blender` | gems, props, items, icons, isometric sets: same light, scale and shadow on every sprite | free, no API key |
| `image-mcp` | characters, backgrounds, painted or pixel styles (Gemini, PixelLab…) | service credits, with a cap |

Every pipeline starts the same way: approve a 1-3 asset style anchor, batch the rest, review one preview board and
re-render only what you rejected.

**Match-3 pack.** `install_unity.py --pack match3` adds:
- A deterministic core: specials, combos, crates, ice, chains, holes, boosters and goals, covered by 84 tests.
- Bots and a simulator for difficulty curves.
- An animated board view with juice.
- A one-click portrait scene.
- References for difficulty targets, meta, economy and live ops.

## Does it actually save money?

Yes, and the work is better. Nine everyday tasks were run with v1.3.0 and with plain Claude Code. Both used Claude Opus 5.5
as headless `claude -p` sessions on the same fixture projects. Cost is Claude Code's own `total_cost_usd` at list prices; the
plain column is the mean of two runs. Measured 2026-11-12.

| Task | Plugin | Plain | Cost | Checks passed (plugin vs plain) |
|---|---|---|---|---|
| Kickoff a new platformer | $0.38 | $1.01 | **−63%** | 5/5 vs 2/5 |
| Double jump | $0.53 | $0.57 | −6% | 5/5 vs 2/5 |
| Enemy wave system | $0.57 | $0.87 | **−35%** | 5/5 vs 2/5 |
| Palette + sprite prompts (first art task) | $0.55 | $0.75 | **−27%** | 5/5 vs 1/5 |
| Explosion VFX | $0.58 | $0.74 | **−21%** | 7/7 vs 6/7 |
| End of day (Haiku scribe) | $0.30 | $0.44 | **−32%** | 3/3 vs 0/3 |
| Dash, 3-turn session | $2.18 | $2.57 | **−15%** | 6/6 vs 3/6 |
| Second art task (reuses the first) | $0.43 | $0.52 | **−17%** | 5/5 vs 4/5 |
| Godot project (plugin must step aside) | $0.39 | $0.36 | +8% | 3/3 vs 3/3 |
| **All 9** | **$5.90** | **$7.83** | **−25%** | **44/44 vs 23/44** |

The plugin also writes handoffs, board updates and tests that plain runs skip, and still costs less. The Godot row is within run-to-run
noise: there the plugin only checks the engine and steps aside.

The checks are the ones a script can decide: rule check, map-first, palette roles, devlog files, prompt syntax and so on. Code-shape
checks such as "uses the existing state machine" are left out. Plugin runs are n=1 per task, and the eval suite isn't shipped.

<details>
<summary><b>Technical details</b>: hooks, scripts, editor tools, state files</summary>

### Hooks (plugin install)
- Refresh `.studio/PROJECT_MAP.md` after signature changes.
- Run `check_code.py` on changed `.cs` files.
- Run `scene_check.py` on scenes Unity saved; without Unity it catches empty composites and unassigned refs.
- With `dotnet`, compile changed C# at Stop.
- Mark `file:line` refs in `.studio` notes that no longer exist as `(UNVERIFIED …)`.
- Print a short status at session start.
- Ask a subagent for its missing handoff once.

Reports are per agent, so parallel subagents don't mix them. Outside a Unity project (no `Assets/` +
`ProjectSettings/ProjectVersion.txt`, e.g. Godot) the hooks and the skill step aside. Hooks start through `hooks/run.sh`
(`python3`, then `python`, then `py`, version-checked).

### Editor tools (copied to `Assets/_Project/Code/Editor/`, never overwritten)
`StudioSetup` (Tools > Studio > Setup Project) · `StudioConsoleBridge` · `StudioPlayCheck` · `StudioRequests`
(`.studio/menu.request`, `.studio/play-check.request`) · `StudioEditorUtil` · `StudioAnimUtil` · `StudioUiUtil` · `StudioLightUtil` ·
`StudioCameraUtil` (only with Cinemachine 3) · `StudioPlatforms` · `StudioAsmdefs` · `SpriteImportRules` · `VfxBuilder`.

### Scripts (stdlib Python)
| Script | Does |
|---|---|
| `project_map.py` | regex code map: types, members, most-referenced types (no LLM) |
| `check_code.py` · `scene_check.py` · `compile_check.py` | rule check, scene YAML check, dotnet build of Unity's .csproj |
| `install_unity.py` · `asmdefs.py` | copy Editor tools and packs; asmdefs with package refs |
| `palette.py` · `vfx_spec.py` · `logo_sheet.py` | palette table + contrast + colour-blind board, VFX specs by palette role, logo tests |
| `placeholder_sprites.py` · `sprite_from_image.py` · `blender_sprites.py` | placeholder art, generated image → clean sheet, headless Blender sets |
| `devlog.py` · `status.py` · `hooks.py` | end of day pre-filled from handoffs, status, hook entry point |

### State in `.studio/`
brief, gdd, board, handoffs, devlogs, art bible, palette, VFX specs, `PROJECT_MAP.md`, `console.log`, `play-check.txt` + shots.

### Skill only (no subagents, no hooks)
`npx skills add polatcangurbuz/unity-2d-studio -a claude-code` copies only the skill to `.claude/skills/`. The main agent runs
the map and rule checks itself, so savings are smaller than the numbers above.

### Tests
`python -m unittest discover -s tests` (stdlib only). CI runs them on Ubuntu + Windows, Python 3.8 + 3.12, plus
`claude plugin validate --strict`.
</details>

## Limits
- Tested on Unity 6 (6000.0.58f2) with URP 17. Older versions or the built-in pipeline may need small fixes.
- 2D only; 3D-only work, non-game C# and other engines are out of scope.
- The plugin gives you structure, checks and consistency, not finished art. Blender sprites look clean and solid but are not
  hand-painted AAA art; an image MCP or an artist still makes the difference for hero assets.

Credits and licences: [CREDITS.md](CREDITS.md) (ideas only, no copied code or prompts). License: [MIT](LICENSE).
Changes: [CHANGELOG.md](CHANGELOG.md).
