#!/usr/bin/env python3
"""Palette bookkeeping so the model only picks colours.

  python palette.py set background=#1b1f3b midground=#3e4a7a interactive=#f2f2e9 hazard=#e04f5f reward=#ffd34d ...
      -> validates 5-8 colours, writes the table into .studio/art/art-bible.md (## Palette), a swatch board
         .studio/art/palette.html (normal + protan/deutan/tritan + value strip) and prints contrast/colour-blind warnings
  python palette.py show   -> prints role=hex lines (what vfx_spec.py and prompts use)
Options: --root PROJECT_ROOT
"""
import argparse
import html
import os
import re
import sys

MIN_COLOURS, MAX_COLOURS = 5, 8
FOREGROUND_ROLES = ("player", "interactive", "hazard", "reward", "enemy", "ui")
MIN_FG_CONTRAST = 3.0
MIN_CB_DISTANCE = 20.0
START, END = "<!-- palette:start -->", "<!-- palette:end -->"
# Machado et al. 2009 dichromacy matrices (severity 1.0), applied in linear RGB.
CVD = {
    "protan": ((0.152286, 1.052583, -0.204868), (0.114503, 0.786281, 0.099216), (-0.003882, -0.048116, 1.051998)),
    "deutan": ((0.367322, 0.860646, -0.227968), (0.280085, 0.672501, 0.047413), (-0.011820, 0.042940, 0.968881)),
    "tritan": ((1.255528, -0.076749, -0.178779), (-0.078411, 0.930809, 0.147602), (0.004733, 0.691367, 0.303900)),
}
BIBLE_SKELETON = """# Art bible
## Pillars
-
## Palette
{table}
## Values
Player/enemies/hazards darker or lighter than every background they cross (check value strip in palette.html).
## Shapes and silhouette
-
## Resolution
PPU: | sprite sizes: | outline: | light from:
## UI
-
"""


def to_linear(c):
    c /= 255
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def to_srgb(c):
    c = min(max(c, 0.0), 1.0)
    return round(255 * (12.92 * c if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055))


def rgb(hex_code):
    return tuple(int(hex_code[i:i + 2], 16) for i in (1, 3, 5))


def luminance(hex_code):
    r, g, b = (to_linear(c) for c in rgb(hex_code))
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def contrast(a, b):
    hi, lo = sorted((luminance(a), luminance(b)), reverse=True)
    return (hi + 0.05) / (lo + 0.05)


def simulate(hex_code, kind):
    lin = [to_linear(c) for c in rgb(hex_code)]
    out = [sum(m * v for m, v in zip(row, lin)) for row in CVD[kind]]
    return "#" + "".join(f"{to_srgb(c):02x}" for c in out)


