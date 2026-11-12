#!/usr/bin/env python3
"""Flag studio rule violations in a Unity project's C# (regex only, stdlib only).

Usage: python check_code.py [project_root] [--files A.cs B.cs] [--quiet]
Exit 0 = clean, 1 = violations. Output: path:line [rule] message
"""
import argparse
import os
import re
import sys

sys.dont_write_bytecode = True  # plugin dir may be a read-only cache: no __pycache__ there
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from project_map import STRIP_RE, TYPE_RE, match_brace, strip_code, top_level  # noqa: E402

MAX_FILE_LINES = 300
# Unity requires these at the Assets root, or packages put them there.
ALLOWED_ROOTS = {"_Project", "ThirdParty", "Plugins", "StreamingAssets", "TextMesh Pro", "AddressableAssetsData",
                 "Gizmos", "Editor Default Resources", "Samples", "XR", "TutorialInfo", "Settings"}
# URP and the Unity 6 templates create these at the Assets root themselves.
# Unity 6 URP templates also ship Settings/ (RP assets), TutorialInfo/ + Readme.asset.
ALLOWED_ROOT_FILES = {"UniversalRenderPipelineGlobalSettings.asset", "DefaultVolumeProfile.asset", "InputSystem_Actions.inputactions",
                      "Readme.asset"}
FIND_RE = re.compile(r"\b(FindObjectOfType|FindObjectsOfType|FindFirstObjectByType|FindAnyObjectByType|FindObjectsByType"
                     r"|FindWithTag|FindGameObjectWithTag|FindGameObjectsWithTag)\b|\bGameObject\.Find\s*\(|(?<!\bShader)\.Find\s*\(\s*\"")
STUDIO_USE_RE = re.compile(r"\b(StudioEditorUtil|StudioConsoleBridge|StudioSetup|StudioAsmdefs|VfxBuilder|StudioPlayCheck|StudioAnimUtil|StudioUiUtil|StudioLightUtil|StudioPlatforms|StudioCameraUtil|StudioRequests)\s*\.")
UPDATE_RE = re.compile(r"\bvoid\s+(Update|FixedUpdate|LateUpdate)\s*\(\s*\)\s*\{")
GETCOMP_RE = re.compile(r"\b(GetComponent|GetComponents|GetComponentInChildren|GetComponentInParent)\b")
PUBLIC_FIELD_RE = re.compile(
    r"(?:^|(?<=[;{}\]]))\s*public\s+((?:(?:static|readonly|new|volatile)\s+)*)"
    r"([\w.]+(?:<[^;=(){}]*?>)?(?:\[[,\s]*\])*\??)\s+(\w+)\s*(?:=(?!>)[^;]*)?;"
)


def line_at(text, pos):
    return text.count("\n", 0, pos) + 1


def check_file(path, rel):
    issues = []
    raw = open(path, encoding="utf-8-sig", errors="replace").read()
    lines = raw.splitlines()

    if len(lines) > MAX_FILE_LINES:
        issues.append((1, "file-length", f"{len(lines)} lines > {MAX_FILE_LINES}; split by responsibility"))

    last_doc = -2
    for m in STRIP_RE.finditer(raw):
        tok = m.group(0)
        if tok.startswith("///"):
            ln = line_at(raw, m.start())
            if ln != last_doc + 1:
                issues.append((ln, "doc-comment", "XML doc comment (/// <summary>); names explain what, one-line // explains why"))
            last_doc = ln
        elif tok.startswith("/*"):
            issues.append((line_at(raw, m.start()), "block-comment", "multi-line /* */ comment"))
    run = 0
    for i, l in enumerate(lines, 1):
        if re.match(r"\s*//(?!/)", l):
            run += 1
            if run == 2:
                issues.append((i - 1, "comment-block", "2+ consecutive comment lines; keep one line of why"))
        else:
            run = 0

    src = strip_code(raw)
    for m in FIND_RE.finditer(src):
        issues.append((line_at(src, m.start()), "find", f"'{m.group(0).strip()}' - inject a reference, use an event or a registry"))

    for m in UPDATE_RE.finditer(src):
        end = match_brace(src, m.end() - 1)
        for g in GETCOMP_RE.finditer(src, m.end(), end):
            issues.append((line_at(src, g.start()), "update-getcomponent", f"{g.group(1)} inside {m.group(1)}; cache it in Awake"))

    for m in TYPE_RE.finditer(src):
        if not m.group(3).startswith("class"):
            continue
        open_idx = m.end() - 1
        body = top_level(src[open_idx + 1:match_brace(src, open_idx)])
        for f in PUBLIC_FIELD_RE.finditer(body):
            mods = f.group(1).split()
            if f.group(2) in ("const", "event") or ("static" in mods and "readonly" in mods):
                continue
            ln = line_at(src, open_idx + 1) + body.count("\n", 0, f.start(2))
            issues.append((ln, "public-field", f"public field '{f.group(3)}'; use [SerializeField] private or a property"))

    # Studio Editor tools live in Studio.EditorTools; builders in the game's Editor namespace need the using.
    m = STUDIO_USE_RE.search(src)
    if m and not re.search(r"^\s*using\s+Studio\.EditorTools\s*;|^\s*namespace\s+Studio\.EditorTools\b", raw, re.M):
        issues.append((line_at(src, m.start()), "missing-using", f"{m.group(1)} used without 'using Studio.EditorTools;'"))

    if outside_project(rel):
        issues.append((1, "location", "outside Assets/_Project/"))
    return issues


