When to read: art bible, palette, value/silhouette rules, reference-game analysis, mood board, sprite import, accessibility. Read only the needed `##` section.
Sections: Order · Art bible · Palette · References · Boards · Import · Accessibility

# Art direction

## Order
Direction before production (no asset prompt before the bible exists and the user approved it):
brief (style, PPU, refs) → pillars → palette (`palette.py`) → values/shapes/resolution → board → user OK → prompts (image-prompting).
Every later art task reads `.studio/art/art-bible.md` only, not this file again.
One-time outputs (`art-bible.md`, `palette.html`, `model-notes.md`) exist → read, never regenerate; rerun `palette.py` only when the user changes the palette.

## Art bible
`.studio/art/art-bible.md` ≤60 lines. `palette.py set` creates the skeleton + palette table. Fill:
- Pillars: 2-3 words each + one line why (e.g. `crisp reads | hazards readable in 1 frame`).
- Values: player/enemies/hazards contrast with every background they cross; backgrounds low contrast, mid-dark.
- Shapes: player = most distinct silhouette; enemy families by shape (round = harmless, spiky = danger); readable as solid black fill.
- Resolution: one PPU (pixel 16/32, hi-res 100); sprite sizes multiples of PPU; character height in tiles.
- Outline: none / 1px darkest palette colour / coloured (pick one). Light from one direction (e.g. top-left) for all assets.
- UI: font, panel shape, which palette roles, min text size.

## Palette
5-8 colours, each with a role. Roles: `background`, `midground`, `interactive` (player, platforms you touch), `hazard`, `reward`, `ui`, `outline`, `accent`.
```
python <skill>/scripts/palette.py set background=#1b1f3b midground=#3e4a7a interactive=#f2f2e9 hazard=#e04f5f reward=#ffd34d outline=#0d0f1e
```
- Script validates count/hex, writes table (hex, luminance, contrast) into the bible + `.studio/art/palette.html` (normal, protan, deutan, tritan, value strip).
- Fix every `warn:` line: foreground roles ≥3:1 vs background; hazard vs reward must differ in value or shape, not hue only.
- `set` prints what it wrote; don't re-read the bible after it. `palette.py show` → role=hex for prompts, VFX, UI. Never type hex elsewhere by hand; change palette → re-run `set`.
- Pixel art: ramps of 3-4 shades per role are fine inside sprites; the bible lists role anchors only.

## References
User's 2-3 games → principles only, one line each in the bible:
`<game>: palette structure | readability trick | UI layout | juice`.
Never copy: characters, names, logos, assets, signature designs, level layouts. Never write game titles, character names or
"in the style of <game>" into prompts or files meant for production; describe the principle instead (e.g. "high-contrast hazard colour").

## Boards
Mood/palette board = HTML/SVG in `.studio/art/` (canvas idea: philosophy first, then visuals). `palette.html` is the base board;
add a silhouette row (black SVG shapes) only if the user wants to compare character shapes. No image generation needed for boards.

## Import
`assets/unity/Editor/SpriteImportRules.cs` (copied at kickoff with StudioSetup) applies to `Assets/_Project/Art/Sprites/`:
PPU + Point filter + no compression + no mipmaps from `.studio/brief.md` (`style: pixel 16 PPU`); hi-res → bilinear default compression.
Sheets named `<name>_sheet_<W>x<H>.png` auto-slice into W×H cells, pivot bottom-center. Name generated sheets that way.

## Accessibility
- Colour-blind: `palette.html` rows; never encode meaning by hue alone (add shape/icon/value).
- UI text contrast ≥4.5:1 (WCAG AA), large text ≥3:1 → check with `palette.py` contrast column against the panel colour.
- Screen shake/flash: options toggle; no full-screen flashes >3/s.
