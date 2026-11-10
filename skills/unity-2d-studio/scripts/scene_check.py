#!/usr/bin/env python3
"""Static checks on Unity scene/prefab YAML, no Unity needed. Usage: scene_check.py [root] [--files rel/path.unity ...]

Flags: empty CompositeCollider2D (saved before its geometry was generated) · unassigned object refs in _camelCase fields of
project scripts · URP Sprite-Lit material in a scene without any Light2D (sprites render black).
Clean → exit 0, prints nothing. Issues → one line each, exit 1.
"""
import glob
import os
import re
import sys

DOC_RE = re.compile(r"^--- !u!\d+ &(\d+)[^\n]*\n(\w+):", re.M)
NAME_RE = re.compile(r"^  m_Name: (.*)$", re.M)
OWNER_RE = re.compile(r"^  m_GameObject: \{fileID: (\d+)\}", re.M)
SCRIPT_RE = re.compile(r"^  m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})", re.M)
UNSET_RE = re.compile(r"^  (_\w+|<\w+>k__BackingField): \{fileID: 0\}", re.M)
EMPTY_COMPOSITE_RE = re.compile(r"^  m_CompositePaths:\n    m_Paths: \[\]", re.M)
# Stable GUIDs shipped with URP (com.unity.render-pipelines.universal).
SPRITE_LIT_GUID = "a97c105638bdf8b4a8650670310a4cd3"
LIGHT2D_GUID = "073797afb82c5a1438f328866b10b3f0"


def project_script_guids(root):
    guids = set()
    for meta in glob.glob(os.path.join(root, "Assets", "_Project", "**", "*.cs.meta"), recursive=True):
        m = re.search(r"^guid: ([0-9a-f]{32})", open(meta, encoding="utf-8", errors="ignore").read(), re.M)
        if m:
            guids.add(m.group(1))
    return guids


def documents(text):
    heads = list(DOC_RE.finditer(text))
    for i, h in enumerate(heads):
        end = heads[i + 1].start() if i + 1 < len(heads) else len(text)
        yield h.group(1), h.group(2), text[h.start():end]


def check(path, rel, own_scripts):
    text = open(path, encoding="utf-8", errors="ignore").read()
    docs = list(documents(text))
    names = {fid: (NAME_RE.search(body) or [None, "?"])[1] for fid, kind, body in docs if kind == "GameObject"}
    owner = lambda body: names.get((OWNER_RE.search(body) or [None, ""])[1], "?")
    issues, lit, lights = [], False, False
    for _, kind, body in docs:
        if kind == "CompositeCollider2D" and EMPTY_COMPOSITE_RE.search(body):
            issues.append(f"{owner(body)}: CompositeCollider2D has no paths (saved before geometry was built; "
                          "use StudioEditorUtil.BakeTilemapColliders before SaveScene)")
        elif kind == "MonoBehaviour":
            script = (SCRIPT_RE.search(body) or [None, ""])[1]
            lights = lights or script == LIGHT2D_GUID
            unset = UNSET_RE.findall(body) if script in own_scripts else []
            if len(unset) >= 2:
                # Several refs empty at once is a builder bug, not an optional field: usually assets loaded before
                # NewScene(Single) unloaded them, or a LoadAssetAtPath path is wrong.
                issues.append(f"{owner(body)}: unassigned refs {', '.join(unset)} - wiring bug (assets loaded before NewScene? "
                              "use StudioEditorUtil.BuildScene; check the [Wire] errors in console.log)")
            elif unset:
                issues.append(f"{owner(body)}: unassigned ref {unset[0]} (optional by design? else wire it)")
        elif kind in ("SpriteRenderer", "TilemapRenderer") and SPRITE_LIT_GUID in body:
            lit = True
    if rel.endswith(".unity") and lit and not lights:
        issues.append("Sprite-Lit material but no Light2D in this scene: sprites render black (add a Global Light2D)")
    return [f"{rel}: {i}" for i in issues]


def main():
    args = sys.argv[1:]
    files = args[args.index("--files") + 1:] if "--files" in args else None
    root = os.path.abspath(args[0] if args and args[0] != "--files" else ".")
    if files is None:
        base = os.path.join(root, "Assets", "_Project")
        files = [os.path.relpath(p, root) for ext in ("unity", "prefab")
                 for p in glob.glob(os.path.join(base, "**", f"*.{ext}"), recursive=True)]
    own = project_script_guids(root)
    out = []
    for rel in files:
        path = os.path.join(root, rel)
        if os.path.exists(path):
            out += check(path, rel.replace("\\", "/"), own)
    out = list(dict.fromkeys(out))  # a builder run twice leaves duplicate objects
    if out:
        print("\n".join(out))
    sys.exit(1 if out else 0)


if __name__ == "__main__":
    main()
