#!/usr/bin/env python3
"""Create today's devlog skeleton so the model only writes the bullets.

  python devlog.py <agent>              -> .studio/devlog/YYYY-MM-DD-<agent>.md (done/decisions/next/risks)
  python devlog.py producer --summary   -> first pre-fills a log for every agent with a handoff dated that day
                                          (done/next/risks from the handoff; decisions left for the model), then prints
                                          every agent log of the day and creates .studio/devlog/YYYY-MM-DD.md.
                                          Files/API a handoff lists but the project lacks become an UNVERIFIED risk, so claims are checked.
  python devlog.py --check-refs A.md B.md -> every `file:line` ref whose file is missing or shorter gets "(UNVERIFIED …)" appended
                                          in place; prints them, exit 1 if any (Stop hook runs it on edited .studio notes).
Options: --date YYYY-MM-DD, --root PROJECT_ROOT
"""
import argparse
import datetime
import glob
import os
import re
import sys

AGENT_TEMPLATE = "# {date} · {agent}\ndone:\n- \ndecisions:\n- \nnext:\n- \nrisks:\n- \n"
DAY_TEMPLATE = "# {date} · studio\nprogress: \ndecisions: \nblockers: \nrisks: \ntomorrow: \n"
MAX_AGENT_LINES = 12


def write_if_missing(path, text):
    if os.path.exists(path):
        print(f"exists: {path}")
        return
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    print(f"created: {path}")


def expand(spec):
    m = re.search(r"\{([^}]*)\}", spec)
    return [x for part in m.group(1).split(",") for x in expand(spec[:m.start()] + part.strip() + spec[m.end():])] if m else [spec]


def missing_files(root, files_field):
    """Paths from a handoff 'files:' line that exist neither from the root nor under Assets/_Project."""
    missing = []
    for item in re.split(r"\s*·\s*|;\s*", files_field):
        spec = re.sub(r"\s*\(.*?\)\s*$", "", item).strip()
        if not spec or " " in spec or not re.search(r"\.\w+(\}|$)", spec):
            continue
        for path in expand(spec):
            if not any(os.path.exists(os.path.join(root, base, path)) for base in ("", "Assets/_Project", "Assets")):
                missing.append(path)
    return missing


def missing_api(root, api_field):
    """Type.Member names from a handoff 'api:' line whose member appears in no project .cs file."""
    names = {m.group(2) for m in re.finditer(r"\b([A-Z]\w*)\.([A-Z]\w*)", api_field)}
    if not names:
        return []
    code = "".join(open(p, encoding="utf-8", errors="replace").read()
                   for p in glob.glob(os.path.join(root, "Assets", "**", "*.cs"), recursive=True))
    return sorted(n for n in names if not re.search(rf"\b{n}\b", code))


REF_RE = re.compile(r"`?([\w./\\-]+\.(?:cs|md|asset|prefab|unity|json|ya?ml|shader|shadergraph|asmdef|txt|py)):(\d+)(?:-(\d+))?`?(?! \(UNVERIFIED)")
SKIP_DIRS = {"Library", "Temp", "Logs", "obj", ".git", ".claude"}


def resolve(root, rel):
    for base in ("", "Assets/_Project", "Assets"):
        if os.path.isfile(os.path.join(root, base, rel)):
            return os.path.join(root, base, rel)
    name = os.path.basename(rel)
    for d, dirs, files in os.walk(root):
        dirs[:] = [x for x in dirs if x not in SKIP_DIRS]
        if name in files:
            return os.path.join(d, name)
    return None


def bad_refs(root, text):
    """(ref text, reason) for every file:line ref whose file is missing or has fewer lines."""
    bad = []
    for m in REF_RE.finditer(text):
        path = resolve(root, m.group(1).replace("\\", "/"))
        line = int(m.group(3) or m.group(2))
        if not path:
            bad.append((m.group(0), "file not in project"))
            continue
        with open(path, encoding="utf-8", errors="replace") as fh:
            count = sum(1 for _ in fh)
        if line > count:
            bad.append((m.group(0), f"file has {count} lines"))
    return bad


def check_refs(root, paths):
    found = 0
    for p in paths:
        if not os.path.isfile(p):
            continue
        text = open(p, encoding="utf-8").read()
        bad = bad_refs(root, text)
        for ref, why in dict(bad).items():
            text = re.sub(re.escape(ref) + r"(?! \(UNVERIFIED)", f"{ref} (UNVERIFIED: {why})", text)
            print(f"{os.path.relpath(p, root)}: {ref.strip('`')} -> UNVERIFIED ({why})")
            found += 1
        if bad:
            open(p, "w", encoding="utf-8", newline="\n").write(text)
    return found


