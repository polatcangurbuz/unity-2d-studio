#!/usr/bin/env python3
"""Copy the Unity Editor tools (and optional game packs) shipped with the skill into a Unity project. Never overwrites.

  python install_unity.py [project_root]
      -> Assets/_Project/Code/Editor/*.cs (Studio tools) + Templates~/{gitignore,gitattributes}.txt
         (read by Tools > Studio > Setup Project; Unity ignores folders ending in ~)
  python install_unity.py [project_root] --pack match3
      -> also Assets/_Project/Code/Puzzle/{Runtime,Tests,View,Editor} with __NS__ replaced by the project namespace
         (`ns:` in .studio/brief.md) and example levels in Assets/_Project/Data/Levels/
"""
import argparse
import glob
import os
import shutil
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from asmdefs import namespace  # noqa: E402

SKILL = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EDITOR = os.path.join("Assets", "_Project", "Code", "Editor")
PACKS = {"match3": ("Puzzle", ("Runtime", "Tests", "View", "Editor"))}
TOKEN = "__NS__"


def copy(pairs, copied, kept, ns=None):
    for src, target in pairs:
        if os.path.exists(target):
            kept.append(os.path.basename(target))
            continue
        os.makedirs(os.path.dirname(target), exist_ok=True)
        if ns is None:
            shutil.copyfile(src, target)
        else:
            with open(src, encoding="utf-8") as fh:
                text = fh.read().replace(TOKEN, ns)
            with open(target, "w", encoding="utf-8", newline="\n") as fh:
                fh.write(text)
        copied.append(os.path.basename(target))


def pack_pairs(root, name, ns):
    folder, parts = PACKS[name]
    base = os.path.join(SKILL, "assets", "packs", name)
    dst = os.path.join(root, "Assets", "_Project", "Code", folder)
    pairs = []
    for part in parts:
        for src in sorted(glob.glob(os.path.join(base, part, "*"))):
            if os.path.isfile(src):
                pairs.append((src, os.path.join(dst, part, os.path.basename(src).replace(TOKEN, ns))))
    levels = [(f, os.path.join(root, "Assets", "_Project", "Data", "Levels", os.path.basename(f)))
              for f in sorted(glob.glob(os.path.join(base, "Levels~", "*.txt")))]
    return pairs, levels


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("root", nargs="?", default=".")
    ap.add_argument("--pack", choices=sorted(PACKS), help="also install a game pack")
    args = ap.parse_args()
    root = os.path.abspath(args.root)
    if not os.path.isdir(os.path.join(root, "Assets")):
        sys.exit(f"not a Unity project (no Assets/): {root}")
    dst = os.path.join(root, EDITOR)
    pairs = [(f, os.path.join(dst, os.path.basename(f)))
             for f in sorted(glob.glob(os.path.join(SKILL, "assets", "unity", "Editor", "*.cs")))]
    pairs += [(os.path.join(SKILL, "assets", n), os.path.join(dst, "Templates~", n)) for n in ("gitignore.txt", "gitattributes.txt")]
    copied, kept = [], []
    copy(pairs, copied, kept)
    print(f"{EDITOR.replace(os.sep, '/')}: copied {', '.join(copied) or '-'}; kept {', '.join(kept) or '-'}")
    if args.pack:
        ns = namespace(root)
        code, levels = pack_pairs(root, args.pack, ns)
        copied, kept = [], []
        copy(code, copied, kept, ns)
        copy(levels, copied, kept)
        print(f"pack {args.pack} (ns {ns}): copied {len(copied)} file(s); kept {len(kept)} existing"
              + (f" ({', '.join(kept[:6])}{'…' if len(kept) > 6 else ''})" if kept else ""))


if __name__ == "__main__":
    main()
