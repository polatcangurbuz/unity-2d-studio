When to read: sprite animation (flipbooks, Animator states, rigged characters). Read only the needed `##` section.
Sections: Choose · Flipbook · Animator · Rigged

# Animation

## Choose
| Need | Use |
|---|---|
| Looping prop, pickup, VFX sprite (no logic) | flipbook clip on SpriteRenderer, no Animator logic, or code flipbook on a pooled object |
| Character with states (idle/run/jump/hit) | Animator + flipbook clips, params driven by the gameplay adapter |
| Smooth character motion, many outfits | 2D Animation package (bones + Sprite Resolver), PSD Importer for layered art |
| UI/feel motion (scale, move, fade) | tweens (audio-and-juice § Tweens), not Animator |
Spine: paid licence → ask the user first.

## Flipbook
Sheets from `sprite_from_image.py`/artists, sliced by SpriteImportRules (`<name>_sheet_<W>x<H>.png`). Builders (`using Studio.EditorTools;`):
```
var frames = StudioAnimUtil.SheetSprites("Assets/_Project/Art/Sprites/Hero_sheet_32x32.png"); // sorted by _N suffix
var run = StudioAnimUtil.Flipbook("Assets/_Project/Art/Animations/Hero_Run.anim", frames, fps: 10, loop: true, first: 4, count: 6);
```
Pixel art 8-12 fps, smooth 12-24. Clip on the `Visual` child's SpriteRenderer. Last frame is held (no blank last frame).

## Animator
```
var ctrl = StudioAnimUtil.Controller("Assets/_Project/Art/Animations/Hero.controller", ("Idle", idle), ("Run", run), ("Jump", jump));
StudioAnimUtil.Param(ctrl, "Speed", AnimatorControllerParameterType.Float);
StudioAnimUtil.Transition(ctrl, "Idle", "Run", ("Speed", AnimatorConditionMode.Greater, 0.1f));
StudioAnimUtil.Transition(ctrl, "Any", "Jump", ("Jumped", AnimatorConditionMode.If, 0f));
```
Controller is rebuilt from scratch each run (idempotent builder). Transitions: duration 0, no exit time (sprite games snap).
- Runtime: `static readonly int SpeedId = Animator.StringToHash("Speed")`; the adapter sets params from model state once per frame; no strings per frame.
- Few states + params; >8 states or sub-state spaghetti → the logic belongs in a code state machine (patterns § State machine) that sets one int param.
- Animation events only for frame-exact cues (footstep, hit frame) → raise a C# event; no gameplay logic in clips.

## Rigged
`com.unity.2d.animation` (+ `com.unity.2d.psdimporter`): asmdef ref `Unity.2D.Animation.Runtime` (asmdefs.py adds it).
- Layered PSD/PSB → Skinning Editor: bones, auto geometry + weights → animate bones in clips (Animator as above).
- Outfits/expressions: Sprite Library asset + `SpriteResolver` (category/label) → swap by code, one rig.
- Performance: enable Burst/Collections for deformation; ≤ 20 rigged characters on screen on mobile.
