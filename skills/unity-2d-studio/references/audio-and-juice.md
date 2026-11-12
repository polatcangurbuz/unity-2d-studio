When to read: audio manager, sound definitions, mixer, hit stop, screen shake, squash/stretch, tweens. Read only the needed `##` section.
Sections: Audio · Visual juice · Tweens

# Audio and juice

## Audio
Owner: gameplay-coder. Only when the game needs it (architecture § Managers).
- `SoundDefSO`: clips[], volume range, pitch range, mixer group, cooldown (`[Min]/[Range]/[Tooltip]`). Random clip/pitch per play.
- `AudioService : IAudioService` in Bootstrap: `ObjectPool<AudioSource>` (prewarm ~8-16), `Play(SoundDefSO, Vector2? pos)`, music on its own source with crossfade.
- AudioMixer `Audio/Mixers/Main.mixer`: Master → Music, SFX, UI. Volumes via exposed params in dB (`20·log10(v)`), saved in settings.
- Triggers: listen to existing channels/events (jumped, died, wave cleared); no `Play` calls scattered in unrelated code.
- Import: SFX = Decompress On Load (short) · music = Streaming, Vorbis. Files under `Audio/SFX`, `Audio/Music`.

## Visual juice
Split: gameplay-coder owns timing that changes play (hit stop length, coyote time); vfx-artist owns visuals reacting to events.
- Hit stop: `Time.timeScale = 0` for 0.03-0.08 s via unscaled-time `Awaitable`/coroutine; one owner, ignores overlapping requests; never in menus.
- Screen shake: Cinemachine already in project → Impulse (`CinemachineImpulseSource` on the cause, listener on the camera). Else 10-line offset on the
  camera's parent (Perlin or random, decaying 0.1-0.3 s); don't add Cinemachine just for shake. Amplitude by event size; accessibility toggle.
- Squash & stretch: scale the sprite child, not the physics root; preserve area (x·y ≈ 1); 0.08-0.15 s back to 1.
- Hit flash: shader `_Flash` via MaterialPropertyBlock (vfx-materials-shaders § Shaders), 1-2 frames.
- Particles on events: vfx-materials-shaders § Runtime. Damage numbers: pooled TMP text.
- Every juice effect listens to an event; gameplay code never calls juice directly.

- Polish bar (pro-looking): every input answers within 1 frame (press scale 0.95) · nothing teleports (ease every move, overshoot 4-8 %) ·
  anticipation before big events (swell, squash, 0.05-0.12 s) · layered feedback per event (sprite + particles + sound + UI number) ·
  combos escalate (pitch +5 %/step, bigger shake, text) · idle hints · reduce-motion toggle disables shake/flash.

## Tweens
Library ladder (code-standards § Library ladder):
1. Unity: Animator/Timeline for authored motion; `AnimationCurve` + `Awaitable`/coroutine for 1-3 code tweens.
2. Project already has a tween lib → use it.
3. Many code tweens → MIT library (e.g. LitMotion): verify current LICENSE + version, add via git URL/OpenUPM, log `.studio/deps.md`.
   Non-MIT/custom licence (some popular tween libs) → ask user first.
4. Own minimal helper only if 1-3 don't fit.
Tweens on UI use unscaled time (work during hit stop/pause).
