#!/usr/bin/env python3
"""Render game sprites with headless Blender from a small JSON spec: same light, camera and material for a whole set.

  python blender_sprites.py check                       -> prints the Blender found (or how to get it), exit 0/2
  python blender_sprites.py new <set> --preset gems|model -> writes .studio/art/blender/<set>.json to edit
  python blender_sprites.py render <set> [--only NAME] [--size N]
      -> PNGs (transparent, soft ground shadow) into the spec's "out" folder, frames as one <name>_sheet_<W>x<H>.png row,
         and ONE preview board .studio/art/blender/<set>_preview.png (read that, not every PNG).
Spec: out, size, view top|front|iso, shadow, padding, look{roughness coat key rim fill sheen world}, items[]:
  {"name": "Gem_Red", "kind": "gem", "sides": 0|3..8, "angle": deg, "color": "#hex" | palette role}
  {"name": "Hero", "kind": "model", "file": ".blend|.glb|.gltf|.fbx|.obj", "yaw": [0, 90], "frames": [1, 5, 9]}
Blender: $BLENDER, PATH, or the default install folders. Options: --root PROJECT_ROOT
"""
import argparse
import glob
import json
import math
import os
import re
import subprocess
import sys
import tempfile

try:
    import bpy
except ImportError:
    bpy = None

LOOK = {"roughness": 0.12, "coat": 1.0, "key": 230.0, "rim": 45.0, "fill": 25.0, "sheen": 2.5, "world": 0.25}
PRESETS = {
    "gems": {"out": "Assets/_Project/Art/Sprites/Gems", "size": 128, "view": "top", "shadow": True, "padding": 0.0,
             "look": LOOK, "items": [{"name": f"Gem_{n}", "kind": "gem", "sides": s, "angle": a, "color": c} for n, s, a, c in (
                 ("Red", 0, 0, "#e8453c"), ("Green", 4, 45, "#4cbf4b"), ("Blue", 4, 0, "#3d8be8"),
                 ("Yellow", 3, 90, "#f5c928"), ("Purple", 5, 90, "#9b59d0"), ("Orange", 6, 0, "#f28c28"))]},
    "model": {"out": "Assets/_Project/Art/Sprites", "size": 128, "view": "iso", "shadow": True, "padding": 0.06, "look": LOOK,
              "items": [{"name": "Crate", "kind": "model", "file": "Assets/_Project/Art/Models/Crate.blend", "yaw": [0], "frames": [1]}]},
}


# ---------- outside Blender ----------
def find_blender():
    env = os.environ.get("BLENDER")
    if env and os.path.isfile(env):
        return env
    for name in ("blender", "blender.exe"):
        for folder in os.environ.get("PATH", "").split(os.pathsep):
            path = os.path.join(folder, name)
            if os.path.isfile(path):
                return path
    found = (glob.glob(r"C:\Program Files\Blender Foundation\Blender *\blender.exe")
             + glob.glob("/Applications/Blender*.app/Contents/MacOS/Blender") + glob.glob("/snap/bin/blender") + glob.glob("/usr/bin/blender"))
    version = lambda p: [int(x) for x in re.findall(r"\d+", p)[:2]] or [0]
    return max(found, key=version) if found else None


def palette(root):
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from palette import read_palette
    return read_palette(root)


def resolve(root, spec, only, size):
    roles = palette(root)
    items = [dict(i) for i in spec["items"] if not only or i["name"] == only]
    for item in items:
        color = item.get("color", "#cccccc")
        if not color.startswith("#"):
            if color not in roles:
                sys.exit(f"colour role '{color}' not in the art bible palette (palette.py show)")
            item["color"] = roles[color]
        if item.get("file"):
            item["file"] = os.path.abspath(os.path.join(root, item["file"]))
            if not os.path.exists(item["file"]):
                sys.exit(f"model file missing: {item['file']}")
    return {**spec, "items": items, "size": size or spec.get("size", 128), "look": {**LOOK, **spec.get("look", {})},
            "out": os.path.abspath(os.path.join(root, spec["out"]))}


