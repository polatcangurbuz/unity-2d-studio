#!/usr/bin/env python3
"""Build .studio/PROJECT_MAP.md for a Unity project using regex only (no LLM, stdlib only).

Usage: python project_map.py [project_root] [--out PATH] [--max-lines 300] [--all] [--stdout]
"""
import argparse
import datetime
import hashlib
import json
import os
import re
import sys
from collections import Counter, defaultdict

SECTION_CAP = 25
SKIP_DIRS = {"ThirdParty", "Plugins", "Samples", "TextMesh Pro", "AddressableAssetsData"}

STRIP_RE = re.compile(
    r'//[^\n]*|/\*.*?\*/|@"(?:""|[^"])*"|\$?"(?:\\.|[^"\\\n])*"|\'(?:\\.|[^\'\\\n])\'',
    re.S,
)
NAMESPACE_RE = re.compile(r"\bnamespace\s+([\w.]+)")
TYPE_RE = re.compile(
    r"((?:\[[^\]]*\]\s*)*)"
    r"((?:(?:public|internal|private|protected|static|abstract|sealed|partial|readonly|ref|unsafe|new)\s+)*)"
    r"\b(class|struct|interface|enum|record(?:\s+(?:class|struct))?)\s+(\w+)\s*(<[^>{]*>)?"
    r"\s*(?:\(([^)]*)\))?\s*(?::\s*([^{;]+?))?\s*(?:where\s[^{]*)?\{"
)
MODS = r"(?:(?:public|private|protected|internal|static|virtual|override|abstract|sealed|async|extern|new|unsafe|readonly|const|volatile|partial|required)\s+)*"
TYPE_PAT = r"[\w.]+(?:<[^()=;]*?>)?(?:\[[,\s]*\])*\??"
METHOD_RE = re.compile(rf"^({MODS})({TYPE_PAT})\s+(\w+)\s*(?:<[^()=;]*>)?\s*\(([^)]*)\)")
CTOR_RE = re.compile(rf"^({MODS})(\w+)\s*\(([^)]*)\)")
PROP_RE = re.compile(rf"^({MODS})({TYPE_PAT})\s+(\w+)\s*(\{{\}}|=>)")
EVENT_RE = re.compile(rf"^({MODS})event\s+({TYPE_PAT})\s+(\w+)")
FIELD_RE = re.compile(rf"^({MODS})({TYPE_PAT})\s+(\w+)\s*(?:=|,|$)")
ATTR_RE = re.compile(r"^\s*\[([^\]]*)\]\s*")
IDENT_RE = re.compile(r"\b[A-Z]\w*\b")
DEBT_RE = re.compile(r"//\s*DEBT:")
GUID_RE = re.compile(r"^guid:\s*([0-9a-f]{32})", re.M)
MSCRIPT_RE = re.compile(r"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-f]{32})")
KEYWORDS = {"if", "for", "foreach", "while", "switch", "return", "using", "lock", "catch", "fixed", "new", "throw", "base", "this"}


def rel(path, root):
    return os.path.relpath(path, root).replace("\\", "/")


def walk(assets, include_all, exts):
    for dirpath, dirnames, filenames in os.walk(assets):
        if not include_all:
            dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS and not d.startswith(".")]
        for f in filenames:
            if f.endswith(exts):
                yield os.path.join(dirpath, f)


def read(path, limit=None):
    try:
        with open(path, encoding="utf-8-sig", errors="replace") as fh:
            return fh.read(limit) if limit else fh.read()
    except OSError:
        return ""


def strip_code(src):
    # Newlines survive stripping so callers can report real line numbers.
    src = STRIP_RE.sub(lambda m: ('""' if m.group(0)[:1] in '"@$' else " ") + "\n" * m.group(0).count("\n"), src)
    return re.sub(r"^[ \t]*#.*$", "", src, flags=re.M)


def match_brace(text, open_idx):
    depth = 0
    for i in range(open_idx, len(text)):
        c = text[i]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return i
    return len(text) - 1


def top_level(body):
    """Collapse every nested {...} to {} so only this type's own declarations remain."""
    out, depth = [], 0
    for c in body:
        if c == "{":
            if depth == 0:
                out.append("{")
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                out.append("}")
        elif depth == 0 or c == "\n":
            out.append(c)
    return "".join(out)


