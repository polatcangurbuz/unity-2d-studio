#!/usr/bin/env python3
"""Studio status in a few lines, no LLM: open board rows, Unity compile/console/test state, scenes Unity saved since the
last stop, stale or unverified handoffs, hook errors.

Usage: python status.py [root]   (SessionStart hook and /unity-2d-studio:status print this; it replaces reading board.md)
"""
import glob
import os
import re
import subprocess
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
MAX_ROWS = 8
STAMP_RE = re.compile(r"^\[\d\d:\d\d:\d\d\] ")
PROBLEM_RE = re.compile(r"^(package-)?(compile-error|error|exception|assert)\b")


def lines(path):
    if not os.path.exists(path):
        return []
    with open(path, encoding="utf-8", errors="replace") as fh:
        return fh.read().splitlines()


def board_rows(root):
    rows = []
    for l in lines(os.path.join(root, ".studio", "board.md")):
        cells = [c.strip() for c in l.strip().strip("|").split("|")]
        if len(cells) >= 4 and re.match(r"^[A-Z]+-\d+$", cells[0]):
            rows.append(cells)
    return rows


def console_summary(studio):
    console = lines(os.path.join(studio, "console.log"))
    if not console:
        return []
    body = [STAMP_RE.sub("", l) for l in console[1:]]
    # Only what happened in the last Play counts for play errors; the bridge marks its start.
    plays = [i for i, l in enumerate(body) if l.startswith("play start")]
    # Older bridges did not tag package errors: a line naming only Packages/ paths is Unity's, not project code.
    package = lambda l: l.startswith("package-") or ("Packages/" in l and "Assets/" not in l)
    own = [l for l in body if PROBLEM_RE.match(l) and not package(l)]
    pkg = sum(1 for l in body if PROBLEM_RE.match(l) and package(l))
    text = f"console.log: {console[0].lstrip('# ')}"
    if own:
        text += f" · {len(own)} error line(s)"
    if pkg:
        text += f" · {pkg} from Unity packages (not project code)"
    if not plays and any("[play]" in l for l in own):
        text += " · play errors predate the play-start marker (old bridge?)"
    return [text] + ["  " + l for l in own[:3]]


def scene_issues(root):
    stamp = os.path.join(root, ".studio", ".scene_stamp")
    since = os.path.getmtime(stamp) if os.path.exists(stamp) else 0
    base = os.path.join(root, "Assets", "_Project")
    changed = [os.path.relpath(p, root) for ext in ("unity", "prefab")
               for p in glob.glob(os.path.join(base, "**", "*." + ext), recursive=True) if os.path.getmtime(p) > since]
    if not changed:
        return []
    r = subprocess.run([sys.executable, os.path.join(HERE, "scene_check.py"), root, "--files", *changed],
                       capture_output=True, text=True, encoding="utf-8")
    found = r.stdout.strip().splitlines()
    return [f"scene_check ({len(changed)} scene/prefab file(s) saved since last stop): {len(found)} issue(s)"] + ["  " + f for f in found[:4]] if found else []


def compile_errors(root):
    r = subprocess.run([sys.executable, os.path.join(HERE, "compile_check.py"), root, "--editor-log"],
                       capture_output=True, text=True, encoding="utf-8")
    found = r.stdout.strip().splitlines()
    return [f"Unity Editor.log: last compile failed, {len(found)} error(s)"] + ["  " + f for f in found[:4]] if found else []


def handoff_flags(root, rows):
    status = {c[0]: c[3].lower() for c in rows}
    flags, unverified = [], []
    for p in sorted(glob.glob(os.path.join(root, ".studio", "handoff", "*.md"))):
        name = os.path.basename(p)[:-3]
        text = "\n".join(lines(p))
        if "UNVERIFIED" in text:
            unverified.append(name)
        # Task moved on (review/done) but its handoff still says blocked: the producer took it over and left it stale.
        if status.get(name, "").startswith(("review", "done")) and re.search(r"^(open|status):.*\bblocked\b", text, re.M | re.I) \
                and not re.search(r"^(fix|update|unblocked)\b", text, re.M | re.I):
            flags.append(name)
    out = []
    if unverified:
        out.append("UNVERIFIED refs in handoffs: " + ", ".join(unverified[:5]))
    if flags:
        out.append("stale handoffs (board review/done, handoff says blocked): " + ", ".join(flags[:5]) + " → update them")
    return out


def main():
    root = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else ".")
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    studio = os.path.join(root, ".studio")
    if not os.path.isdir(studio):
        print("studio: no .studio/ (kickoff not done)")
        return
    rows = board_rows(root)
    open_rows = [f"{c[0]} {c[1]} | {c[2]} | {c[3]}" for c in rows if c[3].lower() != "done"]
    out = [f"studio status · {len(open_rows)} open board row(s)"]
    out += ["  " + r for r in open_rows[:MAX_ROWS]]
    if len(open_rows) > MAX_ROWS:
        out.append(f"  … +{len(open_rows) - MAX_ROWS} more in .studio/board.md")
    out += compile_errors(root)
    out += console_summary(studio)
    tests = lines(os.path.join(studio, "test-results.txt"))
    if tests:
        out.append(f"tests: {tests[0].lstrip('# ')}")
    out += scene_issues(root)
    out += handoff_flags(root, rows)
    if os.path.exists(os.path.join(studio, "hook-errors.log")):
        out.append("hook errors logged: .studio/hook-errors.log (tell the user; plugin bug)")
    print("\n".join(out))


if __name__ == "__main__":
    main()
