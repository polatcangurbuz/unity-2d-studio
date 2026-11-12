When to read: match-3 / tile-swap puzzle game (board, levels, boosters, difficulty, meta, live ops). Read only the needed `##` section.
Sections: Install · Core · Levels · Feel · Difficulty · Meta · Live ops · Pro look

# Match-3 puzzle

## Install
`python <skill>/scripts/install_unity.py --pack match3` → `Assets/_Project/Code/Puzzle/{Runtime,Tests,View,Editor}` (namespace `<ns>.Puzzle`,
keeps existing files) + `Data/Levels/*.txt`. Then `.studio/menu.request` `Tools/Studio/Match3/Build Board Scene` + `Tools/Studio/Platform Preset/Mobile Portrait`
and a play check of `Assets/_Project/Scenes/Puzzle.unity` (unity-bridge § Requests). Result: portrait board, placeholder gems (one shape per colour),
swipe/tap input, cascades, specials, HUD, particles. Needs Input System + TMP + URP 2D (template default).
Pack source stays the reference: don't read Runtime files to learn the rules, the pack `README.md` (40 lines) has them.

## Core
Pure C# (`noEngineReferences`), deterministic (seeded xorshift), 84 EditMode tests. Extend it, never fork logic into the view.
- `new PuzzleGame(LevelParser.Parse(text))` → `TrySwap(a, b)` / `UseBooster(kind, pos)` → `TurnResult.Steps` (ordered, `Step.Wave` = cascade depth).
- State: `Board`, `MovesLeft`, `State` (Playing/Won/Lost), `GoalProgress(i)`, `Level.Goals`; `MoveFinder.TryGetBest(game, out swap)` = hint.
- New rule/blocker/special: add `StepKind` + resolver code + a test in `Tests/` first; then the view animates the new step kind.
- Run tests on plain .NET while Unity is closed: `dotnet test <pack>/Tests~` (copy of the pack's tests, same sources).

## Levels
Text files in `Data/Levels/` (format in pack README: `moves/colors/seed/goals/grid`, `C2` crate, `I1` ice, `L` chain, `#` hole, presets `3H`, `*`).
`BoardView._level` = the TextAsset; level select loads `BoardView.Load(text)`. Keep levels as text: diffable, simulator-readable, no editor tool needed.
New blocker art → `BoardSkinSO` fields; layout → `BoardLayout` (row 0 top, centred).

## Feel
`BoardFeelSO` (tune in Play Mode): swap 0.16 s back-out · pop swell 1.2× 0.18 s · special hold 0.12 s · fall 14 cells/s back-out, 15 ms column stagger ·
wave pause 0.05 s · +6 % pitch per cascade wave · hint after 5 s. `StepPlayer` plays beats (swap → clear → fire → transform → settle) and
awaits each before the next; `BoardView.Events` (PieceCleared, WaveCleared, SpecialFired, BlockerHit, GoalProgressed, LevelEnded…) feed juice, HUD, audio.
Add effects as listeners (`BoardJuice` pattern: one shared ParticleSystem, `Emit` per piece with its colour), never inside StepPlayer.
Shake has an accessibility toggle (`ShakeEnabled`); keep it.

## Difficulty
Simulator before playtest: `dotnet run --project <pack>/Sim~ -- Assets/_Project/Data/Levels/*.txt --runs 500 --bot greedy` → pass rate, moves left, per level.
Target pass rates (greedy bot ≈ a decent player): tutorial 1-10 ≥95 % · normal 70-90 % · hard (every ~5-10th) 40-60 % · super hard 20-35 %.
Sawtooth curve: hard level followed by 2-3 easy ones. Levers in order: moves ±2 · colours 5↔6 (biggest lever) · goal counts · blocker hp/placement.
Random bot pass rate ≈ "can't fail by accident": keep it <50 % on hard levels. Re-run the sim after any rule change; record the table in `gdd.md`.

## Meta
Meta = why players come back; keep it data-driven (SOs + save), separate from the board scene.
- Progress map or decoration meta: stars from wins buy tasks/area items (own art, own theme; don't copy existing games' worlds, characters or names).
- Economy (all in one `EconomySO`): coins, lives (5, one per 30 min, lost only on fail), pre-level boosters, in-level boosters (`UseBooster`),
  +5 moves continue for coins once per level. Rewards from goals/stars, never from random paid boxes without user decision.
- Save: `ISaveService` (data-and-assets § Save) with version field; level index, currencies, inventory, timestamps (UTC, guard clock rollback).
- Mechanics and genre conventions are free to use; art, characters, names, music and UI layout of a specific game are not.

## Live ops
Only when the user asks (costs: backend, ops). Ask which service; default offer:
- Remote config: Unity Gaming Services Remote Config (free tier) or a versioned JSON on a CDN → level order, difficulty overrides, event dates.
- Events as data: `EventSO` (id, start/end UTC, goal, rewards) — streak (win N in a row), collection (gather X), league/leaderboard (needs backend).
- Analytics funnel: level_start / level_win / level_fail (moves left, boosters, attempts). Compare real pass rate per level with the simulator.
- Ads/IAP (Unity LevelPlay, IAP package): ask user; rewarded ads for continue/boosters only, never interrupting a board.

## Pro look
Checklist before calling a board scene "polished" (verify on play-check shots):
- Art: one cohesive style (art-direction § Palette), gems readable at 1 cm, distinct silhouette per colour (colour-blind), soft drop shadow,
  board frame + tile checkerboard, background lower contrast than pieces.
- Motion: everything eased (no linear), anticipation before pops, specials telegraph (glow/pulse), combo text on wave ≥3, win sequence
  (remaining moves → rockets, confetti, star count-up).
- UI: safe area, big moves counter, goal icons with tick, buttons ≥ 120 px on 1080 w, press feedback, transitions ≤0.3 s (ui § Juice).
- Audio: per-event SFX, pitch rising per wave, music ducked on win/lose popups.
- Tools: tweens = pack `Tweens` or LitMotion (MIT). Spine (paid licence) / DOTween (custom licence) → ask the user first; Unity 2D Animation is the free path.