def split_params(params):
    parts, depth, cur = [], 0, ""
    for c in params:
        if c in "<([":
            depth += 1
        elif c in ">)]":
            depth -= 1
        if c == "," and depth == 0:
            parts.append(cur)
            cur = ""
        else:
            cur += c
    if cur.strip():
        parts.append(cur)
    types = []
    for p in parts:
        p = re.sub(r"\[[^\]]*\]", "", p.split("=")[0]).strip()
        p = re.sub(r"^(this|params)\s+", "", p)
        words = p.rsplit(None, 1)
        types.append(words[0] if len(words) == 2 else p)
    return ",".join(types)


def parse_members(body, type_name, kind):
    pub, ser = [], []
    if kind == "enum":
        values = [v.split("=")[0].strip() for v in top_level(body).split(",")]
        return [v for v in values if v][:10], []
    interface = kind == "interface"
    chunks = re.split(r";|\{\s*\}", top_level(body))
    for chunk in chunks:
        decl = " ".join(chunk.split())
        attrs = []
        while True:
            m = ATTR_RE.match(decl)
            if not m:
                break
            attrs.append(m.group(1))
            decl = decl[m.end():]
        if not decl or re.search(r"\b(class|struct|interface|enum|record|delegate)\b", decl.split("(")[0]):
            continue
        serialized = any("SerializeField" in a or "SerializeReference" in a for a in attrs)
        m = EVENT_RE.match(decl)
        if m:
            entry, mods = f"{m.group(3)}:event {m.group(2)}", m.group(1)
        elif (m := METHOD_RE.match(decl)) and m.group(3) not in KEYWORDS and m.group(2) not in KEYWORDS:
            entry, mods = f"{m.group(3)}({split_params(m.group(4))})", m.group(1)
            if m.group(2) != "void":
                entry += f":{m.group(2)}"
        elif (m := CTOR_RE.match(decl)) and m.group(2) == type_name:
            entry, mods = f"new({split_params(m.group(3))})", m.group(1)
        elif m := PROP_RE.match(decl):
            entry, mods = f"{m.group(3)}:{m.group(2)}", m.group(1)
        elif m := FIELD_RE.match(decl):
            if m.group(2) in KEYWORDS or m.group(3) in KEYWORDS:
                continue
            entry, mods = f"{m.group(3)}:{m.group(2)}", m.group(1)
        else:
            continue
        if serialized:
            ser.append(entry)
        elif interface or re.search(r"\bpublic\b", mods):
            pub.append(entry)
    return pub, ser


def parse_cs(path, root):
    raw = read(path)
    src = strip_code(raw)
    ns_m = NAMESPACE_RE.search(src)
    types = []
    for m in TYPE_RE.finditer(src):
        open_idx = m.end() - 1
        body = src[open_idx + 1:match_brace(src, open_idx)]
        kind = m.group(3).split()[0]
        name = m.group(4) + (m.group(5) or "").replace(" ", "")
        bases = " ".join((m.group(7) or "").split())
        pub, ser = parse_members(body, m.group(4), kind)
        types.append({"kind": kind, "name": name, "short": m.group(4), "bases": bases, "pub": pub, "ser": ser,
                      "static": "static" in m.group(2), "idents": set(IDENT_RE.findall(bases + " " + body))})
    return {
        "path": rel(path, root),
        "ns": ns_m.group(1) if ns_m else "",
        "types": types,
        "debt": len(DEBT_RE.findall(raw)),
    }


def meta_guid(path):
    m = GUID_RE.search(read(path + ".meta", 2000))
    return m.group(1) if m else None


def collect_asmdefs(assets, root, include_all):
    """Returns (summary lines, {folder: asmdef name})."""
    defs, by_guid = [], {}
    for p in walk(assets, include_all, (".asmdef",)):
        try:
            data = json.loads(read(p))
        except ValueError:
            continue
        g = meta_guid(p)
        if g:
            by_guid[g] = data.get("name", "?")
        defs.append((data, rel(p, root)))
    lines = []
    for data, path in sorted(defs, key=lambda d: d[1]):
        refs = [by_guid.get(r[5:], r[5:13]) if r.startswith("GUID:") else r for r in data.get("references", [])]
        editor = " [Editor]" if data.get("includePlatforms") == ["Editor"] else ""
        test = " [Test]" if "UNITY_INCLUDE_TESTS" in data.get("defineConstraints", []) else ""
        lines.append(f"- {data.get('name', '?')}{editor}{test} → {', '.join(refs) or '-'}  ({os.path.dirname(path)})")
    return lines, {os.path.dirname(path): data.get("name", "?") for data, path in defs}


