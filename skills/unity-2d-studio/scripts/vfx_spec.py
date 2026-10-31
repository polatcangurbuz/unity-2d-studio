#!/usr/bin/env python3
"""VFX specs as data: the model picks a preset and palette roles, the script writes and converts the spec.

  python vfx_spec.py new <name> --preset burst|explosion|hit|pickup|dust|trail [--colors roleA,roleB] [--trigger ChannelAsset]
      -> .studio/vfx/<name>.yaml (layers with palette roles, never raw hex; edit numbers there if needed)
  python vfx_spec.py build [name ...]
      -> resolves roles via .studio/art/art-bible.md palette → Assets/_Project/Art/VFX/Specs/<name>.vfx.json
         then Unity: Tools > Studio > Build VFX From Specs (VfxBuilder.cs) makes Prefabs/VFX/<name>.prefab
Options: --root PROJECT_ROOT
"""
import argparse
import json
import os
import re
import sys

sys.dont_write_bytecode = True  # plugin dir may be a read-only cache: no __pycache__ there
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from palette import read_palette  # noqa: E402

LAYER_KEYS = {
    "name": str, "shape": str, "radius": float, "angle": float, "burst": int, "rate": float, "lifetime": str, "speed": str,
    "size": str, "size_over_life": str, "color": str, "fade": bool, "gravity": float, "blend": str, "texture": str,
    "noise": float, "delay": float,
}
SHAPES, BLENDS, TEXTURES, SIZE_CURVES = ("circle", "cone", "edge", "point"), ("alpha", "additive"), ("pixel", "soft"), ("none", "shrink", "grow")
# a/b = first/second --colors role; numbers are world units and seconds at PPU-independent scale.
PRESETS = {
    "burst": [dict(name="core", shape="circle", radius=0.1, burst=16, lifetime="0.2-0.4", speed="2-4", size="0.06-0.12",
                   size_over_life="shrink", color="a>b", blend="alpha")],
    "explosion": [
        dict(name="flash", shape="point", burst=1, lifetime="0.06-0.1", speed="0-0", size="1.2-1.4", color="b>b", blend="additive", texture="soft"),
        dict(name="fire", shape="circle", radius=0.15, burst=24, lifetime="0.25-0.45", speed="2-5", size="0.15-0.3",
             size_over_life="shrink", color="b>a", blend="additive"),
        dict(name="smoke", shape="circle", radius=0.2, burst=10, lifetime="0.5-0.8", speed="0.5-1.2", size="0.25-0.4",
             size_over_life="grow", color="midground>background", blend="alpha", noise=0.3, delay=0.05),
        dict(name="sparks", shape="circle", radius=0.05, burst=12, lifetime="0.3-0.6", speed="5-8", size="0.04-0.06",
             color="b>a", gravity=1.0, blend="additive"),
    ],
    "hit": [dict(name="spark", shape="cone", angle=35, burst=8, lifetime="0.1-0.2", speed="3-6", size="0.05-0.08",
                 size_over_life="shrink", color="a>b", blend="additive")],
    "pickup": [
        dict(name="ring", shape="circle", radius=0.3, burst=12, lifetime="0.3-0.4", speed="0.5-1", size="0.06-0.08", color="a>a", blend="additive"),
        dict(name="rise", shape="circle", radius=0.2, burst=6, lifetime="0.4-0.6", speed="1-2", size="0.04-0.06", color="a>b", gravity=-0.3, blend="alpha"),
    ],
    "dust": [dict(name="puff", shape="edge", radius=0.3, burst=6, lifetime="0.25-0.4", speed="0.3-0.8", size="0.08-0.14",
                  size_over_life="grow", color="a>background", blend="alpha")],
    "trail": [dict(name="trail", shape="point", rate=30, lifetime="0.15-0.25", speed="0-0.2", size="0.06-0.1",
                   size_over_life="shrink", color="a>b", blend="alpha")],
}
DEFAULT_LAYER = dict(radius=0.1, angle=25, burst=0, rate=0, size_over_life="none", fade=True, gravity=0, texture="pixel", noise=0, delay=0)


def dump_yaml(spec):
    lines = [f"{k}: {v}" for k, v in spec.items() if k != "layers"] + ["layers:"]
    for layer in spec["layers"]:
        for i, (k, v) in enumerate(layer.items()):
            lines.append(f"{'  - ' if i == 0 else '    '}{k}: {str(v).lower() if isinstance(v, bool) else v}")
    return "\n".join(lines) + "\n"


def load_yaml(path):
    spec, layers = {}, []
    for n, raw in enumerate(open(path, encoding="utf-8"), 1):
        line = raw.split("#", 1)[0].rstrip()
        if not line.strip():
            continue
        m = re.match(r"^(\s*)(- )?([a-z_]+):\s*(.*)$", line)
        if not m:
            sys.exit(f"{path}:{n}: cannot parse '{line.strip()}'")
        indent, dash, key, value = m.groups()
        if not indent and not dash:
            if key != "layers":
                spec[key] = value
        elif dash:
            layers.append({key: value})
        elif layers:
            layers[-1][key] = value
        else:
            sys.exit(f"{path}:{n}: layer field before '- name:'")
    spec["layers"] = layers
    return spec


def span(value, path, key):
    m = re.fullmatch(r"(-?[\d.]+)(?:-(-?[\d.]+))?", str(value).strip())
    if not m:
        sys.exit(f"{path}: {key} '{value}' must be N or MIN-MAX")
    lo = float(m.group(1))
    return lo, float(m.group(2)) if m.group(2) else lo


