#!/usr/bin/env python3
"""Studio status in ≤12 lines, no LLM: open board rows, Unity console/test state, unverified notes, hook errors.

Usage: python status.py [root]   (SessionStart hook and /unity-2d-studio:status print this; it replaces reading board.md)
"""
import glob
import os
import re
import sys

sys.dont_write_bytecode = True
MAX_ROWS = 8


def lines(path):
    if not os.path.exists(path):
        return []
    with open(path, encoding="utf-8", errors="replace") as fh:
        return fh.read().splitlines()


def board_rows(root):
    rows = []
    for l in lines(os.path.join(root, ".studio", "board.md")):
        cells = [c.strip() for c in l.strip().strip("|").split("|")]
        if len(cells) < 4 or not re.match(r"^[A-Z]+-\d+$", cells[0]):
            continue
        if cells[3].lower() != "done":
            rows.append(f"{cells[0]} {cells[1]} | {cells[2]} | {cells[3]}")
    return rows


def main():
    root = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else ".")
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    studio = os.path.join(root, ".studio")
    if not os.path.isdir(studio):
        print("studio: no .studio/ (kickoff not done)")
        return
    rows = board_rows(root)
    out = [f"studio status · {len(rows)} open board row(s)"]
    out += ["  " + r for r in rows[:MAX_ROWS]]
    if len(rows) > MAX_ROWS:
        out.append(f"  … +{len(rows) - MAX_ROWS} more in .studio/board.md")
    console = lines(os.path.join(studio, "console.log"))
    if console:
        errors = sum(1 for l in console[1:] if l.startswith(("compile-error", "error", "exception")))
        out.append(f"console.log: {console[0].lstrip('# ')}" + (f" · {errors} error line(s)" if errors else ""))
    tests = lines(os.path.join(studio, "test-results.txt"))
    if tests:
        out.append(f"tests: {tests[0].lstrip('# ')}")
    unverified = [os.path.basename(p) for p in glob.glob(os.path.join(studio, "handoff", "*.md"))
                  if "UNVERIFIED" in "\n".join(lines(p))]
    if unverified:
        out.append("UNVERIFIED refs in handoffs: " + ", ".join(sorted(unverified)[:5]))
    if os.path.exists(os.path.join(studio, "hook-errors.log")):
        out.append("hook errors logged: .studio/hook-errors.log (tell the user; plugin bug)")
    print("\n".join(out))


if __name__ == "__main__":
    main()
