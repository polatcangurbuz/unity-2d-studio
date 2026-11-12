When to read: HUD, menus, popups, buttons, fonts, safe area, UI polish. Read only the needed `##` section.
Sections: Canvas · Build · Screens · Juice · Text

# UI

## Canvas
uGUI + TextMeshPro for game UI (animatable, particles, world/screen mix). UI Toolkit only for data-heavy menus/settings if the user prefers it.
- Canvas Scaler: Scale With Screen Size, reference 1080×1920 match 0 (portrait, width-driven) · 1920×1080 match 1 (landscape, height-driven).
- Safe area: every screen's content under a `SafeArea` RectTransform fitted to `Screen.safeArea` (match3 pack `SafeAreaFitter`); backgrounds outside it.
- Split canvases: static (frames, backgrounds) vs dynamic (counters, timers); any change rebuilds its whole canvas.
- `Raycast Target` off on every non-interactive Graphic; one EventSystem with `InputSystemUIInputModule`.
- Sorting: HUD canvas order 10, popups 20, transitions 30; world sprites never above UI.

## Build
Builder helpers (`StudioUiUtil`, Editor, needs uGUI; TMP if present):
```
var canvas = StudioUiUtil.Canvas("HUD", new Vector2(1080, 1920), 0f);      // + EventSystem
var safe = StudioUiUtil.SafeArea(canvas);
var bar = StudioUiUtil.Place(StudioUiUtil.Panel(safe, "TopBar", Vector2.zero, Vector2.one, new Color(0,0,0,.35f)), new Vector2(.5f, 1), new Vector2(1000, 220), new Vector2(0, -30));
var score = (TMP_Text)StudioUiUtil.Label(bar, "Score", "0", 72);          // TMP when installed, legacy Text otherwise
var play = StudioUiUtil.Button(safe, "Play", "PLAY", new Vector2(480, 140), accent);
var hp = StudioUiUtil.Fill(bar, "Health", back, fill);                      // Image.Type.Filled bar
```
`Place(rt, anchor, size, offset)` = anchor + pivot at the same point. TMP Essentials are imported automatically (`EnsureTmpEssentials`).
Wire views with `StudioEditorUtil.Wire`; UI code listens to events/channels, gameplay never references UI.

## Screens
- One `UIService`/screen stack: `Show<T>()`, `Hide()`, back button = pop; each screen = prefab with CanvasGroup (alpha, interactable, blocksRaycasts).
- Popups: dim background (black 60 %), tap outside = close only for non-critical ones; one popup at a time via queue.
- Flows: Title → Level select/Map → Game → Win/Lose popup → Map. Pause sets `timeScale 0`; UI tweens use unscaled time.
- Loading: additive scene + progress bar; never a frozen frame >0.3 s.

## Juice
- Buttons: press scale 0.92-0.95 (≤0.08 s), release overshoot 1.05 → 1, click SFX; disabled = desaturated, not hidden.
- Screens in: scale 0.9 → 1 back-out + fade 0.2-0.3 s; out faster (0.15 s). Stagger list items 30-50 ms.
- Counters roll (count up over 0.3-0.6 s), punch scale on change; rewards fly from source to counter, counter bumps on arrival.
- Stars/results revealed one by one with sound; confetti particles in UI via a camera-space canvas or UI particle on its own canvas.
- Reduce-motion setting: skip punch/shake, keep fades.

## Text
- TMP font asset per font: SDF, atlas 1024-2048, padding 9; dynamic for user text, static subset + fallback for UI. Fonts: OFL/Apache only, log `.studio/deps.md`.
- Numbers: `SetText("{0}", value)` (no string alloc), not `text = value.ToString()` per frame.
- Outline/shadow via TMP material presets, not extra Text components. Min size 28 px on 1080 w for body, 40+ for counters.
- Multiple languages → Localization package from the start (string tables), no hard-coded strings in builders except placeholders.