def resolve(role, palette, path):
    role = role.strip()
    if re.fullmatch(r"#[0-9a-fA-F]{6}", role):
        sys.exit(f"{path}: raw hex {role}; use a palette role so colours stay on-palette")
    if role not in palette:
        sys.exit(f"{path}: role '{role}' not in palette ({', '.join(palette)})")
    return palette[role]


def to_json(spec, palette, path):
    layers, end = [], 0.0
    for layer in spec["layers"]:
        unknown = set(layer) - set(LAYER_KEYS)
        if unknown:
            sys.exit(f"{path}: unknown layer keys {sorted(unknown)}")
        merged = {**DEFAULT_LAYER, **layer}
        for key, allowed in (("shape", SHAPES), ("blend", BLENDS), ("texture", TEXTURES), ("size_over_life", SIZE_CURVES)):
            if str(merged.get(key, allowed[0])) not in allowed:
                sys.exit(f"{path}: {merged.get('name')}: {key} must be one of {allowed}")
        lt, sp, sz = (span(merged[k], path, k) for k in ("lifetime", "speed", "size"))
        start, _, finish = str(merged["color"]).partition(">")
        out = {
            "name": merged["name"], "shape": merged["shape"], "radius": float(merged["radius"]), "angle": float(merged["angle"]),
            "burst": int(merged["burst"]), "rate": float(merged["rate"]), "lifetimeMin": lt[0], "lifetimeMax": lt[1],
            "speedMin": sp[0], "speedMax": sp[1], "sizeMin": sz[0], "sizeMax": sz[1], "sizeOverLife": merged["size_over_life"],
            "colorStart": resolve(start, palette, path), "colorEnd": resolve(finish or start, palette, path),
            "fade": str(merged["fade"]).lower() == "true", "gravity": float(merged["gravity"]), "blend": merged["blend"],
            "texture": merged["texture"], "noise": float(merged["noise"]), "delay": float(merged["delay"]),
        }
        if out["burst"] <= 0 and out["rate"] <= 0:
            sys.exit(f"{path}: {out['name']}: needs burst > 0 or rate > 0")
        end = max(end, out["delay"] + out["lifetimeMax"])
        layers.append(out)
    return {"name": spec["name"], "duration": round(max(float(spec.get("duration") or 0), end), 3),
            "loop": str(spec.get("loop", "false")).lower() == "true", "sortingLayer": spec.get("sorting_layer", "Default"),
            "order": int(spec.get("order", 10)), "layers": layers}


def cmd_new(args):
    palette = read_palette(args.root)
    if not palette:
        sys.exit("no palette in .studio/art/art-bible.md; run palette.py set first")
    roles = (args.colors or "").split(",") if args.colors else [r for r in ("hazard", "reward") if r in palette] or list(palette)[-2:]
    a, b = roles[0], roles[1] if len(roles) > 1 else roles[0]
    layers = []
    for preset_layer in PRESETS[args.preset]:
        layer = dict(preset_layer)
        layer["color"] = ">".join({"a": a, "b": b}.get(r, r if r in palette else a) for r in layer["color"].split(">"))
        layers.append(layer)
    spec = {"name": args.name, "preset": args.preset, "trigger": args.trigger or "<EventChannelAsset>", "pool_prewarm": 4,
            "loop": "false", "sorting_layer": "Default", "order": 10, "layers": layers}
    for layer in layers:
        for role in layer["color"].split(">"):
            resolve(role, palette, args.name)
    folder = os.path.join(args.root, ".studio", "vfx")
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, f"{args.name}.yaml")
    if os.path.exists(path) and not args.force:
        sys.exit(f"exists: {path} (use --force to overwrite)")
    open(path, "w", encoding="utf-8", newline="\n").write(dump_yaml(spec))
    print(f"wrote {path} ({len(layers)} layers, colours {a}/{b})")


def cmd_build(args):
    palette = read_palette(args.root)
    folder = os.path.join(args.root, ".studio", "vfx")
    names = args.names or ([f[:-5] for f in sorted(os.listdir(folder)) if f.endswith(".yaml")] if os.path.isdir(folder) else [])
    if not names:
        sys.exit("no specs in .studio/vfx/")
    out_dir = os.path.join(args.root, "Assets", "_Project", "Art", "VFX", "Specs")
    os.makedirs(out_dir, exist_ok=True)
    for name in names:
        path = os.path.join(folder, f"{name}.yaml")
        data = to_json(load_yaml(path), palette, path)
        target = os.path.join(out_dir, f"{name}.vfx.json")
        open(target, "w", encoding="utf-8", newline="\n").write(json.dumps(data, indent=1))
        print(f"wrote {target} ({len(data['layers'])} layers, {data['duration']}s)")
    print("next: Unity menu Tools > Studio > Build VFX From Specs")


def main():
    ap = argparse.ArgumentParser(description="VFX specs")
    sub = ap.add_subparsers(dest="command", required=True)
    new = sub.add_parser("new")
    new.add_argument("name")
    new.add_argument("--preset", choices=sorted(PRESETS), required=True)
    new.add_argument("--colors", help="two palette roles a,b (default hazard,reward)")
    new.add_argument("--trigger", help="SO event channel asset that plays it")
    new.add_argument("--force", action="store_true")
    build = sub.add_parser("build")
    build.add_argument("names", nargs="*")
    for p in (new, build):
        p.add_argument("--root", default=".")
    args = ap.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    (cmd_new if args.command == "new" else cmd_build)(args)


if __name__ == "__main__":
    main()
