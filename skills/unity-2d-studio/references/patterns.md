When to read: writing a type this project lacks (state machine, channel, services, bootstrap, pool, tests). Read only the needed `##` section.
Sections: State machine · Event channels · Services · Bootstrapper · Pool · Tests
Signatures only. Write the project's own minimal version in its namespace; skip members the task doesn't use.

# Patterns

## State machine
```csharp
public interface IState { void Enter(); void Tick(float deltaTime); void Exit(); }
public sealed class StateMachine { public IState Current { get; } public event Action<IState, IState> StateChanged;
  public void ChangeState(IState next); public void Tick(float deltaTime); }
```
- `ChangeState`: null → throw; same as Current → no-op; else `Current?.Exit()` → set → `Enter()` → raise `StateChanged(prev, next)`.
- Plain C# (Core asmdef), ticked by the owning MonoBehaviour. States = plain classes taking a context in ctor.
- `StateChanged` only if something listens (animation, debug UI).

## Event channels
```csharp
[CreateAssetMenu(menuName = "<Game>/Events/Void")] public class VoidEventChannelSO : ScriptableObject { public event Action Raised; public void Raise(); }
public abstract class EventChannelSO<T> : ScriptableObject { public event Action<T> Raised; public void Raise(T value); }
public class IntEventChannelSO : EventChannelSO<int> { }   // + [CreateAssetMenu]
```
- Add the generic base only when a payload is really needed and no existing channel fits. Void first.
- Listeners subscribe `OnEnable`, unsubscribe `OnDisable`. One raiser per channel.

## Services
```csharp
public static class Services { public static void Register<T>(T s) where T : class; public static void Unregister<T>(T s) where T : class;
  public static T Get<T>() where T : class; public static bool TryGet<T>(out T s) where T : class; public static bool IsEmpty { get; } }
```
- `Dictionary<Type, object>`; `Get` throws with "is the Bootstrap scene loaded?" hint; `Unregister` only if same instance.
- `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` clears it (domain reload may be off).
- Cache results in `Awake`; never call in Update.

## Bootstrapper
```csharp
public sealed class Bootstrapper : MonoBehaviour { [SerializeField] string _firstScene; [SerializeField] MonoBehaviour[] _managers; void Awake(); }
```
- Awake: register each manager under each of its non-System interfaces → load `_firstScene` additively.
- Editor play-from-any-scene: `[InitializeOnLoadMethod]` sets `EditorSceneManager.playModeStartScene` = build scene 0;
  on `ExitingEditMode` store active scene path in `SessionState`; Bootstrapper loads that instead of `_firstScene`. Wrap in `#if UNITY_EDITOR`.

## Pool
```csharp
_pool = new ObjectPool<T>(create: () => Instantiate(_prefab, transform), actionOnGet: x => x.gameObject.SetActive(true),
  actionOnRelease: x => x.gameObject.SetActive(false), actionOnDestroy: x => Destroy(x.gameObject), collectionCheck: false, defaultCapacity: n);
```
- Field in the owner (spawner/weapon/VFX player). Prewarm in Awake: Get n into an array, then Release all.
- Get → reset state (position, velocity, health). Death/despawn → `Release`. Subscribe instance events once in `create`.
- Wrapper class only when ≥2 owners share identical setup.

## Tests
```csharp
[Test] public void Method_Condition_Result()              // EditMode, Tests.EditMode asmdef, plain C# only
[UnityTest] public IEnumerator Bootstrap_Loads_WithServicesAndNoErrors()   // PlayMode: LoadScene(0), wait ~10 frames,
                                                                           // Assert.IsFalse(Services.IsEmpty); LogAssert.NoUnexpectedReceived()
```
- AAA; spy/fake `IState` with counters for state machine tests. One behaviour per test.
