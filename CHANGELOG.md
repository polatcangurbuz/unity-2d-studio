# Changelog

## v1.3.0 · 2026-11-12
From a comparison with other Unity agent skills/plugins (official Unity skills, Unity-MCP, game-studio templates): play-time
verification, 2D topics and a genre pack for polished mobile puzzle games.
- Play check (`StudioPlayCheck`, Editor): plays a scene N s, writes `.studio/play-check.txt` (verdict `clean | errors N | check watch |
  aborted`, project errors with first `Assets/` frame, Unity/package errors counted apart, fps/worst frame/GC, watched objects start→end,
  out-of-view) + ≤4 shots (`.studio/shots/`, Camera.main + overlay UI, ≤960 px, 9:16 for portrait games). Works with Unity in the
  background and in batch (`-executeMethod Studio.EditorTools.StudioPlayCheck.RunBatch -studioCheck "…"`, exit 0/1).
- Requests (`StudioRequests`): the open Editor polls `.studio/menu.request` (only `Tools/…` menus) and `.studio/play-check.request`;
  resolves packages after a `manifest.json` change, refreshes + waits for compile, answers `# compile failed` on errors. No more
  "click this menu" / "press Play" round-trips. Quick card: scene task done = clean play check + shots read.
- Console bridge: play lines reset on ExitingEditMode, so Awake/OnEnable errors of the new Play are kept.
- Builder helpers: `StudioAnimUtil` (sheet → flipbook clip, Animator states/params/transitions), `StudioUiUtil` (canvas, safe area,
  panels, TMP/legacy labels, buttons, fill bars, TMP Essentials import), `StudioLightUtil` (global/point Light2D on all sorting layers,
  shadow casters), `StudioCameraUtil` (Cinemachine 3 2D follow + confiner + impulse; compiled only when Cinemachine is installed),
  `StudioPlatforms` (Mobile Portrait/Landscape, WebGL, PC presets → `.studio/platform.md`, never switches build target).
- asmdefs: Editor defines `STUDIO_UGUI/TMP/URP/CINEMACHINE/TEST_RUNNER` (merged into existing asmdefs), refs for Cinemachine 3 and
  2D Animation; packages read from `packages-lock.json` too (dependencies of feature packages count).
- `sprite_from_image.py`: generated image → key removal, frame split, one-scale block-majority downscale, bottom-centre, palette
  mapping with drift report, optional outline.
- match3 pack (`install_unity.py --pack match3`): pure C# deterministic core (specials, combos, crates/ice/chains/holes, boosters,
  shuffle, goals; 84 NUnit tests also run on plain .NET), greedy/random bots + simulator for pass-rate curves, 3 example levels;
  view (step player with beat timing, pooled pieces, tweens, swipe input, juice, HUD, safe area) and a scene builder with
  procedural placeholder art (one shape per colour). Verified in Unity 6000.0.58f2: scene builds and plays clean.
- References: animation, input-and-camera, ui, puzzle-match3 (install, feel, difficulty targets, meta/economy, live ops, pro-look
  checklist); vfx § Lighting 2D, qa § Platforms, unity-bridge § Requests/§ Play check, image-prompting § After generation,
  audio-and-juice polish bar; Cinemachine only when already installed or the user agrees.