def handoff_logs(root, date):
    """agent -> pre-filled log text, from handoffs whose header (or mtime) is that day."""
    by_agent = {}
    for path in sorted(glob.glob(os.path.join(root, ".studio", "handoff", "*.md"))):
        lines = open(path, encoding="utf-8").read().splitlines()
        head = lines[0] if lines else ""
        stamp = re.search(r"\d{4}-\d{2}-\d{2}", head)
        day = stamp.group(0) if stamp else datetime.date.fromtimestamp(os.path.getmtime(path)).isoformat()
        parts = [p.strip() for p in head.lstrip("# ").split("·")]
        if day != date or len(parts) < 2:
            continue
        task, agent = parts[0], re.sub(r"[^a-z0-9-]", "", parts[1].lower())
        field = {m.group(1): m.group(2).strip() for m in (re.match(r"^(goal|next|open|files|api):\s*(.*)$", l) for l in lines) if m}
        entry = by_agent.setdefault(agent or "agent", {"done": [], "next": [], "risks": []})
        gone = missing_files(root, field.get("files", "")) + missing_api(root, field.get("api", ""))
        unverified = gone or bad_refs(root, "\n".join(lines))
        entry["done"].append(f"{task}: {field.get('goal', '')}".strip() + (" (UNVERIFIED)" if unverified else ""))
        if field.get("next"):
            entry["next"].append(field["next"])
        # One risk line per task keeps a multi-task log within MAX_AGENT_LINES.
        risk = [field["open"]] if field.get("open") and field["open"].lower() not in ("-", "none", "no") else []
        if gone:
            risk.append(f"UNVERIFIED, not in project: {', '.join(os.path.basename(g) for g in gone[:4])}")
        refs = bad_refs(root, "\n".join(lines))
        if refs:
            # Same "(UNVERIFIED: …)" form the Stop hook writes, so the hook does not mark it twice.
            risk.append("refs " + ", ".join(f"{r.strip('`')} (UNVERIFIED: {why})" for r, why in refs[:3]))
        if risk:
            entry["risks"].append(f"{task.split()[0]}: {'; '.join(risk)}")
    return {a: "\n".join([f"# {date} · {a}", "done:"] + [f"- {x}" for x in e["done"]] + ["decisions:", "- ", "next:"]
                          + [f"- {x}" for x in e["next"] or [""]] + ["risks:"] + [f"- {x}" for x in e["risks"] or [""]]) + "\n"
            for a, e in by_agent.items()}


def main():
    ap = argparse.ArgumentParser(description="devlog skeletons")
    ap.add_argument("agent", nargs="?")
    ap.add_argument("--check-refs", nargs="+", metavar="MD", help="mark file:line refs that do not exist")
    ap.add_argument("--summary", action="store_true", help="producer end-of-day mode")
    ap.add_argument("--date", default=datetime.date.today().isoformat())
    ap.add_argument("--root", default=".")
    args = ap.parse_args()
    # Windows consoles default to a legacy codepage that cannot print the map symbols.
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    if args.check_refs:
        sys.exit(1 if check_refs(args.root, args.check_refs) else 0)
    if not args.agent or not re.fullmatch(r"[a-z0-9][a-z0-9-]*", args.agent):
        sys.exit("error: agent name must be lowercase letters, digits, dashes")
    if not re.fullmatch(r"\d{4}-\d{2}-\d{2}", args.date):
        sys.exit("error: --date must be YYYY-MM-DD")

    folder = os.path.join(args.root, ".studio", "devlog")
    os.makedirs(folder, exist_ok=True)

    if not args.summary:
        write_if_missing(os.path.join(folder, f"{args.date}-{args.agent}.md"),
                         AGENT_TEMPLATE.format(date=args.date, agent=args.agent))
        return

    for agent, text in handoff_logs(args.root, args.date).items():
        write_if_missing(os.path.join(folder, f"{args.date}-{agent}.md"), text)
    logs = sorted(glob.glob(os.path.join(folder, f"{args.date}-*.md")))
    if not logs:
        print(f"no agent devlogs for {args.date}")
    for path in logs:
        with open(path, encoding="utf-8") as fh:
            body = fh.read().strip()
        content = [l for l in body.splitlines()[1:] if l.strip() not in ("", "-")]
        warn = f"  (!) {len(content)} lines > {MAX_AGENT_LINES}" if len(content) > MAX_AGENT_LINES else ""
        print(f"=== {os.path.basename(path)}{warn}\n{body}\n")
    write_if_missing(os.path.join(folder, f"{args.date}.md"), DAY_TEMPLATE.format(date=args.date))


if __name__ == "__main__":
    main()
