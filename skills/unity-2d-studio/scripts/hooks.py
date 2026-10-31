#!/usr/bin/env python3
"""Claude Code hook entry points (wired by the plugin's hooks/hooks.json).

post-edit: PostToolUse on Edit/Write/MultiEdit. For a .cs file: remember it for the stop check and refresh
           .studio/PROJECT_MAP.md only if the public signature hash changed. Prints nothing.
           A .studio note (.md): devlog.py --check-refs marks bad file:line refs "(UNVERIFIED …)" in place and tells Claude
           (exit 2) before it writes its reply.
stop:      Stop / SubagentStop. Runs check_code.py on the remembered files only. Clean → silent exit 0.
           Also runs scene_check.py on .unity/.prefab files under Assets/_Project saved since the previous Stop.
           Violations → listed on stderr, exit 2 (Claude keeps working to fix them).
"""
import glob
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PENDING = os.path.join(".studio", ".pending_check")
SCENE_STAMP = os.path.join(".studio", ".scene_stamp")


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


def code_report(root):
    pending = os.path.join(root, PENDING)
    if not os.path.exists(pending):
        return ""
    files = [f for f in open(pending, encoding="utf-8").read().split() if os.path.exists(os.path.join(root, f))]
    result = run("check_code.py", root, "--files", *files) if files else None
    if not result or result.returncode == 0:
        os.remove(pending)
        return ""
    report = result.stdout.strip()
    last = pending + ".last"
    # Report already shown and unchanged (ignored or unfixable) → stay silent instead of repeating it every turn.
    if os.path.exists(last) and open(last, encoding="utf-8").read() == report:
        return ""
    with open(last, "w", encoding="utf-8") as fh:
        fh.write(report)
    return "check_code.py found rule violations in files you changed; fix them:\n" + report


def scene_report(root):
    # Unity, not Claude, writes scenes (builder clicks), so look at what changed since the previous Stop.
    stamp = os.path.join(root, SCENE_STAMP)
    if not os.path.isdir(os.path.dirname(stamp)):
        return ""
    since = os.path.getmtime(stamp) if os.path.exists(stamp) else None
    open(stamp, "w").close()
    if since is None:
        return ""
    base = os.path.join(root, "Assets", "_Project")
    changed = [os.path.relpath(p, root) for ext in ("unity", "prefab")
               for p in glob.glob(os.path.join(base, "**", "*." + ext), recursive=True) if os.path.getmtime(p) > since]
    result = run("scene_check.py", root, "--files", *changed) if changed else None
    if not result or result.returncode == 0:
        return ""
    return "scene_check.py: scenes/prefabs Unity saved since your last turn; fix the builder, not the YAML:\n" + result.stdout.strip()


def stop(event):
    root = find_root(event.get("cwd") or os.getcwd())
    if not root:
        return 0
    reports = [r for r in (code_report(root), scene_report(root)) if r]
    if not reports:
        return 0
    sys.stderr.write("\n".join(reports) + "\n")
    return 2


def main():
    event = json.load(sys.stdin) if not sys.stdin.isatty() else {}
    handler = {"post-edit": post_edit, "stop": stop}.get(sys.argv[1] if len(sys.argv) > 1 else "")
    if not handler:
        sys.exit("usage: hooks.py post-edit|stop  (hook event JSON on stdin)")
    sys.exit(handler(event))


if __name__ == "__main__":
    main()
