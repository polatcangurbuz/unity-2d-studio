#!/usr/bin/env python3
"""Claude Code hook entry points (wired by the plugin's hooks/hooks.json).

State is kept per agent (`agent_id` from the hook input, "main" for the producer), so parallel subagents never see or
clear each other's files.

session-start:  SessionStart. Studio project → refreshes the map, prints status.py (board, compile, console, scenes saved
                since the last stop) into context and moves the scene stamp.
subagent-start: SubagentStart. Stamps the start time (scene check + handoff check measure from it).
post-edit:      PostToolUse on Edit/Write. A .cs file: remember it for this agent's stop check and refresh
                .studio/PROJECT_MAP.md only if the public signature hash changed. Prints nothing.
                A .studio note (.md): devlog.py --check-refs marks bad file:line refs "(UNVERIFIED …)" in place and tells Claude.
stop:           Stop / SubagentStop. check_code.py on this agent's remembered files, scene_check.py on .unity/.prefab
                files under Assets/_Project saved since this agent's previous stop (or start), compile_check.py (dotnet build
                of the changed files' assemblies). A studio subagent that wrote no handoff, or one without the
                `# <ID> <title> · <agent> · <date>` header, is asked once. Clean → silent exit 0. Violations → stderr, exit 2 (Claude keeps working).
Any unexpected error → appended to .studio/hook-errors.log, exit 0: a broken hook must never block the session.
"""
import glob
import json
import os
import re
import subprocess
import sys
import time
import traceback

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
STATE = ".studio"
# Studio subagents that must leave a handoff (scribe writes devlogs instead).
HANDOFF_AGENTS = {"architect", "gameplay-coder", "art-director", "vfx-artist", "qa-reviewer"}
HEADER_RE = re.compile(r"^# [A-Z]+-\d+ .+ · [a-z][a-z0-9-]* · \d{4}-\d{2}-\d{2}")


def find_root(path):
    path = os.path.abspath(path)
    while True:
        if os.path.isdir(os.path.join(path, "Assets")):
            return path
        parent = os.path.dirname(path)
        if parent == path:
            return None
        path = parent


def agent_key(event):
    raw = event.get("agent_id") or "main"
    return re.sub(r"[^\w-]", "_", str(raw))[:64]


def state(root, name, key):
    return os.path.join(root, STATE, f".{name}" if key == "main" else f".{name}.{key}")


def run(script, *args):
    return subprocess.run([sys.executable, os.path.join(HERE, script), *args], capture_output=True, text=True, encoding="utf-8")


def read(path):
    with open(path, encoding="utf-8") as fh:
        return fh.read()


def write(path, text=""):
    with open(path, "w", encoding="utf-8") as fh:
        fh.write(text)


def session_start(event):
    root = find_root(event.get("cwd") or os.getcwd())
    if not root or not os.path.exists(os.path.join(root, STATE, "brief.md")):
        return 0
    # The user clicks builders and plays between sessions: refresh the map and report what Unity saved meanwhile.
    run("project_map.py", root, "--if-changed")
    out = run("status.py", root).stdout.strip()
    write(state(root, "scene_stamp", "main"))  # status.py just reported those scenes; the next Stop starts from here
    if out:
        print(out)
    return 0


def subagent_start(event):
    root = find_root(event.get("cwd") or os.getcwd())
    if root and os.path.isdir(os.path.join(root, STATE)):
        write(state(root, "agent_start", agent_key(event)), str(time.time()))
    return 0


def post_edit(event):
    path = (event.get("tool_input") or {}).get("file_path", "")
    note = path.endswith(".md") and STATE in path.replace("\\", "/").split("/")
    if not path.endswith(".cs") and not note:
        return 0
    root = find_root(os.path.dirname(path))
    if not root:
        return 0
    if note:
        refs = run("devlog.py", "--root", root, "--check-refs", path)
        if refs.returncode:
            sys.stderr.write("file:line refs not in project, now marked (UNVERIFIED) in place; report them as unverified:\n"
                             + refs.stdout.strip() + "\n")
            return 2
        return 0
    pending = state(root, "pending_check", agent_key(event))
    os.makedirs(os.path.dirname(pending), exist_ok=True)
    rel = os.path.relpath(os.path.abspath(path), root).replace("\\", "/")
    known = set(read(pending).split()) if os.path.exists(pending) else set()
    if rel not in known:
        with open(pending, "a", encoding="utf-8") as fh:
            fh.write(rel + "\n")
    run("project_map.py", root, "--if-changed")
    return 0


