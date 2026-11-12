When to read: producer starting a project, changing phase, assigning work, closing the day. Read only the needed `##` section.
Sections: Phases · Kickoff · Pre-production · Roles · Board · Delegating · Handoff · Devlog · Release

# Studio workflow

## Phases
Each phase ends with a short user summary + go-ahead. Never skip ahead.
| # | Phase | Output (`.studio/`) | Exit |
|---|---|---|---|
| 0 | Kickoff | `brief.md` ≤30 lines | batched questions answered (or defaults accepted) |
| 1 | Pre-production | `gdd.md` ≤1 page + § Tech, `art/art-bible.md`, seeded `board.md` | user approves loop + art |
| 2 | Vertical slice | one playable polished level | start → win/lose, no errors |
| 3 | Production | board tasks in order | no `doing` without owner |
| 4 | Polish | VFX, audio, juice, UI transitions | qa pass, 0 GC in gameplay |
| 5 | Release | build, profiling, checklist | qa-reviewer sign-off |

## Kickoff
Request already gives scope, art style and PPU (e.g. "vertical slice: one level, run/jump/coins, pixel 16 PPU") → ask nothing:
fill the gaps with defaults marked `(default, unconfirmed)`, write `brief.md` + `gdd.md`, go straight to the slice,
and list every assumption in ONE block in the final message. Otherwise one batched message, user's language, defaults so "ok" works:
1. Genre + one-line pitch
2. Art style: pixel / hand-drawn / vector (+ resolution, PPU)
3. Target platform(s)
4. Unity version (detect from `ProjectSettings/ProjectVersion.txt`, confirm; 6 → `Awaitable`, older → coroutines)
5. Art pipeline: asked at the first art task (art-direction § Pipeline), not here; brief gets `art-pipeline: (ask at first art task)`
6. 2-3 reference games (principles only: palette structure, readability, UI layout, juice; never assets/designs)
7. Company/game name → namespace `<Company>.<Game>.<Module>`
8. MCP for Unity installed?
Write `brief.md` right away with defaults marked `(default, unconfirmed)`:
```
# Brief · <Game>
pitch: ... | genre: ... | style: pixel 16 PPU | platform: PC
unity: 6000.x | ns: Acme.Skyhop
art-pipeline: prompt | blender | image-mcp | image-model: <name> | commercial: yes/no/unknown
refs: <game> (why) · <game> (why)
bridge: mcp | menu-scripts
```
Then: `python <skill>/scripts/install_unity.py` (Editor tools → `Assets/_Project/Code/Editor/`, keeps existing) · `asmdefs.py` (code needn't wait for the click)
· **Tools > Studio > Setup Project** via `.studio/menu.request` (editor open; else ask for one click) (folders, asmdefs, Bootstrap scene, SampleScene move, git files) · seed `board.md` · run `project_map.py` once.
Don't write gameplay code at kickoff unless the request was full-scope (above).
Full-scope kickoff with no art bible yet: `placeholder_sprites.py` and `vfx_spec.py` use the placeholder palette roles
(`background midground interactive hazard reward outline`, marked `(default, unconfirmed)`); art-director replaces them later with `palette.py set`.

## Pre-production
`gdd.md`: pitch, core loop (verb → reward → escalation), 3-6 mechanics, win/lose, controls, slice scope.
Tech plan (architect) → `gdd.md` § Tech. Art bible (art-director). Seed board with slice tasks.

## Roles
| Role | Agent | Reads (only) |
|---|---|---|
| Producer | main | this file |
| Tech lead | architect | architecture, code-standards |
| Gameplay | gameplay-coder | quick card; code-standards/data-and-assets sections on demand |
| Art director | art-director | art-direction, image-prompting, logo |
| VFX/tech art | vfx-artist | vfx-materials-shaders (+ audio-and-juice § Visual juice) |
| QA | qa-reviewer | qa-and-perf § Review |
| Scribe | scribe | token-economy § a |
Juice split: gameplay-coder = AudioManager, mixer, SO sound defs, hit-stop timing. vfx-artist = hit flash, shake, squash/stretch, particle triggers (react to events).
Cheapest capable role; architect only for cross-system decisions.
Agent Teams are not used. Parallel work → several subagent calls in the same message.

## Board
```
| ID | task | owner | status | acceptance |
| T-007 | double jump | gameplay-coder | review | 2nd jump in air only; resets on ground; test passes |
```
`todo` → `doing` → `review` → `done` | `blocked: <reason>`. Acceptance = one testable line, else not assignable.
Only the producer writes `board.md` (parallel agents editing one file collide); agents report status as the first field of
their reply (`review|blocked`, qa: `pass|fail|unverified`). Keep open + last 10 done; older → `board-archive.md`.

## Delegating
Default: do it yourself (measured: one-system task delegated = +17% cost; a delegated prompt-only art task = +28%). Delegate only
(a) 2+ independent tasks with disjoint scopes → several subagent calls in one message, (b) cross-system design → architect,
(c) a long read-heavy job (many files/assets) the main context doesn't need. Models: architect=opus · gameplay-coder, art-director,
vfx-artist, qa-reviewer=sonnet · scribe=haiku. After a subagent: relay its reply, read its handoff only, never re-read its files
(its Stop hook already ran check_code). You alone write `board.md`: status from the reply's first field (`review|blocked`;
qa: `pass`→done, `fail`→doing, `unverified`→ask user for one Unity switch/test run). Partial result (turn limit) → continue that
agent with SendMessage, don't respawn. Follow-up fixes to its work → do inline.
`task: T-007 | goal | acceptance | scope: Code/Gameplay/Player/ | read: .studio/PROJECT_MAP.md, .studio/handoff/T-005.md`. No pasted code.
Parallel only with disjoint scopes (no shared scene, prefab or SO asset); no git worktrees (Unity re-imports Library per copy).
Agents stop after the same fix fails twice and reply `blocked | <why>` → board `blocked: <why>`, ask the user.

