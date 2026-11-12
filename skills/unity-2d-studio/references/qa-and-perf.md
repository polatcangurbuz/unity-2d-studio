When to read: reviewing a task, writing tests, profiling, phase gate/release. Read only the needed `##` section.
Sections: Tests · check_code · Review · Budgets · Platforms · Gates

# QA and performance

## Tests
- EditMode (no scene): every plain-C# rule with branches (movement model, damage, wave scheduling, save migration). AAA, one behaviour per test, `Method_Condition_Result`.
- PlayMode smoke: load Bootstrap, wait frames, assert services registered, `LogAssert.NoUnexpectedReceived()`. One per project.
- Shapes: patterns § Tests. Location `Assets/_Project/Code/Tests/EditMode|PlayMode/` (asmdefs from StudioSetup).
- Run: MCP if present; else user or batch mode (unity-bridge § No MCP).

## check_code
Runs automatically via the Stop hook (changed files only). Manual: `python <skill>/scripts/check_code.py [project_root] [--files A.cs B.cs] [--quiet]`
Flags: `/// <summary>` · `/* */` and 2+ consecutive `//` lines · files >300 lines · `FindObjectOfType` family, `GameObject.Find`, `Find*WithTag`,
`transform.Find("…")` · `GetComponent*` in `Update/FixedUpdate/LateUpdate` · public class fields (`const`, `static readonly`, events, properties, struct fields allowed) ·
files outside `Assets/_Project/` (`Plugins`, `StreamingAssets`, `ThirdParty` allowed). Exit 1 on any issue. Regex: hit = strong hint, miss ≠ proof.
Hook: plugin `hooks/hooks.json` (Stop → `hooks.py stop`: changed files only, silent when clean). Skill-only install (no hooks): run it with `--files` on changed files before the handoff.
CI/pre-commit option: `python <skill>/scripts/check_code.py --quiet || exit 1`

## Review
1. Compiles; Console clean of our errors/warnings.
2. `check_code.py` clean for handoff files.
3. Board acceptance demonstrably met (test or ≤1 min user steps).
4. Branching logic tested; tests pass.
5. SOLID smells: multi-job class, type switch, concrete manager deps, singletons.
6. Comments: no XML docs/blocks; why-only; `[Tooltip]` for designers.
7. Hot path: no alloc/GetComponent/LINQ/string building per frame; `?.` on UnityEngine.Object.
8. Data: SO not mutated at runtime; handles released; pooled spawns returned.
9. YAGNI: no unrequested asmdef/menu/base class; no generic base/wrapper with one user; pools use `ObjectPool<T>` directly.
10. `Assets/_Project/`, namespace = asmdef, one class per file. Map refreshed; handoff ≤20 lines, accurate.
Findings (telegraph, handoff `review:`): `file:line | problem | fix`.
play-check.txt + shots (unity-bridge § Play check) are the evidence for scene tasks: `clean` + shots matching the acceptance → pass;
shots not read → `unverified`.
console.log evidence: only lines after the last `play start` belong to the current Play; `package-…` lines are Unity packages,
not the task; a play error older than the last `mark build` or scene save → stale, say `unverified`, never `fail`.

## Budgets
Per platform in `gdd.md` § Tech. Mid mobile / PC:
frame ≤11 ms main thread · GC 0 B/frame in gameplay · draw calls ≤100, SetPass ≤30 after atlasing · ≤1000 active particles, pooled ·
atlases ≤2048² mobile, compressed except pixel art · Bootstrap → playable ≤3 s.
Profile dev build on slowest target, not editor. Tools: Profiler (CPU, Memory), Frame Debugger, Memory Profiler package.

## Platforms
`Tools/Studio/Platform Preset/{Mobile Portrait, Mobile Landscape, WebGL, PC}` (via menu.request) sets Player Settings, writes `.studio/platform.md`;
never switches the build target (user does, it reimports everything). Mobile: IL2CPP, ARM64, Android API 24+, ASTC, stripping Low,
orientation locked, frame pacing · WebGL: Gzip + fallback, data caching, exceptions explicitly thrown only · PC: windowed fullscreen 1920×1080, vSync.
Then: target 60 fps mobile (`Application.targetFrameRate = 60` in Bootstrap), safe area on all UI (ui § Canvas), audio Vorbis, atlases per screen.

## Gates
Vertical slice: start → win/lose, no Console errors, smoke green, check_code clean, budgets met on target.
Release: + `deps.md` licenses re-verified, credits screen, build size noted.
