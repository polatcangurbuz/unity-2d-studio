#!/usr/bin/env python3
"""Create the studio asmdefs without waiting for the Setup click (same layout and JSON as StudioAsmdefs.cs).

  python asmdefs.py [project_root]
      -> Assets/_Project/Code/{Core,Gameplay,UI,Editor,Tests/EditMode,Tests/PlayMode}/<ns>.<Module>.asmdef
Namespace: `ns:` in .studio/brief.md, else company + product from ProjectSettings. Package references follow
Packages/manifest.json (Input System, uGUI/TMP, URP 2D, test framework). Existing asmdefs are kept; only missing
package references are added to them.
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
    ("com.unity.test-framework", "UnityEngine.TestRunner", ("Editor",)),
    ("com.unity.test-framework", "UnityEditor.TestRunner", ("Editor",)),
]
TEST_RUNNER_DEFINE = {"name": "com.unity.test-framework", "expression": "", "define": "STUDIO_TEST_RUNNER"}


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
    refs = [ref for pkg, ref, modules in PACKAGE_REFS if pkg in packages and module in modules]
    ugui2 = not packages.get("com.unity.ugui", "1.").startswith("1.")
    if module in ("UI", "Editor") and (ugui2 or "com.unity.textmeshpro" in packages):
        refs.append("Unity.TextMeshPro")
    return refs


def write(root, module, ns, own, platforms, is_test, packages, log):
    folder = os.path.join(root, "Assets", "_Project", "Code", *module.split("/"))
    os.makedirs(folder, exist_ok=True)
    extra = [] if is_test else package_refs(module, packages)
    test_runner = module == "Editor" and "com.unity.test-framework" in packages
    existing = sorted(f for f in os.listdir(folder) if f.endswith(".asmdef"))
    if existing:
        path = os.path.join(folder, existing[0])
        data = json.load(open(path, encoding="utf-8-sig"))
        refs = data.setdefault("references", [])
        missing = [r for r in extra if r not in refs]
        refs += missing
        defines = data.setdefault("versionDefines", [])
        add_define = test_runner and not any(d.get("define") == TEST_RUNNER_DEFINE["define"] for d in defines)
        if add_define:
            defines.append(dict(TEST_RUNNER_DEFINE))
        if missing or add_define:
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
        "versionDefines": [dict(TEST_RUNNER_DEFINE)] if test_runner else [],
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
    manifest = os.path.join(root, "Packages", "manifest.json")
    packages = json.load(open(manifest, encoding="utf-8-sig")).get("dependencies", {}) if os.path.exists(manifest) else {}
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
