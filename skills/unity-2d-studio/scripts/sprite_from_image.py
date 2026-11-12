#!/usr/bin/env python3
"""Turn an image-model "pixel art" picture or sheet into a real pixel-grid sprite (sheet) Unity can slice. Stdlib only.

  python sprite_from_image.py <input> --cell WxH [--frames N | --grid CxR] [--out PATH] [--force] [--root PROJECT_ROOT]
          [--key auto|none|#rrggbb] [--tolerance 32] [--no-palette] [--outline ROLE|#hex]
      -> removes the flat key background (auto = commonest border colour; flood fill from the border, so interior
         pixels of that colour survive; key-colour fringes go too), splits frames (transparent gaps if they give
         exactly N / CxR, else equal division, row-major), downscales every frame with ONE scale (majority colour
         per block) into WxH cells, bottom-centre (Unity pivot), maps colours to the art-bible palette (palette.py)
         and writes one horizontal row: Assets/_Project/Art/Sprites/<Stem>_sheet_<W>x<H>.png (<Stem>.png for 1 frame).
         Existing files are kept unless --force. Prints one summary line; drift = share of opaque pixels moved
         more than ΔE 10 by the palette mapping (high drift → re-prompt with the palette hex codes).
Reads any format when Pillow is installed, otherwise PNG only (non-interlaced, any bit depth / colour type).
"""
import argparse
import bisect
import math
import os
import re
import struct
import sys
import zlib
from array import array
from collections import Counter

sys.dont_write_bytecode = True  # plugin dir may be a read-only cache: no __pycache__ there
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from palette import lab, read_palette  # noqa: E402
from placeholder_sprites import png  # noqa: E402

OUT = os.path.join("Assets", "_Project", "Art", "Sprites")
SHEET = re.compile(r"_sheet_(\d+)x(\d+)$", re.IGNORECASE)
SIGNATURE = b"\x89PNG\r\n\x1a\n"
CLEAR = 1 << 24          # "transparent" marker in colour lists (real colours are 24-bit 0xBBGGRR)
BUCKET = 0xF8F8F8        # 5 bits per channel: near-identical generator noise votes together
DRIFT_DE = 10.0          # palette moves smaller than this are noise, not drift
DRIFT_WARN = 25          # % drift that suggests re-prompting
RUN = re.compile(rb"\x01+")
CONTENT = re.compile(rb"[^\x00]+")
WORD = "I" if array("I").itemsize == 4 else "L"


# ---------- decoding ----------

def add_bytes(a, b):
    """Byte-wise (a + b) & 255 over whole rows at once (PNG 'Up' filter) using big-int arithmetic."""
    n = len(a)
    lo, hi = int.from_bytes(b"\x7f" * n, "big"), int.from_bytes(b"\x80" * n, "big")
    x, y = int.from_bytes(a, "big"), int.from_bytes(b, "big")
    return bytearray((((x & lo) + (y & lo)) ^ ((x ^ y) & hi)).to_bytes(n, "big"))


def unfilter(raw, height, stride, bpp):
    out = bytearray(height * stride)
    prev = bytearray(stride)
    pos = 0
    for y in range(height):
        kind = raw[pos]
        line = bytearray(raw[pos + 1:pos + 1 + stride])
        pos += stride + 1
        if kind == 1:
            for i in range(bpp, stride):
                line[i] = (line[i] + line[i - bpp]) & 255
        elif kind == 2:
            line = add_bytes(line, prev)
        elif kind == 3:
            for i in range(bpp):
                line[i] = (line[i] + (prev[i] >> 1)) & 255
            for i in range(bpp, stride):
                line[i] = (line[i] + ((line[i - bpp] + prev[i]) >> 1)) & 255
        elif kind == 4:
            for i in range(bpp):
                line[i] = (line[i] + prev[i]) & 255
            for i in range(bpp, stride):
                a, b, c = line[i - bpp], prev[i], prev[i - bpp]
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - c - c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        elif kind:
            raise ValueError(f"PNG row {y} has unknown filter type {kind}")
        out[y * stride:(y + 1) * stride] = line
        prev = line
    return out


def samples16(buf):
    values = array("H", bytes(buf))
    if sys.byteorder == "little":
        values.byteswap()
    return values


