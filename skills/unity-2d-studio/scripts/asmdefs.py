#!/usr/bin/env python3
"""Create the studio asmdefs without waiting for the Setup click (same layout and JSON as StudioAsmdefs.cs).

  python asmdefs.py [project_root]
      -> Assets/_Project/Code/{Core,Gameplay,UI,Editor,Tests/EditMode,Tests/PlayMode}/<ns>.<Module>.asmdef
Namespace: `ns:` in .studio/brief.md, else company + product from ProjectSettings. Package references follow
Packages/manifest.json (Input System, uGUI/TMP, URP 2D, Cinemachine 3, 2D Animation, test framework). The Editor asmdef
gets STUDIO_* version defines, so Studio tools for optional packages compile only when the package is there. Existing
asmdefs are kept; only missing package references and defines are added to them.
"""
import json
import os
import re
import sys

PACKAGE_REFS = [
    ("com.unity.inputsystem", "Unity.InputSystem", ("Gameplay", "UI")),
    ("com.unity.ugui", "UnityEngine.UI", ("UI", "Editor")),
    ("com.unity.render-pipelines.universal", "Unity.RenderPipelines.Core.Runtime", ("Gameplay", "Editor")),
    ("com.unity.render-pipelines.universal", "Unity.RenderPipelines.Universal.Runtime", ("Gameplay", "Editor")),
    ("com.unity.render-pipelines.universal", "Unity.RenderPipelines.Universal.2D.Runtime", ("Gameplay", "Editor")),
    ("com.unity.cinemachine", "Unity.Cinemachine", ("Gameplay", "Editor")),
    ("com.unity.2d.animation", "Unity.2D.Animation.Runtime", ("Gameplay",)),
    ("com.unity.test-framework", "UnityEngine.TestRunner", ("Editor",)),
    ("com.unity.test-framework", "UnityEditor.TestRunner", ("Editor",)),
]
# Editor asmdef only; expression "" = any version, "3.0.0" = 3.0.0 or newer. Same list as StudioAsmdefs.cs.
EDITOR_DEFINES = [
    {"name": "com.unity.test-framework", "expression": "", "define": "STUDIO_TEST_RUNNER"},
    {"name": "com.unity.ugui", "expression": "", "define": "STUDIO_UGUI"},
    {"name": "com.unity.ugui", "expression": "2.0.0", "define": "STUDIO_TMP"},
    {"name": "com.unity.textmeshpro", "expression": "", "define": "STUDIO_TMP"},
    {"name": "com.unity.render-pipelines.universal", "expression": "", "define": "STUDIO_URP"},
    {"name": "com.unity.cinemachine", "expression": "3.0.0", "define": "STUDIO_CINEMACHINE"},
]


def read_packages(root):
    """manifest.json + packages-lock.json (feature packages pull in e.g. 2D Animation as a dependency); manifest wins."""
    packages = {}
    lock = os.path.join(root, "Packages", "packages-lock.json")
    if os.path.exists(lock):
        for name, entry in json.load(open(lock, encoding="utf-8-sig")).get("dependencies", {}).items():
            packages[name] = entry.get("version", "") if isinstance(entry, dict) else str(entry)
    manifest = os.path.join(root, "Packages", "manifest.json")
    if os.path.exists(manifest):
        packages.update(json.load(open(manifest, encoding="utf-8-sig")).get("dependencies", {}))
    return packages


def namespace(root):
    brief = os.path.join(root, ".studio", "brief.md")
    if os.path.exists(brief):
        m = re.search(r"\bns:\s*([A-Za-z_][\w.]*)", open(brief, encoding="utf-8").read())
        if m:
            return m.group(1)
    settings = os.path.join(root, "ProjectSettings", "ProjectSettings.asset")
    text = open(settings, encoding="utf-8", errors="replace").read() if os.path.exists(settings) else ""

    def part(key, default):
        m = re.search(rf"^\s*{key}:\s*(.*)$", text, re.M)
        clean = re.sub(r"[^A-Za-z0-9]", "", m.group(1) if m else "")
        return default if not clean else ("Game" + clean if clean[0].isdigit() else clean)
    return f"{part('companyName', 'Studio')}.{part('productName', 'Game')}"


