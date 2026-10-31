# unity-2d-studio

Claude Code plugin for Unity 2D projects: clean code, consistent art and low token use. One agent (the "producer") does the work,
reading only the reference section it needs; subagents for end of day (scribe, Haiku), parallel tasks, cross-system design.

## Install (Claude Code plugin: skill + subagents + hooks)
```
/plugin marketplace add polatcangurbuz/unity-2d-studio
/plugin install unity-2d-studio@unity-2d-studio
```
Needs `python` 3.8+ on PATH. Hooks refresh `.studio/PROJECT_MAP.md` after signature changes, mark missing `file:line` refs in
`.studio` notes `(UNVERIFIED …)` and run `check_code.py` on changed `.cs` files at Stop; outside a Unity project they exit at once.
In a Unity project say "start a new 2D game": one batch of questions, Editor tools copied to `Assets/_Project/Code/Editor/`
(never overwrites), then you click **Tools > Studio > Setup Project** (folders, asmdefs, Bootstrap scene, git files).

Skill only: `npx skills add polatcangurbuz/unity-2d-studio -a claude-code` copies just the skill folder to `.claude/skills/`: no
subagents, no hooks (no Haiku scribe; the main agent runs map/rule checks). Evals used subagents + hooks: expect smaller savings.

## What you get
- `.studio/` state: brief, gdd, board, handoffs, devlogs, art bible, VFX specs, `PROJECT_MAP.md` (regex map, no LLM).
- Scripts: `project_map.py`, `check_code.py`, `hooks.py`, `install_unity.py`, `devlog.py` (end of day, pre-filled from handoffs and checked against disk),
  `palette.py` (palette table, contrast + colour-blind board), `vfx_spec.py` (palette-role VFX specs), `logo_sheet.py` (logo tests).
- Unity 6 tested (6000.0.58f2): StudioSetup, SpriteImportRules (PPU/point filter/auto-slice `_sheet_WxH`), VfxBuilder.
- Not a Unity project (no `Assets/` + `ProjectSettings/ProjectVersion.txt`, e.g. Godot)? The skill steps aside.

## Evals (eval suite and workspace not shipped)
Cost with the skill vs without, Claude Opus 5.5, headless `claude -p`, at list prices (platform.claude.com/docs/en/about-claude/pricing,
read 2026-10-30; 1h cache writes counted). e6/e7: v1.0.0 config with subagents installed; others: pre-release builds of 2026-10-30.
| eval | vs no skill | | eval | vs no skill |
|---|---|---|---|---|
| e1 kickoff | **48% cheaper** | | e6 end of day (scribe) | **16% cheaper** |
| e2 double jump | 2% more | | e7 multi-turn dash | 4% more |
| e3 enemy waves | **43% cheaper** | | e8 2nd art task | **18% cheaper** |
| e4 palette + prompts | 30% more | | e9 Godot guard | 4% cheaper |
| e5 explosion VFX | **28% cheaper** | | **all 9** | **18% cheaper ($5.19 vs $6.33)** |
n=1-3 runs each. e4 does one-time work (model notes, art bible, colour-blind board) that e8 reuses. Cost per passed check is
lower on 8 of 9; pass rate 97-98% vs 51-64%. A delegated code task cost 13% more, so code isn't delegated by default (CHANGELOG).
Installed-plugin check, 1 run each: e2 $0.52 (before $0.52-0.53), e6 $0.33 (before $0.32); hooks fired, scribe ran on Haiku.

Credits and licences: [CREDITS.md](CREDITS.md). License: [MIT](LICENSE). Changes: [CHANGELOG.md](CHANGELOG.md).