def lab(hex_code):
    r, g, b = (to_linear(c) for c in rgb(hex_code))
    xyz = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047, 0.2126 * r + 0.7152 * g + 0.0722 * b, \
          (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883
    f = [t ** (1 / 3) if t > 0.008856 else 7.787 * t + 16 / 116 for t in xyz]
    return 116 * f[1] - 16, 500 * (f[0] - f[1]), 200 * (f[1] - f[2])


def distance(a, b):
    return sum((x - y) ** 2 for x, y in zip(lab(a), lab(b))) ** 0.5


def parse_pairs(items):
    # All problems in one message: each retry costs the agent a full round-trip.
    palette, errors = {}, []
    for item in items:
        m = re.fullmatch(r"([A-Za-z][\w-]*)=#?([0-9a-fA-F]{6})", item.strip())
        if not m:
            errors.append(f"'{item}' is not role=#rrggbb")
            continue
        palette[m.group(1).lower().replace("_", "-")] = "#" + m.group(2).lower()
    if not MIN_COLOURS <= len(palette) <= MAX_COLOURS:
        errors.append(f"{len(palette)} colours; art bible needs {MIN_COLOURS}-{MAX_COLOURS} (merge accents or drop one)")
    if "background" not in palette:
        errors.append("one role must be named 'background' (contrast checks use it)")
    if errors:
        sys.exit("error: " + "; ".join(errors))
    return palette


def read_palette(root):
    path = os.path.join(root, ".studio", "art", "art-bible.md")
    if not os.path.exists(path):
        return {}
    text = open(path, encoding="utf-8").read()
    block = text.split(START, 1)[1].split(END, 1)[0] if START in text else ""
    return {m.group(1): m.group(2).lower() for m in re.finditer(r"\|\s*([a-z][a-z0-9-]*)\s*\|\s*(#[0-9a-fA-F]{6})\s*\|", block)}


def warnings(palette):
    bg = palette["background"]
    out = []
    for role, hex_code in palette.items():
        if role in FOREGROUND_ROLES and contrast(hex_code, bg) < MIN_FG_CONTRAST:
            out.append(f"low contrast {role} vs background {contrast(hex_code, bg):.1f}:1 (< {MIN_FG_CONTRAST}:1)")
    fg = [r for r in palette if r in FOREGROUND_ROLES]
    for kind in CVD:
        for i, a in enumerate(fg):
            for b in fg[i + 1:]:
                d = distance(simulate(palette[a], kind), simulate(palette[b], kind))
                if d < MIN_CB_DISTANCE:
                    out.append(f"{kind}: {a} and {b} look alike (ΔE {d:.0f}); separate by value or shape")
    return out


def table(palette):
    bg = palette["background"]
    rows = ["| role | hex | luminance | contrast vs background |", "|---|---|---|---|"]
    rows += [f"| {r} | {h} | {luminance(h):.2f} | {contrast(h, bg):.1f}:1 |" for r, h in palette.items()]
    return "\n".join([START] + rows + [END])


def write_bible(root, palette):
    folder = os.path.join(root, ".studio", "art")
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, "art-bible.md")
    block = table(palette)
    if not os.path.exists(path):
        text = BIBLE_SKELETON.format(table=block)
    else:
        text = open(path, encoding="utf-8").read()
        if START in text:
            text = text.split(START, 1)[0] + block + text.split(END, 1)[1]
        elif "## Palette" in text:
            text = text.replace("## Palette", "## Palette\n" + block, 1)
        else:
            text = text.rstrip() + "\n## Palette\n" + block + "\n"
    open(path, "w", encoding="utf-8", newline="\n").write(text)
    return path


def write_board(root, palette, notes):
    def row(label, colours):
        cells = "".join(f'<div class="sw" style="background:{c}"><span>{html.escape(r)}<br>{c}</span></div>' for r, c in colours)
        return f"<h2>{label}</h2><div class=row>{cells}</div>"
    rows = [row("palette", palette.items())]
    rows += [row(k, [(r, simulate(h, k)) for r, h in palette.items()]) for k in CVD]
    grey = [(r, "#" + f"{to_srgb(luminance(h)):02x}" * 3) for r, h in sorted(palette.items(), key=lambda x: luminance(x[1]))]
    rows.append(row("value (dark → light)", grey))
    warn = "".join(f"<li>{html.escape(n)}</li>" for n in notes) or "<li>no warnings</li>"
    page = ("<!doctype html><meta charset=utf-8><title>Palette</title><style>body{font:14px sans-serif;margin:16px;background:#222;color:#eee}"
            ".row{display:flex;flex-wrap:wrap;gap:6px}.sw{width:110px;height:80px;display:flex;align-items:flex-end;border-radius:4px}"
            ".sw span{background:#000a;padding:2px 4px;font-size:12px}</style>"
            + "".join(rows) + f"<h2>checks</h2><ul>{warn}</ul>")
    path = os.path.join(root, ".studio", "art", "palette.html")
    open(path, "w", encoding="utf-8").write(page)
    return path


def main():
    ap = argparse.ArgumentParser(description="art bible palette")
    ap.add_argument("command", choices=("set", "show"))
    ap.add_argument("pairs", nargs="*", help="role=#rrggbb")
    ap.add_argument("--root", default=".")
    args = ap.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    if args.command == "show":
        palette = read_palette(args.root)
        if not palette:
            sys.exit("no palette in .studio/art/art-bible.md; run: palette.py set role=#hex ...")
        print("\n".join(f"{r}={h}" for r, h in palette.items()))
        return
    palette = parse_pairs(args.pairs)
    notes = warnings(palette)
    print(f"wrote {write_bible(args.root, palette)}\nwrote {write_board(args.root, palette, notes)}")
    print("\n".join(table(palette).splitlines()[1:-1]))
    print("\n".join(f"warn: {n}" for n in notes) or "checks: ok")


if __name__ == "__main__":
    main()
