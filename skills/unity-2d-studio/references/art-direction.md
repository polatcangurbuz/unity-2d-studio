When to read: art bible, palette, value/silhouette rules, reference-game analysis, mood board, sprite import, accessibility. Read only the needed `##` section.
Sections: Order · Pipeline · Blender · Art bible · Palette · References · Boards · Import · Accessibility

# Art direction

## Order
Direction before production (no asset prompt before the bible exists and the user approved it):
brief (style, PPU, refs) → pillars → palette (`palette.py`) → values/shapes/resolution → board → user OK → § Pipeline → production.
Every later art task reads `.studio/art/art-bible.md` only, not this file again.
One-time outputs (`art-bible.md`, `palette.html`, `model-notes.md`) exist → read, never regenerate; rerun `palette.py` only when the user changes the palette.

## Pipeline
First asset-production task and `.studio/brief.md` has no `art-pipeline:` → detect, then ask ONE question (user's language), record the answer:
- detect (one parallel batch): `python <skill>/scripts/blender_sprites.py check` · ToolSearch "image generate" / "pixel" (image MCP tools).
- offer only what works, cheapest first, with a one-line trade-off each; default = the first:
| Option | Best for | Cost | Quality |
|---|---|---|---|
| `prompt` (skill's current path) | any style; user runs the image model | lowest tokens; user's model plan | depends on model + § After generation cleanup |
| `blender` (headless script, MCP only to model) | gems, items, props, icons, boosters, crates, isometric/3D-look sets | free, no API key; Blender installed | consistent light/scale across sets |
| `image-mcp` (Gemini/PixelLab/other MCP the user connects) | characters, backgrounds, painted/pixel styles | provider credits per image | high, needs a style anchor for consistency |
Write `art-pipeline: <option> (+ mcp: <tool prefix>)` in the brief; never ask again unless the user wants to switch. Mixed is fine
(`blender` for pieces, `image-mcp` for characters): record per asset type. Placeholders (`placeholder_sprites.py`, pack placeholder art) need no choice.
Cost rules for every path: one approved style anchor first (1-3 assets), then the batch; judge sets by ONE preview board, not per file;
redo only rejected assets; never regenerate approved ones.
- `prompt` → image-prompting § Sprite prompt → user generates → § After generation (`sprite_from_image.py`).
- `blender` → § Blender.
- `image-mcp` → image-prompting § Model notes (the MCP's model) + § MCP.

## Blender
`python <skill>/scripts/blender_sprites.py new <set> --preset gems|model` → edit `.studio/art/blender/<set>.json` → `render <set>` (~1-2 s/sprite).
- Spec: `out`, `size` (px = one cell at PPU = size), `view` top|front|iso, `shadow` (soft ground shadow), `look` (roughness, coat, key/rim/fill
  light, sheen, world), `items`: `gem` (sides 0 = round, 3-8; `angle`; `color` hex or palette role) or `model` (`file` .blend/.glb/.fbx/.obj,
  `yaw` list → directions, `frames` list → animation frames; all in one `<name>_sheet_<W>x<H>.png`, same scale).
- Read only `.studio/art/blender/<set>_preview.png`. Tune `look` once for the set (too flat → key/sheen up; washed out → key down, roughness up).
- Models: simple props → write a short bpy script (primitives, bevel, materials; headless `blender -b --python`); complex sculpted shapes →
  Blender MCP if connected, save `.blend` under `Assets/_Project/Art/Models/` (or outside Assets/ to keep it out of the build) → render.
  CC0 models (e.g. Kenney, Poly Haven) also work; log them in `deps.md`.
- Pixel-art look: render at 4-8× size, then `sprite_from_image.py --cell WxH --key none` (block downscale + palette).

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
Before real art: `placeholder_sprites.py` → `SPR_*.png` from `.studio/art/placeholders.yaml` (palette roles), same import rules.

## Accessibility
- Colour-blind: `palette.html` rows; never encode meaning by hue alone (add shape/icon/value).
- UI text contrast ≥4.5:1 (WCAG AA), large text ≥3:1 → check with `palette.py` contrast column against the panel colour.
- Screen shake/flash: options toggle; no full-screen flashes >3/s.
