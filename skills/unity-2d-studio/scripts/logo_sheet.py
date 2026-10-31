#!/usr/bin/env python3
"""Logo test sheet: each SVG concept at 16/32/64/256 px, in colour and one colour, on light and dark backgrounds.

  python logo_sheet.py .studio/art/logo/concept-1.svg concept-2.svg ... [--out .studio/art/logo/test-sheet.html]
Also flags SVG traits that break small/one-colour use (gradients, filters, raster images, many paths).
"""
import argparse
import base64
import html
import os
import re
import sys

SIZES = (16, 32, 64, 256)
MAX_PATHS = 40
# CSS filters turn any artwork into flat black or white, which is the one-colour test.
MODES = (("colour", "none"), ("black", "brightness(0)"), ("white", "brightness(0) invert(1)"))
BACKGROUNDS = (("light", "#f4f4f0"), ("dark", "#16161c"))


def audit(svg):
    notes = []
    if re.search(r"<(linear|radial)Gradient", svg):
        notes.append("gradient (master should be flat)")
    if "<filter" in svg:
        notes.append("filter effect")
    if "<image" in svg:
        notes.append("embedded raster image")
    if "<text" in svg:
        notes.append("live <text> (convert to paths or note the font)")
    paths = len(re.findall(r"<(path|rect|circle|ellipse|polygon|polyline|line)\b", svg))
    if paths > MAX_PATHS:
        notes.append(f"{paths} shapes (> {MAX_PATHS}, likely too detailed for 16 px)")
    thin = [float(w) for w in re.findall(r'stroke-width="([\d.]+)"', svg) if float(w) < 1.5]
    if thin:
        notes.append(f"thin strokes ({min(thin)}) may vanish small")
    return notes


def main():
    ap = argparse.ArgumentParser(description="logo test sheet")
    ap.add_argument("svgs", nargs="+")
    ap.add_argument("--out", default=os.path.join(".studio", "art", "logo", "test-sheet.html"))
    args = ap.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    sections = []
    for path in args.svgs:
        svg = open(path, encoding="utf-8").read()
        uri = "data:image/svg+xml;base64," + base64.b64encode(svg.encode("utf-8")).decode("ascii")
        notes = audit(svg)
        rows = []
        for bg_name, bg in BACKGROUNDS:
            for mode, css in MODES:
                imgs = "".join(f'<img src="{uri}" width="{s}" height="{s}" style="filter:{css}" title="{s}px">' for s in SIZES)
                rows.append(f'<div class="row" style="background:{bg}"><span>{bg_name} · {mode}</span>{imgs}</div>')
        warn = "".join(f"<li>{html.escape(n)}</li>" for n in notes) or "<li>no audit warnings</li>"
        sections.append(f"<h2>{html.escape(os.path.basename(path))}</h2><ul>{warn}</ul>{''.join(rows)}")
        print(f"{path}: " + ("; ".join(notes) or "ok"))
    os.makedirs(os.path.dirname(args.out) or ".", exist_ok=True)
    page = ("<!doctype html><meta charset=utf-8><title>Logo test sheet</title><style>body{font:14px sans-serif;margin:16px}"
            ".row{display:flex;align-items:center;gap:16px;padding:8px}.row span{width:110px;color:#888}img{object-fit:contain}</style>"
            + "".join(sections))
    open(args.out, "w", encoding="utf-8").write(page)
    print(f"wrote {args.out}")


if __name__ == "__main__":
    main()
