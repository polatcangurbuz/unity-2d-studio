#!/usr/bin/env python3
"""Copy the Unity Editor tools shipped with the skill into a Unity project. Never overwrites existing files.

  python install_unity.py [project_root]
      -> Assets/_Project/Code/Editor/{StudioSetup,SpriteImportRules,VfxBuilder}.cs
         Assets/_Project/Code/Editor/Templates~/{gitignore,gitattributes}.txt (read by Tools > Studio > Setup Project;
         Unity ignores folders ending in ~)
"""
import glob
import os
import shutil
import sys

SKILL = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EDITOR = os.path.join("Assets", "_Project", "Code", "Editor")


def main():
    root = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else ".")
    if not os.path.isdir(os.path.join(root, "Assets")):
        sys.exit(f"not a Unity project (no Assets/): {root}")
    dst = os.path.join(root, EDITOR)
    pairs = [(f, os.path.join(dst, os.path.basename(f)))
             for f in sorted(glob.glob(os.path.join(SKILL, "assets", "unity", "Editor", "*.cs")))]
    pairs += [(os.path.join(SKILL, "assets", n), os.path.join(dst, "Templates~", n)) for n in ("gitignore.txt", "gitattributes.txt")]
    copied, kept = [], []
    for src, target in pairs:
        if os.path.exists(target):
            kept.append(os.path.basename(target))
            continue
        os.makedirs(os.path.dirname(target), exist_ok=True)
        shutil.copyfile(src, target)
        copied.append(os.path.basename(target))
    print(f"{EDITOR.replace(os.sep, '/')}: copied {', '.join(copied) or '-'}; kept {', '.join(kept) or '-'}")


if __name__ == "__main__":
    main()
