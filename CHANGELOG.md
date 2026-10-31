# Changelog

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