def outside_project(rel):
    parts = rel.replace("\\", "/").split("/")
    if parts[0] != "Assets" or len(parts) < 2:
        return False
    if len(parts) == 2 and parts[1] in ALLOWED_ROOT_FILES:
        return False
    return parts[1] not in ALLOWED_ROOTS


def setup_will_remove(root, rel):
    """Unity's template SampleScene before StudioSetup ran: Setup deletes it when it holds only the default objects."""
    if rel != "Assets/Scenes/SampleScene.unity":
        return False
    if os.path.exists(os.path.join(root, "Assets/_Project/Scenes/Bootstrap.unity")) or \
            not os.path.exists(os.path.join(root, "Assets/_Project/Code/Editor/StudioSetup.cs")):
        return False
    try:
        text = open(os.path.join(root, rel), encoding="utf-8", errors="replace").read()
    except OSError:
        return False
    names = re.findall(r"--- !u!1 &\d+\s+GameObject:[\s\S]*?m_Name: (.*)", text)
    return all(n.strip() in ("Main Camera", "Global Light 2D", "Directional Light") for n in names)


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("root", nargs="?", default=".")
    ap.add_argument("--files", nargs="*", help="check only these files (paths relative to root or absolute)")
    ap.add_argument("--quiet", action="store_true", help="print only the summary line")
    args = ap.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    root = os.path.abspath(args.root)
    assets = os.path.join(root, "Assets")
    if args.files:
        targets = [p if os.path.isabs(p) else os.path.join(root, p) for p in args.files]
    elif os.path.isdir(assets):
        targets = []
        for dirpath, dirnames, filenames in os.walk(assets):
            if os.path.relpath(dirpath, assets) == ".":
                dirnames[:] = [d for d in dirnames if d not in ALLOWED_ROOTS - {"_Project"}]
            targets += [os.path.join(dirpath, f) for f in filenames if not f.endswith(".meta")]
    else:
        sys.exit(f"error: no Assets/ folder under {root}")

    total, bad_files, checked = 0, 0, 0
    for path in sorted(targets):
        rel = os.path.relpath(path, root).replace("\\", "/")
        if path.endswith(".cs"):
            issues = check_file(path, rel)
            checked += 1
        elif setup_will_remove(root, rel):
            print(f"{rel}: pending Tools > Studio > Setup Project (deletes Unity's empty template scene)")
            continue
        else:
            issues = [(1, "location", "outside Assets/_Project/")] if outside_project(rel) else []
        if issues:
            bad_files += 1
            total += len(issues)
            if not args.quiet:
                for ln, rule, msg in sorted(issues):
                    print(f"{rel}:{ln} [{rule}] {msg}")
    print(f"check_code: {'clean' if not total else f'{total} issue(s) in {bad_files} file(s)'} ({checked} .cs checked)")
    sys.exit(1 if total else 0)


if __name__ == "__main__":
    main()