def package_refs(module, packages):
    # Cinemachine 2.x has other assembly and type names; the studio targets 3.x only.
    cm2 = packages.get("com.unity.cinemachine", "3.").startswith(("0.", "1.", "2."))
    refs = [ref for pkg, ref, modules in PACKAGE_REFS
            if pkg in packages and module in modules and not (cm2 and ref == "Unity.Cinemachine")]
    ugui2 = not packages.get("com.unity.ugui", "1.").startswith("1.")
    if module in ("UI", "Editor") and (ugui2 or "com.unity.textmeshpro" in packages):
        refs.append("Unity.TextMeshPro")
    return refs


def write(root, module, ns, own, platforms, is_test, packages, log):
    folder = os.path.join(root, "Assets", "_Project", "Code", *module.split("/"))
    os.makedirs(folder, exist_ok=True)
    extra = [] if is_test else package_refs(module, packages)
    wanted = EDITOR_DEFINES if module == "Editor" else []
    existing = sorted(f for f in os.listdir(folder) if f.endswith(".asmdef"))
    if existing:
        path = os.path.join(folder, existing[0])
        data = json.load(open(path, encoding="utf-8-sig"))
        refs = data.setdefault("references", [])
        missing = [r for r in extra if r not in refs]
        refs += missing
        defines = data.setdefault("versionDefines", [])
        have = {(d.get("name"), d.get("define")) for d in defines}
        new_defines = [dict(d) for d in wanted if (d["name"], d["define"]) not in have]
        defines += new_defines
        if missing or new_defines:
            dump(path, data)
            log.append(f"* {existing[0]}" + (f" + {', '.join(missing)}" if missing else ""))
        return
    name = f"{ns}.{module.replace('/', '.')}"
    data = {
        "name": name,
        "rootNamespace": f"{ns}.Tests" if is_test else name,
        "references": own + extra,
        "includePlatforms": platforms,
        "excludePlatforms": [],
        "allowUnsafeCode": False,
        "overrideReferences": is_test,
        "precompiledReferences": ["nunit.framework.dll"] if is_test else [],
        "autoReferenced": not is_test,
        "defineConstraints": ["UNITY_INCLUDE_TESTS"] if is_test else [],
        "versionDefines": [dict(d) for d in wanted],
        "noEngineReferences": False,
    }
    dump(os.path.join(folder, name + ".asmdef"), data)
    log.append(f"+ {name}.asmdef" + (f" (+ {', '.join(extra)})" if extra else ""))


def dump(path, data):
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(json.dumps(data, indent=4) + "\n")


def main():
    root = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else ".")
    if not os.path.isdir(os.path.join(root, "Assets")):
        sys.exit(f"not a Unity project (no Assets/): {root}")
    packages = read_packages(root)
    ns, log = namespace(root), []
    tests = [f"{ns}.Core", f"{ns}.Gameplay", "UnityEngine.TestRunner"]
    write(root, "Core", ns, [], [], False, packages, log)
    write(root, "Gameplay", ns, [f"{ns}.Core"], [], False, packages, log)
    write(root, "UI", ns, [f"{ns}.Core", f"{ns}.Gameplay"], [], False, packages, log)
    write(root, "Editor", ns, [f"{ns}.Core", f"{ns}.Gameplay", f"{ns}.UI"], ["Editor"], False, packages, log)
    if "com.unity.test-framework" in packages:
        write(root, "Tests/EditMode", ns, tests + ["UnityEditor.TestRunner"], ["Editor"], True, packages, log)
        write(root, "Tests/PlayMode", ns, tests, [], True, packages, log)
    else:
        log.append("! tests asmdefs skipped: com.unity.test-framework not in manifest (Setup click adds it)")
    print(f"asmdefs ns={ns}: " + ("; ".join(log) or "all present, nothing missing"))


if __name__ == "__main__":
    main()
