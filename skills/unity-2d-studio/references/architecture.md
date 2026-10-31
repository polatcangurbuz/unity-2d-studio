When to read: designing a new system, manager or assembly, wiring scenes, splitting a big class. Read only the needed `##` section.
Sections: Shape · Managers · Communication · Plain C# core · State machines · Assemblies · Scene flow · Evolving

# Architecture

## Shape
```
Bootstrap scene (index 0, never unloaded)
  Bootstrapper ── registers ──► Services: IGameFlow, ISceneService, IAudioService, IUIService, IInputService, ISaveService, IPoolService
  loads additive ──► Level scenes + UI scene
Gameplay objects ── resolve services in Awake ── raise/listen SO event channels
```
- `Bootstrapper` holds serialized refs to manager components, registers each under its interface, loads first content scene additively.
- Managers: plain MonoBehaviours, one interface each, no singleton.
- Play from any scene: `Bootstrapper` sets `EditorSceneManager.playModeStartScene` = Bootstrap, reopens edited scene additively.
- Signatures: patterns § Services, § Bootstrapper.

## Managers
Create only what the game needs now.
| Manager | Interface | Owns |
|---|---|---|
| GameFlow | `IGameFlow` | Boot → Menu → Playing → Paused → GameOver |
| Scene | `ISceneService` | additive load/unload, loading screen, scene handles |
| Audio | `IAudioService` | pooled sources, mixer groups, SO sound defs |
| UI | `IUIService` | screen stack, transitions |
| Input | `IInputService` | action maps, rebinding, per-state enable |
| Save | `ISaveService` | versioned JSON |
| Pool | `IPoolService` | shared pools by prefab (only if several systems share) |
- Talk via interfaces/events, never concrete types. No `FindObjectOfType`/`GameObject.Find`/string lookups.
- >~300 lines = two jobs → split (`AudioService` → `MusicPlayer` + `SfxPlayer`). Manager owns its state.

## Communication
| Need | Use |
|---|---|
| inside one system | direct ref / C# `event` |
| across systems/scenes, designer-wireable | SO channel (`VoidEventChannelSO`, `EventChannelSO<T>`) |
| global request | `Services.Get<IAudioService>()` cached in `Awake` |
| live object list | runtime-set SO, cleared on unload |
Channels: one raiser; subscribe `OnEnable`, unsubscribe `OnDisable`. Name past tense: `PlayerDied`, `WaveCleared`.
Reuse the project's existing channel types; add a typed channel only when no existing one fits the payload.

## Plain C# core
`PlayerMotor` (MonoBehaviour: input + physics in, result to Rigidbody2D) → `PlayerMovementModel` (plain C#: velocity, jump counts, timers; EditMode-tested).
Rule with testable branches → plain class. Unity API stays in adapter.

## State machines
- `StateMachine` + `IState` (`Enter`, `Tick(float dt)`, `Exit`) in Core; states = plain classes with context ctor arg.
- Transitions inside states until many; then transition table.
- Extend: new state class + one transition, or handler in existing state. No flags in states. Re-entering current state is a no-op → act directly.
- For player, enemy AI, game flow.

## Assemblies
```
<ns>.Core        no project refs   (interfaces, Services, channels, StateMachine)
<ns>.Gameplay    → Core            (+ Unity.InputSystem, Cinemachine as needed)
<ns>.UI          → Core, Gameplay
<ns>.Editor      → Core, Gameplay, UI (Editor only)
<ns>.Tests.EditMode / .PlayMode → what they test
```
Deps point down; cycle = type in wrong assembly (usually → Core interface). `rootNamespace` set. `StudioSetup` creates these.
New assembly only for a real boundary (reusable module, optional feature).

## Scene flow
Bootstrap → Menu (additive) → Level_NN + UI (additive). Load only via `ISceneService` (tracks handles, unloads first). Many levels → Addressable scenes.

## Evolving
Smallest version + `// DEBT:` with trigger.
| Today | Upgrade when | To |
|---|---|---|
| static `Services` | per-test containers / scoped lifetimes | DI container via ladder (e.g. VContainer, MIT) |
| SO channels | complex/ordered payloads | typed channel per domain / message bus |
| transitions in states | >8 states or designer-edited | transition table SO |
| one Gameplay asmdef | slow compiles / optional feature | split by feature |
Decision log: `.studio/gdd.md` § Tech, one line `YYYY-MM-DD | decision | why | revisit when`.