def outside(argv):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("cmd", choices=("check", "new", "render"))
    ap.add_argument("set", nargs="?")
    ap.add_argument("--preset", choices=sorted(PRESETS), default="gems")
    ap.add_argument("--only")
    ap.add_argument("--size", type=int)
    ap.add_argument("--root", default=".")
    args = ap.parse_args(argv)
    root = os.path.abspath(args.root)
    blender = find_blender()
    if args.cmd == "check":
        print(f"blender: {blender}" if blender else "blender: not found (free: blender.org; or set BLENDER=<path to blender executable>)")
        return 0 if blender else 2
    if not args.set:
        sys.exit("set name required")
    folder = os.path.join(root, ".studio", "art", "blender")
    spec_path = os.path.join(folder, f"{args.set}.json")
    if args.cmd == "new":
        if os.path.exists(spec_path):
            sys.exit(f"exists: {spec_path}")
        os.makedirs(folder, exist_ok=True)
        with open(spec_path, "w", encoding="utf-8", newline="\n") as fh:
            json.dump(PRESETS[args.preset], fh, indent=2)
        print(f"wrote {os.path.relpath(spec_path, root)}: edit items/colours, then: render {args.set}")
        return 0
    if not blender:
        sys.exit("blender not found (free: blender.org; or set BLENDER=<path>)")
    spec = resolve(root, json.load(open(spec_path, encoding="utf-8")), args.only, args.size)
    spec["preview"] = os.path.join(folder, f"{args.set}_preview.png")
    with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False, encoding="utf-8") as fh:
        json.dump(spec, fh)
    try:
        r = subprocess.run([blender, "-b", "--factory-startup", "--python", os.path.abspath(__file__), "--", fh.name],
                           capture_output=True, text=True, encoding="utf-8", errors="replace")
    finally:
        os.remove(fh.name)
    done = [l for l in r.stdout.splitlines() if l.startswith("sprite ")]
    errors = [l for l in (r.stdout + r.stderr).splitlines() if "Error" in l or "Traceback" in l]
    for line in done:
        print(line)
    if r.returncode or not done:
        print("\n".join(errors[-8:]) or r.stderr[-800:], file=sys.stderr)
        return 1
    print(f"preview: {os.path.relpath(spec['preview'], root)} ({len(done)} sprite(s))")
    return 0