- Hooks start through `hooks/run.sh` (python3 → python → py, version-checked: works where only `python3` exists and skips
  Windows' Store stub).
- gitignore: play-check output, shots, request/result files, platform notes.
- Art pipeline is chosen at the first asset task, not at kickoff (art-direction § Pipeline): the skill detects Blender and image-model
  MCP tools, offers only what works (`prompt` = current path, `blender`, `image-mcp`) with cost/quality notes, records `art-pipeline:`
  in the brief and never asks again. All paths: style anchor first, batch after approval, one preview board, no re-renders of approved assets.
- `blender_sprites.py check | new <set> | render <set>`: headless Blender (no MCP needed) renders a set from a JSON spec with one
  light/camera/material rig: faceted gems (round or 3-8 sides, palette roles or hex) or imported models (.blend/.glb/.fbx/.obj) with
  yaw directions and animation frames → `<name>_sheet_<W>x<H>.png`, soft ground shadow, ONE preview board for review.
  Verified with Blender 5.2: 6 gems in ~9 s, a 4-frame model sheet in ~6 s; gems dropped into the match-3 scene play clean.
- image-prompting § MCP: style anchor at draft settings, reference image on every call, credit cap asked once, licence logged.
- README rewritten for first-time visitors (why, quick start, pipelines, numbers; technical details folded).
- Cheaper runs, from the v1.3.0 eval traces:
  - SKILL.md is 27% smaller. Delegation, builder and check_code details moved to the references that already own them.
  - Sections are read in one call (`sed -n '/^## Name/,/^## /p'`) instead of grep plus a ranged read.
  - Prompt-only art tasks stay with the producer, who relays a subagent's reply instead of re-reading its files.
- `palette.py set` reports every problem at once. It accepts `_` and capitals in role names. One trace spent 3 extra round-trips here.
- `check_code`: Unity's empty template `SampleScene` is reported as pending Setup Project, not as an issue. Setup deletes it.
  A SampleScene holding user objects is still flagged.
- image-prompting: concept/key-pose prompts state `single sprite, 1 frame`. The frame count must be in the prompt text, not only its heading.
- Bug and feature issue templates.
- Evals re-run (plugin vs plain Claude Code, Opus 5.5, `total_cost_usd`, 2026-11-12; plain = mean of 2 runs):
  - All 9 tasks: $5.90 vs $7.83 (−25%).
  - Script-decided checks: 44/44 vs 23/44.
  - Before the fixes above: $6.90, with e2 +25%, e4 +28% and e5 +10%.
  - Now every task is cheaper except e9 (Godot guard, +8%, $0.03, noise).
  - The grader now counts subagents' web calls and uses the release scripts.

## v1.2.0 · 2026-11-10
From a session where a builder saved an empty tilemap composite and the error was only found after the user reported it.
- `StudioEditorUtil.BakeTilemapColliders(tilemap, composite)`: static body → TilemapCollider2D (Merge) → `ProcessTilemapChanges` →
  CompositeCollider2D → `GenerateGeometry`; LogError on no tiles / no paths. unity-bridge: helper list + one gotcha line.
- `scripts/scene_check.py` (regex, no Unity): empty CompositeCollider2D, unassigned `_camelCase` refs in project scripts,
  Sprite-Lit material in a scene without Light2D. Stop hook runs it on `.unity`/`.prefab` saved since the previous Stop.
- Quick card: scene-building task not done before one Play with a clean `console.log`; unclear runtime bug → `[DIAG]` LogError + one Play first.
- gitignore: hook state (`.pending_check*`, `.scene_stamp`) and bridge output (`console.log`, `test-results.txt`).
- Verified in Unity 6000.0.58f2 batch: colliders-before-tiles without bake → `m_Paths: []` (flagged), same order + bake → 6 paths;
  tilemap with no tiles → 2 lines in `console.log`; scene_check found all 3 issue types; hook reports once, silent next Stop.

From an audit of the multi-agent setup against current Claude Code docs and other subagent systems (Anthropic harness/
code-review posts and plugins, Claude-Code-Game-Studios, ccpm, ralph-loop).
- Fix: hook state is per agent (`agent_id`). Before, parallel subagents shared `.studio/.pending_check`: the first to stop got
  the other's (and the producer's) violations and cleared them, so the real author never saw them. Scene check per agent too.
- Fix: hook output forced to UTF-8 (Windows codepage garbled `·`/`≤`); any hook exception → `.studio/hook-errors.log`, exit 0.
- Fix: qa-reviewer read Console via the user; now `.studio/console.log` / `test-results.txt` (or MCP), stale or missing = `unverified`.
- Only the producer writes `board.md` (parallel agents editing it collided); agents return status as the reply's first field.
- qa-reviewer is an independent checker: never edits code (`disallowedTools: Agent, Write, NotebookEdit`), evidence over the
  handoff's claims, only real defects with `file:line`, verdict `pass|fail|unverified`.
- All agents: `disallowedTools: Agent` (no nested spawns), `maxTurns`, `color`; edit only the prompt's `scope:`; same fix fails
  twice → `blocked | <why>`. Producer: `scope:` in every delegation, parallel only with disjoint scopes, no worktrees (Library
  re-import), partial result → SendMessage, not respawn. architect gets the handoff template inline and plans at deliverable level.
- `scripts/status.py` (no LLM): open board rows, console/test state, UNVERIFIED handoffs, hook errors. SessionStart hook prints it,
  so the producer no longer reads `board.md` at session start.
- SubagentStart/SubagentStop: a studio subagent (not scribe) that wrote no handoff is asked for one, once.
- Commands (plugin install): `/unity-2d-studio:status`, `/unity-2d-studio:review [ID]`, `/unity-2d-studio:end-of-day [date]`.
- `tests/test_scripts.py` (stdlib unittest: check_code, scene_check, hooks incl. the parallel race, status, project_map, plugin
  files) + GitHub Actions (Ubuntu/Windows, Python 3.8/3.12, `claude plugin validate --strict`).
