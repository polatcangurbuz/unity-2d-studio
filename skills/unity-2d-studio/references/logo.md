When to read: game logo, wordmark, app icon, favicon, Unity splash. Read only the needed `##` section.
Sections: Brief · Concepts · Test · Kit

# Logo

## Brief
≤10 lines in `.studio/art/logo-brief.md`: game name, pitch, 3 adjectives, palette roles to use, where shown (store capsule, app icon, splash, title screen),
must-avoid (other games' marks, genre clichés the user dislikes). Ask the user once (batched) if name or adjectives are missing.

## Concepts
Exactly 3 distinct directions, hand-written SVG (`.studio/art/logo/concept-{1,2,3}.svg`), one per type, e.g.:
wordmark (custom lettering idea on a free font) · symbol + wordmark · emblem/badge.
- Geometry first: few paths, grid-based, palette hex only, no gradients/filters in the master, text converted to paths if font licence allows, else note the font.
- Each concept: one line rationale (`concept-2: island silhouette inside S | reads at 16px`).
- Never imitate an existing logo or a reference game's mark. Fonts: SIL OFL / Apache only; log in `.studio/deps.md`.

## Test
```
python <skill>/scripts/logo_sheet.py .studio/art/logo/concept-*.svg
```
→ `.studio/art/logo/test-sheet.html`: each concept at 16/32/64/256 px, one colour (black, white), on light and dark backgrounds.
Fail if: unreadable at 16 px (icon variant), meaning depends on colour, thin strokes vanish on dark.
Show the sheet to the user → **STOP** for a choice. No kit work before the user picks one.

## Kit
For the chosen concept only:
- `logo-full.svg`, `logo-mark.svg` (icon-only), `logo-mono-black.svg`, `logo-mono-white.svg`.
- PNG exports (Inkscape CLI if installed: `inkscape <svg> --export-type=png -w <px> -o <out>`; else ask user to export):
  app icon 1024, 512, 256, 192, 180, 128, 64, 48, 32, 16 · Steam capsule sizes from current Steam docs (look up once, note in deps/brief).
- Unity: `Assets/_Project/Art/UI/Logo/`; Player Settings icons + splash (Splash Screen → Logos, dark background colour from palette).
- One-line usage rules in the art bible: min size, clear space = mark height × 0.25, allowed backgrounds.