def collect_scenes(assets, root, include_all):
    build = read(os.path.join(root, "ProjectSettings", "EditorBuildSettings.asset"))
    build_paths = re.findall(r"path:\s*(\S+\.unity)", build)
    out = []
    for p in sorted(walk(assets, include_all, (".unity",))):
        r = rel(p, root)
        idx = build_paths.index(r) if r in build_paths else None
        out.append(f"{r}" + (f" [build {idx}]" if idx is not None else ""))
    return out


def collect_so_assets(assets, root, include_all, script_by_guid):
    groups = defaultdict(list)
    for p in walk(assets, include_all, (".asset",)):
        m = MSCRIPT_RE.search(read(p, 4000))
        if m and m.group(1) in script_by_guid:
            groups[script_by_guid[m.group(1)]].append(rel(p, root))
    lines = []
    for script, paths in sorted(groups.items()):
        paths.sort()
        shown = ", ".join(os.path.basename(x)[:-6] for x in paths[:6])
        more = f" +{len(paths) - 6}" if len(paths) > 6 else ""
        lines.append(f"- {script} ({len(paths)}): {shown}{more}  [{os.path.dirname(paths[0])}]")
    return lines


def capped(items):
    return items if len(items) <= SECTION_CAP else items[:SECTION_CAP] + [f"… +{len(items) - SECTION_CAP} more"]


def count_lines(lines):
    return "\n".join(lines).replace("\n\n\n", "\n\n").count("\n") + 1


# Types and public API are never dropped; only these extras are, in this order, to fit the line budget.
LEVEL_NOTES = {
    1: "uses: lines cut",
    2: "uses: + ser: lines cut",
    3: "uses: + ser: cut, pub inlined on the type line",
}


def module_of(path, asmdef_dirs):
    d = os.path.dirname(path)
    while d:
        if d in asmdef_dirs:
            return asmdef_dirs[d]
        d = os.path.dirname(d)
    return "Assembly-CSharp"


