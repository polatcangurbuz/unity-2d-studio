When to read: player input (keyboard, gamepad, touch, rebinding) or camera follow/bounds/shake. Read only the needed `##` section.
Sections: Input · Touch · Camera

# Input and camera

## Input
Input System package only (Player Settings → Active Input Handling = Input System Package or Both).
- Actions asset: Unity 6 templates ship `Assets/InputSystem_Actions.inputactions` (Player: Move, Look, Attack, Interact, Jump, Sprint…; UI map).
  Move it to `Assets/_Project/Settings/Input/`, delete unused actions, add game ones. None present → create one asset there with maps `Player` + `UI`.
- Code: `[SerializeField] private InputActionReference _move;` → `_move.action.Enable()` in OnEnable, read `ReadValue<Vector2>()` in Update
  or subscribe `performed` in OnEnable / unsubscribe in OnDisable. No `PlayerInput` SendMessage/BroadcastMessage, no string lookups per frame.
- Bigger games: `IInputService` (architecture § Services) owns the asset, enables one map per game state (Player in gameplay, UI in menus).
- Rebinding: `action.PerformInteractiveRebinding().Start()`, save `asset.SaveBindingOverridesAsJson()` in settings, load on boot.
- Gamepad + keyboard: same action, two bindings; show glyphs by `InputControl.device` of the last used control.
- UI: `InputSystemUIInputModule` on the EventSystem (`StudioUiUtil.EnsureEventSystem()` does it), never StandaloneInputModule.

## Touch
- Tap/drag/swipe in 2D: `Pointer.current` (mouse + touch + pen in one) → `position` / `press.wasPressedThisFrame`; world via `Camera.ScreenToWorldPoint`.
  Swipe = press → release delta ≥ 0.35 cell along the dominant axis (match3 pack `SwipeInput` is the reference).
- Multi-touch gestures → `EnhancedTouch` (`EnhancedTouchSupport.Enable()`, `Touch.activeTouches`).
- Virtual sticks: Input System `OnScreenStick`/`OnScreenButton` on UI → same actions as gamepad. Hit areas ≥ 9 mm.
- Ignore pointer over UI: `EventSystem.current.IsPointerOverGameObject()` check before world input.

## Camera
Cinemachine only if the project already has it or the game needs more than follow + bounds (multiple cameras, blends, cutscenes) → ask the user to add it.
- Cinemachine 3 present (`STUDIO_CINEMACHINE`) → builder `StudioCameraUtil.Follow2D(Camera.main, player, orthoSize, boundsCollider, lookahead 0.2, pixelPerfect)`:
  brain + CinemachineCamera, position composer (dead zone, damping), Confiner2D on a `PolygonCollider2D` (trigger, not on a physics layer), impulse listener.
- No Cinemachine → own `CameraFollow2D` (≤40 lines) on the camera: LateUpdate, `Vector3.SmoothDamp` toward target + velocity·lookahead,
  dead zone (only move when target leaves a small box), clamp to level bounds minus half view size; shake as offset on a parent transform.
- Fixed-screen games (puzzle, arena): no follow; fit orthographic size to content: `size = max(h/2, w/2/aspect) + margin`, recompute on resolution change.
- Pixel art: URP `PixelPerfectCamera` (unity-bridge § Builder gotchas) and camera moves snapped to texels; no damping smear.
