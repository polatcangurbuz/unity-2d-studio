When to read: writing/reviewing C# beyond what SKILL.md quick card covers. Read only the needed `##` section.
Sections: SOLID · Patterns · Comments · Naming · Hot path · Async · Library ladder · YAGNI and diff

# Code standards

## SOLID
- S: one reason to change. Mover + animator + audio = 3 classes.
- O: add a state / strategy SO / listener, not a bigger switch.
- L: implementers honour contract; no `NotImplementedException` stubs.
- I: small interfaces (`IDamageable`, `IInteractable`), not fat `IEntity`.
- D: depend on interfaces + serialized refs; managers via `Services`, never by name.
Rules in plain C# (EditMode-testable); MonoBehaviour = thin adapter: read input/physics → call plain class → apply result.

## Patterns
Use only when it pays today. Two `if`s ≠ state machine; one listener ≠ event bus.
| Pattern | When | Unity form | Signatures (`patterns.md`) |
|---|---|---|---|
| State machine | ≥3 modes with different rules | `IState` + `StateMachine` | § State machine |
| Observer | one event, several unrelated reactors | C# `event` in-system; SO channel cross-system | § Event channels |
| Object pool | frequent spawn/despawn | `UnityEngine.Pool.ObjectPool<T>` in the owner | § Pool |
| Strategy | designer-swappable behaviour | abstract SO + method, asset per variant | - |
| Command | remap, replay, undo | struct/class with `Execute()` | - |
| Factory | creation needs setup | plain class owned by a manager | - |
| Service locator | global systems | `Services.Register/Get` via `Bootstrapper` | § Services, § Bootstrapper |
No singletons; global → register in `Services`.

## Comments
- No `/// <summary>`/XML docs, no `/* */`, no multi-line, no banners.
- ≤1 line, *why* only. Names say *what*. Designer help → `[Tooltip]`. Shortcut → `// DEBT: <upgrade path>`.
```csharp
// Physics2D queries are cheaper than OnTrigger for 1-frame checks.
[Tooltip("Extra jumps allowed while airborne")] [SerializeField, Min(0)] private int _airJumps = 1;
```

## Naming
- PascalCase types/methods/properties/events/constants; `_camelCase` private fields; `camelCase` locals.
- `I*` interfaces; `SO` suffix only on name clash (`EnemyStatsSO` vs `EnemyStats`).
- No public fields: `[SerializeField] private` or `[field: SerializeField] public T X { get; private set; }`.
- One top-level class per file, file = class. Namespace `<Company>.<Game>.<Module>` = asmdef.
- Events past tense (`Died`), handlers `On<Event>`. Bools as questions (`IsGrounded`).
- asmdef layers: `Core` ← `Gameplay` ← `UI`; details architecture § Assemblies.

## Hot path
Update/FixedUpdate/LateUpdate/per-frame callbacks:
- No `GetComponent*`, `Find*`, LINQ, `new` class/array/list, string concat/format, boxing, capturing lambdas.
- Cache in `Awake`; reuse lists; `Physics2D` overloads with result buffers.
- Event over polling; many tickers → one manager loops a list.
- `CompareTag`; cached `Animator.StringToHash` / `Shader.PropertyToID`.
- No `?.`/`??` on UnityEngine.Object.

## Async
- Unity 6+: `Awaitable` (`WaitForSecondsAsync(t, destroyCancellationToken)`, `NextFrameAsync`); always a lifetime token.
- Older: coroutines. UniTask only via ladder (+ `deps.md`).
- `async void` only for Unity event methods; never `.Result`.

## Library ladder
Stop at first rung that holds:
1. Unity built-in/official: Input System, Cinemachine, Tilemap, 2D Animation, 2D Pixel Perfect, TMP, Localization, Addressables, Test Framework.
2. Already in project (`.studio/PROJECT_MAP.md`, `.studio/deps.md`).
3. Trusted OSS: web search; verify version, activity, LICENSE file. MIT/Apache-2.0/BSD → add. GPL/LGPL/custom/unclear → ask user.
   Add via `Packages/manifest.json` (git URL pinned to tag, or OpenUPM). Log `.studio/deps.md`: `name | version | license | URL | why`.
4. Smallest own code meeting acceptance.

## YAGNI and diff
- `patterns.md` gives signatures only; write the project's own minimal code in its namespace and style.
- Pooling: `ObjectPool<T>` directly in the owner (ladder rung 1). A wrapper only when ≥2 owners share the same setup, and then the smallest one.
- No new asmdef, Editor menu script or generic base class unless the task asks for it or cannot be done without it.
  Asset creation the user can do in 3 clicks (Create menu via `[CreateAssetMenu]`) → list the steps, no menu script.
- No speculative parameters/layers/interfaces. Reuse existing types (event channels, pools, events) before adding siblings.
- Keep validation at trust boundaries (save, input, network) and data-loss handling.
- Branching rule → EditMode test (qa-and-perf § Tests).
