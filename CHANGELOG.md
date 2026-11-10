# Changelog

## Unreleased
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
