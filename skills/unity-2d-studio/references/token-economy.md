When to read: writing agent-to-agent text, using/refreshing the project map, compressing memory, picking a model. Read only the needed `##` section.
Sections: a) Telegraph · b) Least code · c) Project map · d) Memory · e) Reading · f) Models · g) Questions

# Token economy
Cost = what agents read + what they write + which model. Each rule cuts one without cutting correctness.

## a) Telegraph
For handoffs, board, devlogs, status, agent messages. Not for: user replies (plain, short, their language), risk warnings, destructive actions, license questions.
- Result first: `[thing] [change] [reason]`. No greeting, recap, "I will now".
- Drop articles/filler; keep `not`, `no`, `only`, `never`, numbers, units.
- `|` fields · `→` then/leads to · `·` list items · `+18 -4` diff size. One fixed term per thing.
- Never compress: code, identifiers, paths, commands, errors, versions, GUIDs. Not shorter or loses meaning → normal sentence.
Example (41 → 13 words): `T-007 done | PlayerMotor: 2nd jump midair, resets on land | open: coyote time`

## b) Least code
Understand (map, handoff, touched code) → library ladder (code-standards § Library ladder) → smallest code meeting acceptance.
No speculative abstractions/params; shortcut → `// DEBT: <upgrade path>`. Never cut validation or save-safety to save lines.

## c) Project map
`python <skill>/scripts/project_map.py [root] [--out PATH] [--max-lines 300] [--all] [--module NAME]`
- Regex, no LLM, stdlib Python 3.8+. Scans `Assets/` (skips `ThirdParty/`, `Plugins/`, `Samples/` unless `--all`).
- Has: asmdefs + deps, scenes, SO assets by script, most-referenced types, per type: kind, base/interfaces, public API, `[SerializeField]`s, used types, DEBT count.
- Over budget it drops `uses:` → `ser:` → inlines `pub:`; then points to `--module <name>`. Types + public API never cut.
- Read map before any source; answers "what exists / who uses X / where is Y".
- Refreshed by the PostToolUse hook (`scripts/hooks.py`) only when public signatures change; no manual step.
- "over budget" or ~1000+ scripts → suggest graphify (`uv tool install graphifyy`, `graphify install --project`).

## d) Memory
Project `CLAUDE.md` ≤60 lines telegraph: game, namespaces, `.studio/` pointers, 5-10 hard rules. Rest in `.studio/`, read on demand, no duplication.
Compressing a memory file: keep headings/code/paths verbatim, save original as `.bak`.

## e) Reading
- Grep/Glob before Read; read line ranges. References: only the needed `##` section.
- YAML (`.unity`/`.prefab`/`.asset`): never whole; MCP or GUID grep.
- Noisy output: filter first (`| grep -E "error|Exception" | head`).
- Don't re-read files you just wrote or that a handoff already summarizes, unless editing them.
- Multi-task session: map + handoff stay in context; don't re-read them, re-read only files changed since.

## f) Models
haiku (scribe): devlog, map refresh, bookkeeping · sonnet: mechanics, art prompts, VFX specs, reviews · opus (architect): cross-system architecture.
Subagent spawn has a fixed cost; tiny edits inline.

## g) Questions
Collect all, ask once with defaults. Blocked task → board `blocked: <question>`; others continue.