## Handoff
On task finish write `.studio/handoff/<ID>.md`, ≤20 lines, telegraph:
```
# T-007 double jump · gameplay-coder · 2026-10-29
goal: 2nd jump midair, reset on ground
files: Code/Gameplay/Player/PlayerMotor.cs (+18 -4)
api: void PlayerMotor.TryJump() · int PlayerStats.MaxAirJumps
events: raises PlayerJumped (VoidEventChannelSO)
tests: Tests/EditMode/PlayerMotorTests.DoubleJump_ResetsOnGround
open: coyote time not handled
next: qa-reviewer → review T-007
```
Follow-up on same task (fix/extend) → update the same handoff, keep ≤20 lines.
Then reply with status → stop (hooks refresh the map and run check_code; a studio subagent without a handoff is asked once). Next agent reads handoff + map first, source only if it must.

## Devlog
- Agent at session end / `/devlog`: `python <skill>/scripts/devlog.py <agent>` → fill done/decisions/next/risks, ≤12 lines.
- End of day ("gün sonu", "end of day"), scribe agent available (`unity-2d-studio:scribe`): ONE call `scribe: end of day YYYY-MM-DD, reply in <user language>`.
  Read nothing yourself; relay scribe's reply to the user as is (it already lists UNVERIFIED items). No scribe → do the steps below inline.
- Steps: `python <skill>/scripts/devlog.py producer --summary`
  → pre-fills `YYYY-MM-DD-<agent>.md` per agent from today's handoffs (done/next/risks) and prints all logs.
  Edit each log only to add `decisions:` and trim to ≤12 lines (inline; no subagent needed) → fill `.studio/devlog/YYYY-MM-DD.md`, 5 lines:
  progress · decisions · blockers · risks · tomorrow → tell user in their language. Don't re-read handoffs the script already printed.
  Script marks `UNVERIFIED` (files/API not in project, or `file:line` past the file end) → report that task/claim as unverified, never as done or fact; PostToolUse hook marks bad refs in notes you write; reflect them in your reply.

## Release
Build settings + scene list · Player settings (icons, splash, company) · Profiler (CPU, GC, draw calls) · Addressables build ·
stripping/IL2CPP · slowest target device · `deps.md` licenses re-checked · in-game credits.