- Not adopted, on purpose: more agents, Agent Teams, worktrees, a separate `features.json` (board acceptance covers it).
- Not re-measured yet: e1-e9 evals with these changes.

From a real plugin run (local `--plugin-dir`, headless): one-screen vertical slice in a fresh Unity 6000.0.58 URP (3D template)
project, gameplay-coder + vfx-artist in parallel, then `/unity-2d-studio:review` with 3 parallel qa-reviewers.
Confirmed: hook input carries `agent_id`/`agent_type`; per-agent state kept parallel reports apart; only the producer wrote the
board; agents stayed in scope and wrote handoffs; qa never edited code. Found and fixed:
- 18 compile errors after the first Unity focus: builders lacked `using Studio.EditorTools;` (no reference named the namespace)
  and nothing compiled before "done". New `scripts/compile_check.py`: dotnet build of Unity's generated `.csproj` for the changed
  files' assemblies (Stop hook; ~1.5 s incremental; skipped without dotnet/.csproj or with `STUDIO_DOTNET_CHECK=0`), and
  `--editor-log`: last compile's errors from Unity's own Editor.log (found the same 18). check_code `missing-using` rule.
- `console.log` never appeared: the bridge came in the same first compile as the broken builders, and Unity loads no new code
  after a failed compile. status.py now falls back to Editor.log.
- Obstacles never spawned, no VFX/shake: the builder loaded assets before `NewScene(Single)`, which unloaded them, and `Wire`
  saved null without a word. `StudioEditorUtil.BuildScene(path, scene => …)` fixes the order; `Wire` logs null/destroyed values;
  scene_check calls 2+ empty refs on one component a wiring bug instead of "optional? ignore". Quick card carries both rules.
- Scenes the user built between sessions went unreported: SessionStart refreshes the map (scenes are now in its hash) and
  runs scene_check on scenes saved since the last stop; the Stop hook refreshes the map when Unity saved scenes.
- qa failed a task on a stale play error: `console.log` lines are `[HH:mm:ss]`-stamped, each Play replaces the previous Play's
  lines (`play start`), `BuildScene` writes `mark build <scene>`, Unity package errors are `package-…` and counted apart.
- `VfxBuilder` used legacy particle shaders (invisible in URP): URP `Particles/Unlit` (alpha/additive) first, legacy as fallback.
- Handoffs without `# <ID> <title> · <agent> · <date>` dropped out of end of day: agents get the header line, the SubagentStop
  hook checks it once, devlog.py falls back to the board owner and finds bare file names by name.
- VFX blocked without an art bible: `vfx_spec.py` uses the placeholder roles `(default, unconfirmed)`.
- Producer finished vfx-artist's blocked task but left its handoff "blocked": rule to update it; status.py flags such handoffs.
- `/review` with ≤3 tasks and ≤15 files → one qa-reviewer call instead of one per task.
- check_code: Unity 6 URP template `Assets/Settings/`, `TutorialInfo/`, `Readme.asset` no longer flagged.
- Run cost: build $2.32; review run reported $3.12 (3 qa-reviewers).
- Re-run of `/unity-2d-studio:review` in a fresh session after these fixes: ONE qa-reviewer call for 3 tasks, $0.49 (was $3.12).
  Verdicts matched the evidence: T-004 pass (no error after `play start`), T-002 unverified (tests never ran), T-003 unverified
  (no runtime proof) + the stale handoff that SessionStart status flagged. Two follow-ups: console.log's header keeps the last
  compile result (`play · compile ok`) instead of only the last event, and status.py counts untagged `Packages/`-only lines
  (older bridge) as package errors.

## v1.1.0 · 2026-10-30
From a real no-MCP session (Unity 6000.0.58, URP 17 2D, Windows): fewer error turns, less hand-written code.
- Asmdefs get package references from `Packages/manifest.json`: Input System (Gameplay, UI), uGUI + TMP (UI, Editor),
  URP Core/Universal/2D runtime (Gameplay, Editor), test runner (Editor). Existing asmdefs only gain missing refs.
  Asmdef code moved to `StudioAsmdefs.cs`; new `scripts/asmdefs.py` writes the same asmdefs before the Setup click.
  scene-and-folders § Setup: type → asmdef reference table.
- `StudioConsoleBridge.cs`: compile errors and Error/Exception logs (play mode too) → `.studio/console.log` (reset each compile and
  editor launch, repeats collapsed, caller `file:line`); Test Runner summary → `.studio/test-results.txt`. Agents read these, never ask for pastes.
