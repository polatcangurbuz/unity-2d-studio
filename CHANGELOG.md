# Changelog

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
