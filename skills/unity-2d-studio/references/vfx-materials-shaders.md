When to read: particle effect, explosion/hit/pickup VFX, Effekseer, procedural textures, normal maps, 2D shaders/materials. Read only the needed `##` section.
Sections: Particles · Runtime · Effekseer · Material Maker · Shaders · Lighting 2D · Batching

# VFX, materials, shaders

## Particles
Default = Unity Particle System from a spec. Colours come from palette roles only (`palette.py show`).
```
python <skill>/scripts/vfx_spec.py new <name> --preset burst|explosion|hit|pickup|dust|trail --colors <roleA>,<roleB> --trigger <ChannelAsset>
python <skill>/scripts/vfx_spec.py build <name>
```
- `new` → `.studio/vfx/<name>.yaml` (layers, timing, roles, blend); tweak numbers there, never hex. `build` → `Assets/_Project/Art/VFX/Specs/<name>.vfx.json`.
- Unity: **Tools > Studio > Build VFX From Specs** (`VfxBuilder.cs`, copied at kickoff) → `Prefabs/VFX/<name>.prefab`
  (root = timer with stop callback, child per layer, pixel square or soft texture, alpha/additive material).
- `VfxBuilder.cs` missing in project → copy `assets/unity/Editor/VfxBuilder.cs` to `Assets/_Project/Code/Editor/` (one file, not a new menu of yours).
- Pixel art: `texture: pixel`, small sizes (≥1/PPU), few particles (8-30), short lifetimes; soft only for flashes.
- Spec keys (layer): name, shape circle|cone|edge|point, radius, angle, burst, rate, lifetime/speed/size `min-max`, size_over_life none|shrink|grow,
  color `role>role`, fade, gravity, blend alpha|additive, texture pixel|soft, noise, delay. That's all: don't read VfxBuilder.cs or vfx_spec.py.

## Runtime
Effect plays where something happened → reuse the gameplay event, add no new system:
- One `VfxPlayer`-style MonoBehaviour per effect type (or in an existing feedback component): `ObjectPool<ParticleSystem>` of the prefab
  (patterns § Pool), prewarm = spec `pool_prewarm`.
- Trigger: subscribe to the existing SO channel / C# event (`OnEnable`/`OnDisable`). Needs a position → payload channel only if no event carries it;
  often the emitter (enemy) already raises a C# event with itself → use its position.
- Play: `Get` → set position → `Play()`; root `stopAction = Callback` → `OnParticleSystemStopped` on root → `Release`.
- Never `Instantiate`/`Destroy` per effect; no `GetComponent` per play (cache in pool create).

## Effekseer
Only for big set pieces (boss explosion, spell) the particle presets can't express. Editor is GUI → agent writes the spec, user builds:
`.studio/vfx/<name>.effekseer.md`: layers (emitter, shape, count, life, speed curve, size curve), timing (ms), palette hex per layer, blend, texture needs.
Unity: EffekseerForUnity (MIT, re-check LICENSE + pin a release tag, not master; log `.studio/deps.md`) → pooled emitter + same channel trigger as § Runtime.

## Material Maker
Tileable ground/wall textures, noise for shaders, normal maps for URP 2D lights. Agent writes the recipe, user builds in the GUI:
`.studio/art/materials/<name>.md`: nodes in order (e.g. `voronoi → warp → colorize(palette hex) → tiling check`), output size (power of 2, PPU multiple),
maps (albedo, normal), export as PNG → `Assets/_Project/Art/Materials/`. Normal map → sprite Secondary Texture named `_NormalMap`.

## Shaders
URP 2D Shader Graph first (designers edit); HLSL only for what Graph can't do. Sprite Lit/Unlit target. Graphs in `Art/Shaders/`.
| Effect | Core nodes / params |
|---|---|
| hit flash | lerp(tex, flash colour, `_Flash` 0-1) keep alpha |
| dissolve | noise tex step `_Cutoff`, edge = smoothstep band × edge colour (palette hazard/reward) |
| outline | 4 offset alpha samples − own alpha × `_OutlineColor`, `_Thickness` in texels (1 for pixel art) |
| palette swap | sample 1×N ramp tex by grey value; ramp built from palette roles |
| wind sway | vertex offset x = sin(time·speed + worldPos.x) × amount × uv.y |
| water distortion | scene colour / UV offset by scrolling noise, `_Strength` small |
| glow | emission colour > 1 + URP Bloom on 2D renderer + 2D Light (no shader if bloom suffices) |
Per-instance values (flash amount, dissolve) via `MaterialPropertyBlock` or SpriteRenderer colour → keeps batching (§ Batching).

## Lighting 2D
URP 2D Renderer only (Lit sprites are black without a global light). Builder helpers (`StudioLightUtil`, Editor, URP present):
`Global(intensity, color?)` (reuses the scene's) · `Point(parent, outerRadius, intensity, color, shadows)` · `ShadowCaster(go, selfShadows)`;
new lights target all sorting layers (Unity's default is only Default → "light does nothing").
- Mood: global 0.3-0.6 tinted by palette `shadow` role + few point lights on gameplay hotspots (torches, pickups, player); not one per object.
- Normal maps: secondary texture `_NormalMap` on the sprite import; light `Normal Maps` quality Accurate only where read up close.
- Shadows: ShadowCaster2D on large static occluders (walls, crates), not on every tile/enemy; Composite Shadow Caster for tilemaps.
- Mobile budget: ≤4 overlapping lights per pixel, shadows off on low tier, light blend styles ≤2. Pixel art: Point light falloff hard (0-0.2).
- Puzzle/UI-heavy games: Unlit sprites + baked glow sprites look the same and cost nothing.

## Batching
Shared material per effect type; per-object variation through vertex colour/MPB, not new material instances (`renderer.material` = copy, avoid).
Same atlas + material + sorting layer → batches. Check Frame Debugger after adding a shader; particles ≤1000 active (qa-and-perf § Budgets).
