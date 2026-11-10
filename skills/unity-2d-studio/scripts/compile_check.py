#!/usr/bin/env python3
"""Compile errors without switching to Unity (stdlib only).

  python compile_check.py [root] --files A.cs B.cs   dotnet build of the Unity-generated .csproj of each file's assembly
                                                     (asmdef name, else Assembly-CSharp[-Editor]). No dotnet, no .csproj
                                                     (Unity not opened since the asmdef was added) or STUDIO_DOTNET_CHECK=0
                                                     → skipped silently.
  python compile_check.py [root] --editor-log        errors CS of the last compile in Unity's Editor.log, if that log is
                                                     this project's. Works when the Studio console bridge never loaded.
Output: one `path(line,col): error CSxxxx: …` per line, exit 1 if any; exit 0 and no output when clean or skipped.
"""
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

sys.dont_write_bytecode = True
# dotnet prints absolute paths, Unity relative ones; keep the Assets/… part either way.
ERROR_RE = re.compile(r"^\s*(?:[^(]*?[\\/])?(Assets[\\/][^(]+\(\d+,\d+\)): error (CS\d+): (.*?)(?: \[[^\]]*\.csproj\])?\s*$")
COMPILE_START = "[ScriptCompilation] Requested script compilation"
MAX_TAIL = 4 * 1024 * 1024


def assembly_of(root, rel):
    folder = os.path.dirname(os.path.join(root, rel))
    while True:
        defs = [f for f in os.listdir(folder) if f.endswith(".asmdef")] if os.path.isdir(folder) else []
        if defs:
            try:
                with open(os.path.join(folder, defs[0]), encoding="utf-8-sig") as fh:
                    return json.load(fh).get("name") or defs[0][:-7]
            except (OSError, ValueError):
                return defs[0][:-7]
        if os.path.normcase(folder) == os.path.normcase(os.path.join(root, "Assets")) or os.path.dirname(folder) == folder:
            break
        folder = os.path.dirname(folder)
    editor = "/Editor/" in "/" + rel.replace("\\", "/")
    return "Assembly-CSharp-Editor" if editor else "Assembly-CSharp"


def errors_from(text):
    seen = []
    for line in text.splitlines():
        m = ERROR_RE.match(line)
        if m:
            item = f"{m.group(1).replace(chr(92), '/')}: error {m.group(2)}: {m.group(3)}"
            if item not in seen:
                seen.append(item)
    return seen


def dotnet_errors(root, files):
    if os.environ.get("STUDIO_DOTNET_CHECK") == "0" or not shutil.which("dotnet"):
        return []
    projects = []
    for rel in files:
        if rel.endswith(".cs"):
            proj = os.path.join(root, assembly_of(root, rel) + ".csproj")
            if os.path.exists(proj) and proj not in projects:
                projects.append(proj)
    out_dir = os.path.join(tempfile.gettempdir(), "studio-dotnet", hashlib.sha1(root.encode()).hexdigest()[:10])
    found = []
    for proj in projects:
        try:
            r = subprocess.run(["dotnet", "build", proj, "-nologo", "-v", "q", "-o", out_dir], cwd=root,
                               capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=240)
        except (OSError, subprocess.TimeoutExpired):
            continue
        found += [e for e in errors_from(r.stdout + r.stderr) if e not in found]
    return found


def editor_log_path():
    if sys.platform == "win32":
        return os.path.join(os.environ.get("LOCALAPPDATA", ""), "Unity", "Editor", "Editor.log")
    if sys.platform == "darwin":
        return os.path.expanduser("~/Library/Logs/Unity/Editor.log")
    return os.path.expanduser("~/.config/unity3d/Editor.log")


def editor_log_errors(root):
    path = editor_log_path()
    if not os.path.isfile(path):
        return []
    with open(path, "rb") as fh:
        head = fh.read(16384).decode("utf-8", "replace")
        size = os.path.getsize(path)
        fh.seek(max(0, size - MAX_TAIL))
        tail = fh.read().decode("utf-8", "replace")
    if norm_text(os.path.abspath(root)).rstrip("/") not in norm_text(head):
        return []
    return last_compile_errors(tail)


def last_compile_errors(log):
    # Each compile ends in "*** Tundra build success|failed"; its errors sit just before and after that line.
    ends = [m for m in re.finditer(r"^\*\*\* Tundra build (success|failed)", log, re.M)]
    if not ends or ends[-1].group(1) == "success":
        return []
    start = ends[-2].end() if len(ends) > 1 else 0
    nxt = log.find(COMPILE_START, ends[-1].end())
    return errors_from(log[start:nxt if nxt >= 0 else len(log)])


def norm_text(text):
    return os.path.normcase(text).replace("\\", "/")


def main():
    args = sys.argv[1:]
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    flags = {"--files", "--editor-log"}
    root = os.path.abspath(args[0] if args and args[0] not in flags else ".")
    if "--editor-log" in args:
        found = editor_log_errors(root)
    else:
        files = args[args.index("--files") + 1:] if "--files" in args else []
        found = dotnet_errors(root, [os.path.relpath(f, root) if os.path.isabs(f) else f for f in files])
    if found:
        print("\n".join(found))
    sys.exit(1 if found else 0)


if __name__ == "__main__":
    main()
