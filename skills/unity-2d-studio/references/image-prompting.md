When to read: writing prompts for an image-generation model (sprites, backgrounds, key art, icons). Read only the needed `##` section.
Sections: Model notes · MCP · Adapters · Sprite prompt · Other assets · After generation

# Image prompting

## Model notes
Model name + commercial-use answer come from `.studio/brief.md`. Unknown licence → ask user before production use.
`.studio/art/model-notes.md` exists → read it, skip the web. Missing → ONE web search for the model's current official prompt guide
(+ fetch 1-2 official pages), then write the cache (≤25 lines) and never search again unless the user switches model/version:
```
# <model> <version> · checked YYYY-MM-DD · <url> · <url>
style: natural | keyword+weight | reference-image
syntax: <param list exactly as documented: aspect, negative, seed, style/character reference, stylize, version>
transparency: <native | not supported → flat #ff00ff/green bg + remove>
consistency: <seed / style ref / character ref mechanism>
limits: <text in images, max refs, size>
license: <commercial terms summary>
```
Prompts use only syntax listed there. No model list in this skill (they age); the cache is the source.

## MCP
`art-pipeline: image-mcp` (art-direction § Pipeline): the image model is a tool, so Claude generates directly. Same prompt rules as § Sprite prompt.
- Style anchor: generate 2-3 variants of ONE key asset at the cheapest setting the tool offers (draft model/size) → user picks → save as
  `.studio/art/anchor/<name>.png`; every later call passes it as reference image (style/character consistency), text states only the change.
- Batch the rest with the anchor + fixed seed where supported; final quality setting only for approved drafts.
- Ask transparent output if the tool supports it, else flat key colour; always finish with `sprite_from_image.py` (scale, palette, sheet name).
- Ask the user for a credit budget once (e.g. "≤40 images this session"); count calls in `.studio/art/prompts/log.md`; stop at the cap.
- Licence/commercial terms of the provider → `.studio/deps.md` before production use.

## Adapters
| Style | Shape | Put bible in as |
|---|---|---|
| natural language | full sentences, subject first, then style, view, lighting, background | "palette of #hex (role)…" sentence |
| keyword + weight | comma terms, weight syntax from notes, separate negative field/param | hex/colour words + weights; negatives: blur, gradient bg, text, watermark, extra limbs |
| reference image | short text + style/character reference image(s) + strength | palette.html / approved sprite as reference; text only states the change |
Same subject → same seed/reference across the set (consistency).

## Sprite prompt
Every sprite prompt states all of these (e4 checks them):
1. Subject + action (`small round sky-bat, flapping`), silhouette note from the bible.
2. Style: `pixel art, <PPU> px grid, <outline rule>, light from <dir>`, palette roles as hex.
3. View/angle: `side view, orthographic, facing right` (fixed for the whole set).
4. Background: transparent if notes say native; else `flat solid #ff00ff background, no shadow, no ground`.
5. Frames: `sprite sheet, <N> frames in one horizontal row, each frame <W>x<H> px, equal spacing` (W,H multiples of PPU);
   concept/key pose → `single sprite, 1 frame, <W>x<H> px`. In the prompt text itself, not only in its heading.
6. Model syntax from notes: aspect = N·W : H, seed/style ref, negatives.
Keyword-style example skeleton (fill syntax from notes, not from memory):
`<subject>, pixel art, 16 px grid, 1px dark outline, light top-left, palette #1b1f3b #f2f2e9 #e04f5f, side view facing right,
flat #ff00ff background, sprite sheet 6 frames one row, each 32x32 px <aspect param> <seed/style param> <negative param> blur, text`
Save as `.studio/art/prompts/<asset>.md`: prompt, model, seed/ref, target file `<name>_sheet_<W>x<H>.png`.
Each prompt complete on one line, copy-paste ready: no placeholders (`<common tail>`, `...`), all 6 items in every prompt.

## Other assets
- Tiles: `seamless tileable, top/side view, <W>x<H> px, edges match`; then check tiling in Unity (or Material Maker for procedural).
- Backgrounds/parallax: layers separately (far/mid/near), low contrast, value darker than gameplay layer, wide aspect.
- Key art/store capsule: free composition, but palette + characters from the bible; logo added later (logo.md), never generated text.
- Icons/UI: flat, 1 colour role + outline, readable at 32 px.

## After generation
`python <skill>/scripts/sprite_from_image.py <image> --cell WxH [--frames N | --grid CxR] [--outline role|#hex] [--key auto|none|#hex] [--no-palette] [--force]`
→ key background removed, frames split, one scale for all frames (block majority, no smearing), bottom-centre, colours mapped to the art-bible palette
→ `Art/Sprites/<Stem>_sheet_<W>x<H>.png` (SpriteImportRules slices it). High `drift` → re-prompt with the palette hex codes, don't ship.
Never hand-clean generated pixels in code.
Log asset + model + licence in `.studio/deps.md` if commercial terms apply.