# ---------- inside Blender ----------
def linear(hex_code):
    c = [int(hex_code[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (1.0,)


def scene_setup(spec):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.render.resolution_x = scene.render.resolution_y = spec["size"]
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.25, 0.25, 0.3, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = spec["look"]["world"]
    scene.world = world
    return scene


def gem(item, look):
    import bmesh
    bm = bmesh.new()
    n, angle, radius = item.get("sides") or 48, math.radians(item.get("angle", 0)), 0.42
    ring = lambda r, z: [bm.verts.new((r * math.cos(angle + 2 * math.pi * i / n), r * math.sin(angle + 2 * math.pi * i / n), z)) for i in range(n)]
    rings = [ring(radius * 0.86, 0.0), ring(radius, 0.10), ring(radius * 0.97, 0.20), ring(radius * 0.58, 0.30)]
    for a, b in zip(rings, rings[1:]):
        for i in range(n):
            bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
    bm.faces.new(rings[-1])
    bm.faces.new(list(reversed(rings[0])))
    mesh = bpy.data.meshes.new(item["name"])
    bm.to_mesh(mesh)
    bm.free()
    for poly in mesh.polygons:
        poly.use_smooth = not item.get("sides")
    obj = bpy.data.objects.new(item["name"], mesh)
    mat = bpy.data.materials.new(item["name"])
    mat.use_nodes = True
    p = mat.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = linear(item["color"])
    p.inputs["Roughness"].default_value = look["roughness"]
    p.inputs["Coat Weight"].default_value = look["coat"]
    p.inputs["Coat Roughness"].default_value = 0.03
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new("Bevel", "BEVEL")
    bevel.width, bevel.segments, bevel.limit_method = 0.012, 2, "ANGLE"
    bpy.context.scene.collection.objects.link(obj)
    return [obj]


def model(item):
    path, ext = item["file"], os.path.splitext(item["file"])[1].lower()
    before = set(bpy.data.objects)
    if ext == ".blend":
        with bpy.data.libraries.load(path) as (src, dst):
            dst.objects = list(src.objects)
        for obj in dst.objects:
            if obj is not None and obj.type not in ("CAMERA", "LIGHT"):
                bpy.context.scene.collection.objects.link(obj)
    elif ext in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path)
    elif ext == ".obj":
        bpy.ops.wm.obj_import(filepath=path)
    else:
        raise ValueError(f"unsupported model type {ext}")
    return [o for o in bpy.data.objects if o not in before and o.name in bpy.context.scene.objects]


# Union over every yaw and frame, so one scale fits the whole sheet (an animated part may leave the first frame's box).
def bounds(objects, pivot, item):
    from mathutils import Vector
    scene, pts = bpy.context.scene, []
    for yaw in item.get("yaw", [0]):
        pivot.rotation_euler[2] = math.radians(yaw)
        for frame in item.get("frames", [1]):
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            pts += [o.matrix_world @ Vector(c) for o in objects if o.type == "MESH" for c in o.bound_box]
    lo = Vector([min(p[i] for p in pts) for i in range(3)])
    hi = Vector([max(p[i] for p in pts) for i in range(3)])
    center = (lo + hi) / 2
    return center, max((p - center).length for p in pts), lo.z


def rig(scene, spec, center, radius, ground):
    from mathutils import Euler, Vector
    look, s = spec["look"], max(radius / 0.5, 0.01)
    rot = {"top": (0, 0, 0), "front": (math.radians(90), 0, 0), "iso": (math.radians(54.736), 0, math.radians(45))}[spec.get("view", "top")]
    basis = Euler(rot).to_matrix()
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.type = "ORTHO"
    # Gems keep a fixed frame (one cell); models fit their bounding sphere so every yaw/frame has the same scale.
    cam.data.ortho_scale = 1.0 if spec.get("fixed") else 2 * radius * (1 + spec.get("padding", 0.06))
    cam.data.clip_end = 40 * s
    cam.location = center + basis @ Vector((0, 0, 10 * s))
    cam.rotation_euler = Euler(rot)
    scene.collection.objects.link(cam)
    scene.camera = cam
    # Camera-space placement: key top-left toward the viewer, rim bottom-right, soft fill from the camera.
    for name, local, energy, size in (("Key", (-2.5, 2.5, 4), look["key"], 2.0), ("Rim", (2.5, -2.0, 2), look["rim"], 0.8),
                                      ("Fill", (0, 0, 6), look["fill"], 4.0)):
        light = bpy.data.objects.new(name, bpy.data.lights.new(name, "AREA"))
        light.data.energy, light.data.size = energy * s * s, size * s
        light.location = center + basis @ (Vector(local) * s)
        light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()
        scene.collection.objects.link(light)
    bpy.ops.mesh.primitive_plane_add(size=3 * s, location=center + basis @ (Vector((-1.2, 1.2, 2.2)) * s))
    card = bpy.context.active_object
    card.rotation_euler = (center - card.location).to_track_quat("-Z", "Y").to_euler()
    card.visible_camera = card.visible_diffuse = card.visible_shadow = False
    emit = bpy.data.materials.new("Card")
    emit.use_nodes = True
    nodes = emit.node_tree.nodes
    nodes.remove(nodes["Principled BSDF"])
    em = nodes.new("ShaderNodeEmission")
    em.inputs["Strength"].default_value = look["sheen"]
    emit.node_tree.links.new(em.outputs[0], nodes["Material Output"].inputs[0])
    card.data.materials.append(emit)
    if spec.get("shadow", True):
        bpy.ops.mesh.primitive_plane_add(size=40 * s, location=(center.x, center.y, ground - 0.001))
        bpy.context.active_object.is_shadow_catcher = True


def pixels(path):
    import numpy as np
    img = bpy.data.images.load(path)
    arr = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32)
    img.pixels.foreach_get(arr)
    out = arr.reshape(img.size[1], img.size[0], 4).copy()
    bpy.data.images.remove(img)
    return out


def save(arr, path, background=None):
    import numpy as np
    h, w = arr.shape[:2]
    if background is not None:
        a = arr[..., 3:4]
        arr = np.concatenate([arr[..., :3] * a + np.array(background[:3]) * (1 - a), np.ones((h, w, 1))], axis=2)
    img = bpy.data.images.new("out", w, h, alpha=True)
    img.pixels.foreach_set(arr.astype(np.float32).ravel())
    img.filepath_raw, img.file_format = path, "PNG"
    img.save()
    bpy.data.images.remove(img)


def inside(spec_path):
    import numpy as np
    spec = json.load(open(spec_path, encoding="utf-8"))
    os.makedirs(spec["out"], exist_ok=True)
    tmp, size, board = tempfile.mkdtemp(), spec["size"], []
    for item in spec["items"]:
        scene = scene_setup(spec)
        is_gem = item["kind"] == "gem"
        objects = gem(item, spec["look"]) if is_gem else model(item)
        if not objects:
            raise ValueError(f"{item['name']}: nothing imported")
        roots = [o for o in objects if o.parent is None]
        pivot = bpy.data.objects.new("Pivot", None)
        scene.collection.objects.link(pivot)
        for o in roots:
            o.parent = pivot
        center, radius, ground = bounds(objects, pivot, item)
        rig(scene, {**spec, "fixed": is_gem}, center, 0.5 if is_gem else radius, ground)
        frames = []
        for yaw in item.get("yaw", [0]):
            pivot.rotation_euler[2] = math.radians(yaw)
            for frame in item.get("frames", [1]):
                scene.frame_set(frame)
                scene.render.filepath = os.path.join(tmp, f"{item['name']}_{len(frames)}.png")
                bpy.ops.render.render(write_still=True)
                frames.append(pixels(scene.render.filepath))
        strip = np.concatenate(frames, axis=1)
        name = f"{item['name']}_sheet_{size}x{size}.png" if len(frames) > 1 else f"{item['name']}.png"
        save(strip, os.path.join(spec["out"], name))
        board.extend(frames)
        print(f"sprite {os.path.join(spec['out'], name)} ({len(frames)} frame(s))", flush=True)
    columns = min(len(board), 8)
    rows = [np.concatenate(board[i:i + columns] + [np.zeros_like(board[0])] * (columns - len(board[i:i + columns])), axis=1)
            for i in range(0, len(board), columns)]
    os.makedirs(os.path.dirname(spec["preview"]), exist_ok=True)
    save(np.concatenate(rows[::-1], axis=0), spec["preview"], background=(0.16, 0.12, 0.26))


if __name__ == "__main__":
    if bpy is not None:
        inside(sys.argv[sys.argv.index("--") + 1])
    else:
        sys.exit(outside(sys.argv[1:]))