def render(files, header, level):
    out = []
    by_dir = defaultdict(list)
    for f in files:
        by_dir[os.path.dirname(f["path"])].append(f)
    for d in sorted(by_dir):
        out.append(f"\n### {d}")
        for f in sorted(by_dir[d], key=lambda x: x["path"]):
            for t in f["types"]:
                base = f" : {t['bases']}" if t["bases"] else ""
                debt = f"  DEBT×{f['debt']}" if f["debt"] and t is f["types"][0] else ""
                kind = "static class" if t["static"] else t["kind"]
                line = f"- {kind} {t['name']}{base} — {os.path.basename(f['path'])}{debt}"
                if level == 3:
                    out.append(line + (f" | pub: {', '.join(t['pub'])}" if t["pub"] else ""))
                    continue
                out.append(line)
                if t["pub"]:
                    out.append(f"  pub: {', '.join(t['pub'])}")
                if t["ser"] and level < 2:
                    out.append(f"  ser: {', '.join(t['ser'])}")
                if t["uses"] and level < 1:
                    out.append(f"  uses: {', '.join(t['uses'])}")
    return header + out


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("root", nargs="?", default=".")
    ap.add_argument("--out", default=None, help="default: <root>/.studio/PROJECT_MAP.md")
    ap.add_argument("--max-lines", type=int, default=300)
    ap.add_argument("--all", action="store_true", help="also scan ThirdParty/Plugins/Samples")
    ap.add_argument("--stdout", action="store_true")
    ap.add_argument("--module", help="print full detail (no budget) for one asmdef, e.g. Acme.Gameplay or Gameplay")
    ap.add_argument("--if-changed", action="store_true", help="rewrite only when the public signature hash changed (hooks)")
    args = ap.parse_args()
    # Windows consoles default to a legacy codepage that cannot print the map symbols.
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    root = os.path.abspath(args.root)
    assets = os.path.join(root, "Assets")
    if not os.path.isdir(assets):
        sys.exit(f"error: no Assets/ folder under {root}")

    cs_paths = sorted(walk(assets, args.all, (".cs",)))
    files = [parse_cs(p, root) for p in cs_paths]
    script_by_guid = {}
    for p in cs_paths:
        g = meta_guid(p)
        if g:
            script_by_guid[g] = os.path.splitext(os.path.basename(p))[0]

    names = {t["short"] for f in files for t in f["types"]}
    ref_count = Counter()
    for f in files:
        for t in f["types"]:
            t["uses"] = sorted((t.pop("idents") & names) - {t["short"]})
        ref_count.update({u for t in f["types"] for u in t["uses"]})

    asmdefs, asmdef_dirs = collect_asmdefs(assets, root, args.all)
    for f in files:
        f["module"] = module_of(f["path"], asmdef_dirs)
    modules = sorted({f["module"] for f in files})

    if args.module:
        want = args.module.lower()
        picked = [f for f in files if want in (f["module"].lower(), f["module"].lower().rsplit(".", 1)[-1])]
        if not picked:
            sys.exit(f"error: no module '{args.module}'. modules: {', '.join(modules)}")
        name = picked[0]["module"]
        head = [f"# PROJECT_MAP · module {name}", f"{len(picked)} .cs | full detail, no line budget"]
        sys.stdout.write("\n".join(render(picked, head, 0)).replace("\n\n\n", "\n\n") + "\n")
        return

    n_types = sum(len(f["types"]) for f in files)
    debt_total = sum(f["debt"] for f in files)
    out = args.out or os.path.join(root, ".studio", "PROJECT_MAP.md")
    # Inspector fields are in the hash too: a map listing stale [SerializeField]s would mislead.
    scenes = collect_scenes(assets, root, args.all)
    sig = hashlib.sha1(json.dumps(
        [[f["path"], f["ns"], [[t["kind"], t["name"], t["bases"], t["pub"], t["ser"]] for t in f["types"]]] for f in files]
        + [asmdefs, scenes], sort_keys=True).encode()).hexdigest()[:12]
    if args.if_changed and os.path.exists(out) and f"sig {sig}" in read(out, 600):
        return
    header = [
        "# PROJECT_MAP",
        f"generated {datetime.datetime.now():%Y-%m-%d %H:%M} by project_map.py | {len(files)} .cs | {n_types} types"
        + (f" | DEBT markers: {debt_total}" if debt_total else "") + f" | sig {sig}",
        "Read this before opening sources. pub=public API, ser=[SerializeField], uses=project types referenced.",
    ]
    if asmdefs:
        header += ["\n## Assemblies"] + asmdefs
    if scenes:
        header += ["\n## Scenes"] + [f"- {s}" for s in capped(scenes)]
    so = collect_so_assets(assets, root, args.all, script_by_guid)
    if so:
        header += ["\n## Data assets (by script)"] + capped(so)
    top = [f"{n}({c})" for n, c in ref_count.most_common(12) if c > 1]
    if top:
        header += ["\n## Most referenced", ", ".join(top)]
    header.append("\n## Types (by folder)")

    level = 0
    lines = render(files, header, level)
    while count_lines(lines) > args.max_lines and level < 3:
        level += 1
        lines = render(files, header, level)
    if level:
        lines.insert(3, f"(compressed: {LEVEL_NOTES[level]}; all types + public API kept)")
        lines.append(f"\ndetail: python <skill>/scripts/project_map.py --module <name>"
                     f"  (modules: {', '.join(modules)})")
    if count_lines(lines) > args.max_lines:
        lines.insert(4, f"(over budget: {count_lines(lines)} > {args.max_lines} lines; consider graphify for this size)")

    text = "\n".join(lines).replace("\n\n\n", "\n\n") + "\n"
    if args.stdout:
        sys.stdout.write(text)
        return
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    print(f"wrote {out} ({text.count(chr(10))} lines, {len(files)} files, {n_types} types)")


if __name__ == "__main__":
    main()
