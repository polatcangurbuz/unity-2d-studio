#!/usr/bin/env python3
"""Claude Code hook entry points (wired by the plugin's hooks/hooks.json).

post-edit: PostToolUse on Edit/Write/MultiEdit. For a .cs file: remember it for the stop check and refresh
           .studio/PROJECT_MAP.md only if the public signature hash changed. Prints nothing.
           A .studio note (.md): devlog.py --check-refs marks bad file:line refs "(UNVERIFIED …)" in place and tells Claude
           (exit 2) before it writes its reply.
stop:      Stop / SubagentStop. Runs check_code.py on the remembered files only. Clean → silent exit 0.
           Violations → listed on stderr, exit 2 (Claude keeps working to fix them).
"""
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PENDING = os.path.join(".studio", ".pending_check")


def find_root(path):
    path = os.path.abspath(path)
    while True:
        if os.path.isdir(os.path.join(path, "Assets")):
            return path
        parent = os.path.dirname(path)
        if parent == path:
            return None
        path = parent


def run(script, *args):
    return subprocess.run([sys.executable, os.path.join(HERE, script), *args], capture_output=True, text=True, encoding="utf-8")


def post_edit(event):
    path = (event.get("tool_input") or {}).get("file_path", "")
    note = path.endswith(".md") and ".studio" in path.replace("\\", "/").split("/")
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
    pending = os.path.join(root, PENDING)
    os.makedirs(os.path.dirname(pending), exist_ok=True)
    rel = os.path.relpath(os.path.abspath(path), root).replace("\\", "/")
    known = set(open(pending, encoding="utf-8").read().split()) if os.path.exists(pending) else set()
    if rel not in known:
        with open(pending, "a", encoding="utf-8") as fh:
            fh.write(rel + "\n")
    run("project_map.py", root, "--if-changed")
    return 0


def stop(event):
    root = find_root(event.get("cwd") or os.getcwd())
    pending = os.path.join(root, PENDING) if root else ""
    if not pending or not os.path.exists(pending):
        return 0
    files = [f for f in open(pending, encoding="utf-8").read().split() if os.path.exists(os.path.join(root, f))]
    if not files:
        os.remove(pending)
        return 0
    result = run("check_code.py", root, "--files", *files)
    if result.returncode == 0:
        os.remove(pending)
        return 0
    report = result.stdout.strip()
    last = pending + ".last"
    # Report already shown and unchanged (ignored or unfixable) → stay silent instead of repeating it every turn.
    if os.path.exists(last) and open(last, encoding="utf-8").read() == report:
        return 0
    with open(last, "w", encoding="utf-8") as fh:
        fh.write(report)
    sys.stderr.write("check_code.py found rule violations in files you changed; fix them:\n" + report + "\n")
    return 2


def main():
    event = json.load(sys.stdin) if not sys.stdin.isatty() else {}
    handler = {"post-edit": post_edit, "stop": stop}.get(sys.argv[1] if len(sys.argv) > 1 else "")
    if not handler:
        sys.exit("usage: hooks.py post-edit|stop  (hook event JSON on stdin)")
    sys.exit(handler(event))


if __name__ == "__main__":
    main()