def code_report(root, key):
    pending = state(root, "pending_check", key)
    if not os.path.exists(pending):
        return ""
    files = [f for f in read(pending).split() if os.path.exists(os.path.join(root, f))]
    rules = run("check_code.py", root, "--files", *files) if files else None
    build = run("compile_check.py", root, "--files", *files) if files else None
    parts = []
    if rules and rules.returncode:
        parts.append("check_code.py found rule violations in files you changed; fix them:\n" + rules.stdout.strip())
    if build and build.returncode:
        parts.append("compile_check.py (dotnet build of Unity's .csproj) found compile errors; fix them before telling the user to switch to Unity:\n"
                     + build.stdout.strip())
    last = pending + ".last"
    if not parts:
        for p in (pending, last):
            if os.path.exists(p):
                os.remove(p)
        return ""
    report = "\n".join(parts)
    # Report already shown and unchanged (ignored or unfixable) → stay silent instead of repeating it every turn.
    if os.path.exists(last) and read(last) == report:
        return ""
    write(last, report)
    return report


def scene_report(root, key):
    # Unity, not Claude, writes scenes (builder clicks), so look at what changed since this agent's previous stop/start.
    stamp, start = state(root, "scene_stamp", key), state(root, "agent_start", key)
    if not os.path.isdir(os.path.join(root, STATE)):
        return ""
    since = os.path.getmtime(stamp) if os.path.exists(stamp) else os.path.getmtime(start) if os.path.exists(start) else None
    write(stamp)
    if since is None:
        return ""
    base = os.path.join(root, "Assets", "_Project")
    changed = [os.path.relpath(p, root) for ext in ("unity", "prefab")
               for p in glob.glob(os.path.join(base, "**", "*." + ext), recursive=True) if os.path.getmtime(p) > since]
    if changed:
        run("project_map.py", root, "--if-changed")  # scenes are in the map; Unity, not an edit, created them
    result = run("scene_check.py", root, "--files", *changed) if changed else None
    if not result or result.returncode == 0:
        return ""
    return "scene_check.py: scenes/prefabs Unity saved since your last turn; fix the builder, not the YAML:\n" + result.stdout.strip()


def handoff_report(root, key, event):
    if key == "main" or (event.get("agent_type") or "").split(":")[-1] not in HANDOFF_AGENTS:
        return ""
    start, asked = state(root, "agent_start", key), state(root, "handoff_asked", key)
    if not os.path.exists(start) or os.path.exists(asked):
        return ""
    since = os.path.getmtime(start)
    touched = [p for p in glob.glob(os.path.join(root, STATE, "handoff", "*.md")) if os.path.getmtime(p) >= since]
    agent = (event.get("agent_type") or "").split(":")[-1]
    # qa-reviewer appends to the author's handoff; the header is the author's to fix.
    bad = [os.path.basename(p) for p in touched if agent != "qa-reviewer" and not HEADER_RE.match(read(p))]
    if touched and not bad:
        return ""
    write(asked)
    if bad:
        return (f"handoff header missing in {', '.join(bad)}: first line must be `# <ID> <title> · {agent} · YYYY-MM-DD` "
                "(devlog.py and end of day read the agent and date from it). Fix it, then reply.")
    return ("no .studio/handoff/<taskID>.md written or updated in this task: the producer reads only the handoff. "
            f"Write it (≤20 lines; first line `# <ID> <title> · {agent} · YYYY-MM-DD`, then goal | files | api | events | tests | open | next), then reply.")


def cleanup(root, key):
    if key == "main":
        return
    for name in ("pending_check", "pending_check.last", "scene_stamp", "agent_start", "handoff_asked"):
        p = state(root, name, key)
        if os.path.exists(p):
            os.remove(p)


def stop(event):
    root = find_root(event.get("cwd") or os.getcwd())
    if not root:
        return 0
    key = agent_key(event)
    reports = [r for r in (code_report(root, key), scene_report(root, key), handoff_report(root, key, event)) if r]
    if not reports:
        cleanup(root, key)
        return 0
    sys.stderr.write("\n".join(reports) + "\n")
    return 2


HANDLERS = {"session-start": session_start, "subagent-start": subagent_start, "post-edit": post_edit, "stop": stop}


def main():
    handler = HANDLERS.get(sys.argv[1] if len(sys.argv) > 1 else "")
    if not handler:
        sys.exit("usage: hooks.py " + "|".join(HANDLERS) + "  (hook event JSON on stdin)")
    # Windows defaults to a legacy codepage: "·"/"≤" in reports would be garbled or raise.
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8")
    event = {}
    try:
        raw = "" if sys.stdin is None or sys.stdin.isatty() else sys.stdin.read()
        event = json.loads(raw) if raw.strip() else {}
        sys.exit(handler(event))
    except SystemExit:
        raise
    except Exception:
        root = find_root(event.get("cwd") or os.getcwd()) if isinstance(event, dict) else None
        if root and os.path.isdir(os.path.join(root, STATE)):
            with open(os.path.join(root, STATE, "hook-errors.log"), "a", encoding="utf-8") as fh:
                fh.write(f"--- {time.strftime('%Y-%m-%d %H:%M:%S')} {sys.argv[1]}\n{traceback.format_exc()}")
        sys.exit(0)


if __name__ == "__main__":
    main()