- `StudioEditorUtil.cs`: `Wire`, `LoadOrCreate<T>`, `EnsureFolder`, `EnsureLayer`, `AddToBuildSettings`, `Separator`, `NewActor`,
  `SavePrefab`, `OpenOrCreateScene`. unity-bridge § Builder gotchas (NewScene unloads assets, composite + static body,
  `compositeOperation`, Global Light2D, URP PixelPerfectCamera, `linearVelocity`).
- `scripts/placeholder_sprites.py`: 16x16 placeholder PNGs (stdlib only) from `.studio/art/placeholders.yaml`, palette roles from
  the art bible; default set of 7 (player, walker, flyer, coin, ground top/fill, flag). Never overwrites without `--force`.
- Kickoff: a full-scope request (scope + style + PPU) skips the questions; defaults marked `(default, unconfirmed)`, listed once at the end.
- Quick card: all check_code rules + split big builders; create files with the Write tool, not multi-file heredocs.
- patterns § Tests: derive tick counts for timed rules from the constant.
- check_code: URP root assets (`UniversalRenderPipelineGlobalSettings.asset`, `DefaultVolumeProfile.asset`) and Unity 6's
  `InputSystem_Actions.inputactions` are no longer flagged as outside `_Project`.
- Verified in Unity 6000.0.58f2 batch (URP 17.0.4, Input System 1.11.2): InputSystem/UI.Text/TMP/Light2D/URP PixelPerfectCamera
  code compiles with no hand-added refs; a 55-line test builder exercised every helper; compile error → `console.log` in 1.6 s;
  test results file written; play-mode NullReferenceException → `console.log` with `file:line`; sprites import at 16 PPU, Point, uncompressed.

## v1.0.0 · 2026-10-30
First public release.
- Claude Code plugin + single-plugin marketplace in one repo: `.claude-plugin/{plugin,marketplace}.json`, `skills/unity-2d-studio/`,
  `agents/` (namespaced `unity-2d-studio:<name>`), `hooks/hooks.json` (`${CLAUDE_PLUGIN_ROOT}` paths). Install: `/plugin marketplace add`
  + `/plugin install`. Skill-only fallback: `npx skills add` (skill folder only: no subagents, no hooks).
- Scripts resolve from their own folder (`${CLAUDE_SKILL_DIR}` in the skill, `${CLAUDE_PLUGIN_ROOT}` in agents/hooks), write only
  into the project, and no `__pycache__` into the plugin cache. `install_unity.py` copies the Editor tools (+ git templates in
  `Templates~/`, read by StudioSetup) into `Assets/_Project/Code/Editor/` at kickoff, never overwriting.
- Verified as an installed plugin (local marketplace, `claude -p`): e2 $0.519 vs $0.523-0.533 before, e6 $0.326 vs $0.322;
  hooks fired, end of day went to `unity-2d-studio:scribe` (Haiku, $0.005), same checks passed.
- Router `SKILL.md` (under 90 lines) with engine check: works only in Unity projects, steps aside elsewhere (e.g. Godot).
- 15 references read one `##` section at a time: studio workflow, token economy, code standards, patterns, architecture,
  data and assets, scenes and folders, Unity bridge, QA and perf, art direction, image prompting, logo, VFX/materials/shaders,
  audio and juice, credits.
- Single agent by default. Optional subagents: architect (opus), gameplay-coder, art-director, vfx-artist, qa-reviewer (sonnet),
  scribe (haiku, handles end of day). Delegation only for end of day, 2+ independent tasks, cross-system design, read-heavy jobs.
- Scripts: `project_map.py`, `check_code.py`, `hooks.py`, `install_unity.py`, `devlog.py` (handoff pre-fill, UNVERIFIED file/API/`file:line` marking),
  `palette.py`, `vfx_spec.py`, `logo_sheet.py`.
- Hooks: PostToolUse map refresh + note reference check; Stop rule check on changed `.cs` files.
- Unity Editor tools (Unity 6): StudioSetup, SpriteImportRules, VfxBuilder.
- Evals: 9 scenarios; 18% cheaper than no skill overall at list prices, lower cost per passed check on 8 of 9;
  end of day via scribe 24% cheaper than single agent.

### Known issues
- Multi-turn code tasks (e7) cost about the same as no skill (+4%, 1 run). Same reads and code size as cheaper runs;
  the extra is output/thinking tokens (turn 2: 8.6k vs 4.5-5k), i.e. model variance, not hooks or file reads. No fix found.
- Delegating a single code task to a subagent costs ~13% more than doing it in the main agent; hence not delegated by default.
