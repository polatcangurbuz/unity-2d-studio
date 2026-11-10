---
name: end-of-day
description: Unity 2D studio end of day - devlogs from today's handoffs and a 5-line day summary, written by the scribe subagent.
argument-hint: "[date YYYY-MM-DD, default today]"
disable-model-invocation: true
---

Date: `$ARGUMENTS` (empty → today). Read nothing yourself.
`unity-2d-studio:scribe` in your agent list → ONE call: `scribe: end of day <date>, reply in <user's language>`, then relay its reply as is
(it lists UNVERIFIED items). No scribe → follow `${CLAUDE_PLUGIN_ROOT}/skills/unity-2d-studio/references/studio-workflow.md` § Devlog inline.