def unpack_bits(rows, width, height, stride, depth):
    """1/2/4-bit samples -> one byte per sample (unscaled)."""
    out = bytearray(width * height)
    per, mask = 8 // depth, (1 << depth) - 1
    for y in range(height):
        row = rows[y * stride:(y + 1) * stride]
        for x in range(width):
            out[y * width + x] = (row[x // per] >> (8 - depth * (x % per + 1))) & mask
    return out


def decode_png(data):
    """PNG bytes -> (width, height, RGBA bytearray). Raises ValueError with a readable reason."""
    if data[:8] != SIGNATURE:
        raise ValueError("not a PNG (install Pillow for other formats: pip install Pillow)")
    pos, idat, plte, trns, head = 8, [], b"", None, None
    while pos + 8 <= len(data):
        length, kind = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        pos += 12 + length
        if kind == b"IHDR":
            head = struct.unpack(">IIBBBBB", body)
        elif kind == b"PLTE":
            plte = body
        elif kind == b"tRNS":
            trns = body
        elif kind == b"IDAT":
            idat.append(body)
        elif kind == b"IEND":
            break
    if not head or not idat:
        raise ValueError("PNG has no IHDR/IDAT (truncated file?)")
    width, height, depth, ctype, _, _, interlace = head
    if interlace:
        raise ValueError("interlaced PNG: re-save it non-interlaced (or pip install Pillow)")
    channels = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}.get(ctype)
    if not channels or depth not in (1, 2, 4, 8, 16) or (depth < 8 and ctype not in (0, 3)) or (depth == 16 and ctype == 3):
        raise ValueError(f"unsupported PNG colour type {ctype} / bit depth {depth}")
    bits = channels * depth
    stride = (width * bits + 7) // 8
    rows = unfilter(zlib.decompress(b"".join(idat)), height, stride, max(1, bits // 8))
    n = width * height
    rgba = bytearray(b"\xff" * (n * 4))
    if ctype == 3:
        idx = bytes(unpack_bits(rows, width, height, stride, depth) if depth < 8 else rows)
        pal = plte.ljust(768, b"\x00")
        alpha = bytes(trns or b"").ljust(256, b"\xff")[:256]
        for c in range(3):
            rgba[c::4] = idx.translate(pal[c::3])
        rgba[3::4] = idx.translate(alpha)
        return width, height, rgba
    if depth < 8:
        raw = unpack_bits(rows, width, height, stride, depth)
        full = raw
        rgba[0::4] = rgba[1::4] = rgba[2::4] = bytes(raw).translate(bytes(v * 255 // ((1 << depth) - 1) & 255 for v in range(256)))
    else:
        full = samples16(rows) if depth == 16 else rows
        eight = rows[0::2] if depth == 16 else rows
        if ctype in (0, 4):
            grey = eight[0::channels]
            rgba[0::4] = rgba[1::4] = rgba[2::4] = grey
        else:
            for c in range(3):
                rgba[c::4] = eight[c::channels]
        if ctype in (4, 6):
            rgba[3::4] = eight[channels - 1::channels]
    if trns and ctype == 0:
        key = struct.unpack(">H", trns[:2])[0]
        for i in range(n):
            if full[i] == key:
                rgba[i * 4 + 3] = 0
    elif trns and ctype == 2:
        key = struct.unpack(">HHH", trns[:6])
        for i in range(n):
            if full[i * 3] == key[0] and full[i * 3 + 1] == key[1] and full[i * 3 + 2] == key[2]:
                rgba[i * 4 + 3] = 0
    return width, height, rgba


def load(path):
    try:
        from PIL import Image
    except ImportError:
        Image = None
    if Image is not None:
        try:
            with Image.open(path) as img:
                img = img.convert("RGBA")
                return img.width, img.height, bytearray(img.tobytes())
        except OSError as exc:
            raise ValueError(f"cannot read image: {exc}")
    with open(path, "rb") as fh:
        return decode_png(fh.read())


# ---------- background ----------

def colours_of(rgba):
    """RGBA bytes -> list of 0xBBGGRR ints, CLEAR where alpha < 128."""
    words = array(WORD, bytes(rgba))
    if sys.byteorder == "big":
        words.byteswap()
    return [v & 0xFFFFFF if v >= 0x80000000 else CLEAR for v in words]


def hex_of(c):
    return f"#{c & 255:02x}{c >> 8 & 255:02x}{c >> 16 & 255:02x}"


def int_of(hex_code):
    r, g, b = (int(hex_code[i:i + 2], 16) for i in (1, 3, 5))
    return r | g << 8 | b << 16


def border(cols, w, h):
    idx = list(range(w)) + list(range((h - 1) * w, h * w)) + [y * w for y in range(1, h - 1)] + [y * w + w - 1 for y in range(1, h - 1)]
    return [cols[i] for i in idx]


def auto_key(cols, w, h):
    edge = border(cols, w, h)
    solid = [c for c in edge if c != CLEAR]
    if len(solid) * 2 < len(edge):
        return None  # border already mostly transparent
    best = Counter(c & BUCKET for c in solid).most_common(1)[0][0]
    return Counter(c for c in solid if c & BUCKET == best).most_common(1)[0][0]


def flood(match, w, h):
    """Connected runs of match==1 that touch the border -> bytearray mask (1 = reached)."""
    runs = [[(m.start(), m.end()) for m in RUN.finditer(match, y * w, (y + 1) * w)] for y in range(h)]
    runs = [[(s - y * w, e - y * w) for s, e in row] for y, row in enumerate(runs)]
    starts = [[s for s, _ in row] for row in runs]
    stack = [(y, k) for y, row in enumerate(runs) for k, (s, e) in enumerate(row) if y in (0, h - 1) or s == 0 or e == w]
    seen, reached = set(), bytearray(w * h)
    while stack:
        y, k = stack.pop()
        if (y, k) in seen:
            continue
        seen.add((y, k))
        s, e = runs[y][k]
        reached[y * w + s:y * w + e] = b"\x01" * (e - s)
        for ny in (y - 1, y + 1):
            if 0 <= ny < h:
                row, j = runs[ny], bisect.bisect_left(starts[ny], e) - 1
                while j >= 0 and row[j][1] > s:
                    if (ny, j) not in seen:
                        stack.append((ny, j))
                    j -= 1
    return reached


def remove_key(cols, w, h, key, tolerance):
    """Clears the border-connected key colour plus 1px fringes; returns the share of pixels removed."""
    kr, kg, kb = key & 255, key >> 8 & 255, key >> 16
    near, fringe = tolerance * tolerance, (1.5 * tolerance) ** 2
    level = {CLEAR: 1}
    for c in set(cols):
        if c != CLEAR:
            d = ((c & 255) - kr) ** 2 + ((c >> 8 & 255) - kg) ** 2 + ((c >> 16) - kb) ** 2
            level[c] = 1 if d <= near else 2 if d <= fringe else 0
    levels = bytes(map(level.__getitem__, cols))
    reached = flood(levels.translate(bytes([0, 1, 0]) + bytes(253)), w, h)
    edge, i = [], levels.find(2)
    while i >= 0:
        x = i % w
        if not reached[i] and ((x and reached[i - 1]) or (x < w - 1 and reached[i + 1])
                               or (i >= w and reached[i - w]) or (i + w < w * h and reached[i + w])):
            edge.append(i)
        i = levels.find(2, i + 1)
    for i in edge:
        reached[i] = 1
    removed = 0
    for i in RUN.finditer(reached):
        for j in range(i.start(), i.end()):
            if cols[j] != CLEAR:
                cols[j] = CLEAR
                removed += 1
    return removed / float(w * h)


# ---------- frames ----------

def split(profile, count):
    segs = [(m.start(), m.end()) for m in CONTENT.finditer(profile)]
    if len(segs) == count:
        return segs, True
    n = len(profile)
    return [(i * n // count, (i + 1) * n // count) for i in range(count)], False


def frames_of(cols, w, h, columns, rows):
    opaque = bytes(map(CLEAR.__ne__, cols))
    acc = 0
    row_has = bytearray(h)
    for y in range(h):
        line = opaque[y * w:(y + 1) * w]
        acc |= int.from_bytes(line, "big")
        row_has[y] = 1 in line
    xs, found_x = split(acc.to_bytes(w, "big"), columns)
    ys, found_y = split(bytes(row_has), rows) if rows > 1 else ([(0, h)], True)
    boxes = []
    for y0, y1 in ys:
        for x0, x1 in xs:
            box = None
            for y in range(y0, y1):
                line = opaque[y * w + x0:y * w + x1]
                a = line.find(1)
                if a >= 0:
                    b = line.rfind(1) + 1
                    box = [x0 + a, y, x0 + b, y + 1] if box is None else [min(box[0], x0 + a), box[1], max(box[2], x0 + b), y + 1]
            boxes.append(box)
    return boxes, found_x and found_y


# ---------- downscale ----------

def majority(cnt):
    buckets = Counter()
    for c, k in cnt.items():
        buckets[c & BUCKET] += k
    best = buckets.most_common(1)[0][0]
    return max((k, c) for c, k in cnt.items() if c & BUCKET == best)[1]


def downscale(cols, w, box, scale, cell_w, cell_h, inset):
    """One frame -> cell_w*cell_h list of colours (CLEAR = transparent), bottom-centre aligned."""
    out = [CLEAR] * (cell_w * cell_h)
    if box is None:
        return out
    x0, y0, x1, y1 = box
    ow = min(cell_w - 2 * inset, max(1, math.ceil((x1 - x0) / scale - 1e-9)))
    oh = min(cell_h - inset, max(1, math.ceil((y1 - y0) / scale - 1e-9)))
    dx = (cell_w - ow) // 2
    for k in range(oh):
        ye, ys = y1 - int(k * scale), max(y0, y1 - int((k + 1) * scale))
        if ye <= ys:
            continue
        for ox in range(ow):
            xs, xe = x0 + int(ox * scale), min(x1, x0 + int((ox + 1) * scale))
            if xe <= xs:
                continue
            cnt = Counter()
            for y in range(ys, ye):
                cnt.update(cols[y * w + xs:y * w + xe])
            clear = cnt.pop(CLEAR, 0)
            solid = sum(cnt.values())
            if solid and solid >= clear:  # at least half the block is opaque
                out[(cell_h - 1 - k) * cell_w + dx + ox] = majority(cnt)
    return out


# ---------- palette + outline ----------

def map_palette(cells, roles):
    targets = [(int_of(h), lab(h)) for h in roles.values()]
    best = {}
    for cell in cells:
        best.update((c, None) for c in cell if c != CLEAR and c not in best)
    for c in best:
        l = lab(hex_of(c))
        d, t = min((sum((p - q) ** 2 for p, q in zip(l, tl)) ** 0.5, t) for t, tl in targets)
        best[c] = (t, d)
    moved = total = 0
    worst = 0.0
    for cell in cells:
        for i, c in enumerate(cell):
            if c != CLEAR:
                t, d = best[c]
                cell[i] = t
                total += 1
                moved += d > DRIFT_DE
                worst = max(worst, d)
    return (100.0 * moved / total if total else 0.0), worst


def outline(cell, cell_w, cell_h, colour):
    solid = [c != CLEAR for c in cell]
    for i, c in enumerate(cell):
        if c == CLEAR:
            x, y = i % cell_w, i // cell_w
            if (x and solid[i - 1]) or (x < cell_w - 1 and solid[i + 1]) or (y and solid[i - cell_w]) or (y < cell_h - 1 and solid[i + cell_w]):
                cell[i] = colour


# ---------- cli ----------

def size(value, what):
    m = re.fullmatch(r"(\d+)[xX](\d+)", value or "")
    if not m or not int(m.group(1)) or not int(m.group(2)):
        sys.exit(f"error: {what} '{value}' is not WxH (e.g. 32x32)")
    return int(m.group(1)), int(m.group(2))


def target_path(args, root, cell, count):
    if args.out:
        out = os.path.abspath(args.out)
        stem = os.path.splitext(os.path.basename(out))[0]
        m = SHEET.search(stem)
        if count > 1 and (not m or (int(m.group(1)), int(m.group(2))) != cell):
            print(f"warn: {os.path.basename(out)} lacks _sheet_{cell[0]}x{cell[1]}; Unity's SpriteImportRules will not slice it",
                  file=sys.stderr)
        return out
    stem = SHEET.sub("", os.path.splitext(os.path.basename(args.input))[0])
    name = f"{stem}_sheet_{cell[0]}x{cell[1]}.png" if count > 1 else f"{stem}.png"
    return os.path.join(root, OUT, name)


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("input")
    ap.add_argument("--cell", required=True, help="output cell WxH, e.g. 32x32")
    group = ap.add_mutually_exclusive_group()
    group.add_argument("--frames", type=int, default=1, help="frames in one row (default 1)")
    group.add_argument("--grid", help="CxR frames, read row-major")
    ap.add_argument("--out")
    ap.add_argument("--key", default="auto", help="auto | none | #rrggbb")
    ap.add_argument("--tolerance", type=float, default=32, help="RGB distance counted as key colour (default 32)")
    ap.add_argument("--no-palette", action="store_true")
    ap.add_argument("--outline", help="art-bible role or #rrggbb")
    ap.add_argument("--force", action="store_true", help="overwrite an existing output")
    ap.add_argument("--root", default=".")
    args = ap.parse_args()
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8")
    root = os.path.abspath(args.root)
    cell = size(args.cell, "--cell")
    columns, rows = size(args.grid, "--grid") if args.grid else (args.frames, 1)
    if columns < 1:
        sys.exit("error: --frames must be >= 1")
    if args.key not in ("auto", "none") and not re.fullmatch(r"#[0-9a-fA-F]{6}", args.key):
        sys.exit(f"error: --key '{args.key}' is not auto, none or #rrggbb")
    count = columns * rows
    out = target_path(args, root, cell, count)
    if os.path.exists(out) and not args.force:
        sys.exit(f"error: {out} exists; pass --force to overwrite")
    roles = {} if args.no_palette else read_palette(root)
    line_colour = None
    if args.outline:
        hex_code = args.outline if args.outline.startswith("#") else read_palette(root).get(args.outline)
        if not hex_code or not re.fullmatch(r"#[0-9a-fA-F]{6}", hex_code):
            sys.exit(f"error: --outline '{args.outline}' is not #rrggbb or a palette role (python palette.py show)")
        line_colour = int_of(hex_code.lower())
    if not os.path.isfile(args.input):
        sys.exit(f"error: {args.input} not found")
    try:
        w, h, rgba = load(args.input)
    except (ValueError, zlib.error, struct.error) as exc:
        sys.exit(f"error: {args.input}: {exc}")
    cols = colours_of(rgba)
    key = auto_key(cols, w, h) if args.key == "auto" else None if args.key == "none" else int_of(args.key.lower())
    removed = remove_key(cols, w, h, key, args.tolerance) if key is not None else 0.0
    boxes, detected = frames_of(cols, w, h, columns, rows)
    if not any(boxes):
        sys.exit("error: nothing left after background removal (try --key none or a lower --tolerance)")
    inset = 1 if line_colour is not None else 0
    fit_w, fit_h = cell[0] - 2 * inset, cell[1] - inset
    if fit_w < 1 or fit_h < 1:
        sys.exit("error: cell too small for an outline")
    scale = max(1.0, max(max((b[2] - b[0]) / float(fit_w), (b[3] - b[1]) / float(fit_h)) for b in boxes if b))
    cells = [downscale(cols, w, b, scale, cell[0], cell[1], inset) for b in boxes]
    notes = []
    if roles:
        drift, worst = map_palette(cells, roles)
        hint = f" (>{DRIFT_WARN}%: re-prompt with the palette hex codes)" if drift > DRIFT_WARN else ""
        notes.append(f"palette {len(roles)} roles, drift {drift:.0f}% max ΔE {worst:.0f}{hint}")
    elif not args.no_palette:
        notes.append("no palette (python palette.py set ...)")
    if line_colour is not None:
        for c in cells:
            outline(c, cell[0], cell[1], line_colour)
        notes.append(f"outline {hex_of(line_colour)}")
    empty = [str(i + 1) for i, b in enumerate(boxes) if b is None]
    if empty:
        notes.append(f"empty frames {','.join(empty)}")
    sheet_w = cell[0] * count
    pixels = []
    for y in range(cell[1]):
        for c in cells:
            pixels.extend((0, 0, 0, 0) if v == CLEAR else (v & 255, v >> 8 & 255, v >> 16, 255) for v in c[y * cell[0]:(y + 1) * cell[0]])
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "wb") as fh:
        fh.write(png(sheet_w, cell[1], pixels))
    shown = os.path.relpath(out, root) if os.path.splitdrive(out)[0] == os.path.splitdrive(root)[0] else out
    shown = out if shown.startswith("..") else shown.replace(os.sep, "/")
    split_note = "" if count == 1 or detected else " (equal split)"
    key_note = f"key {hex_of(key)} removed {removed * 100:.0f}%" if key is not None else "key none"
    print(" · ".join([shown, f"{count} frame{'s' if count > 1 else ''} {cell[0]}x{cell[1]}{split_note}", key_note] + notes))


if __name__ == "__main__":
    main()
