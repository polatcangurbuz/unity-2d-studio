---
name: review
description: Unity 2D studio QA review of a board task by the independent qa-reviewer subagent.
argument-hint: "[task ID, e.g. T-007; empty = every row in review]"
disable-model-invocation: true
---

Task IDs: `$ARGUMENTS`. Empty → the IDs whose board status is `review` (from the `studio status` block in context, else
`python "${CLAUDE_PLUGIN_ROOT}/skills/unity-2d-studio/scripts/status.py"`). None → tell the user there is nothing to review and stop.
Missing `.studio/handoff/<ID>.md` → report it, skip that ID.

Calls to `unity-2d-studio:qa-reviewer` (each spawn re-reads map, references and console, so fewer is cheaper):
- ≤3 tasks and ≤15 files across their handoffs → ONE call: `tasks: <ID>, <ID> | review each | acceptance: <board line per ID> |
  read: .studio/PROJECT_MAP.md, .studio/handoff/<ID>.md …`; it appends `review:` to each handoff and replies one verdict per ID.
- More → one call per task, all calls in one message, prompt `task: <ID> | review | acceptance: <board line> | read: map, handoff`.
No qa-reviewer in your agent list → do the review yourself per `${CLAUDE_PLUGIN_ROOT}/skills/unity-2d-studio/references/qa-and-perf.md` § Review.

Then set the board: `pass` → `done`, `fail` → `doing` (owner unchanged), `unverified` → keep `review` and ask the user for one switch to
Unity / Play / Test Runner run. Reply in the user's language: one line per task (verdict, findings count, blockers).
