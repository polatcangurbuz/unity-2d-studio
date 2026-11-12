"""Tests for the plugin scripts (stdlib unittest, no Unity). Run: python -m unittest discover -s tests -v"""
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time
import unittest

SCRIPTS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "skills", "unity-2d-studio", "scripts")


def text(path):
    with open(path, encoding="utf-8") as fh:
        return fh.read()


def script(name, *args, stdin=None, cwd=None):
    return subprocess.run([sys.executable, os.path.join(SCRIPTS, name), *args], input=stdin, capture_output=True,
                          text=True, encoding="utf-8", cwd=cwd, env={**os.environ, "PYTHONDONTWRITEBYTECODE": "1"})


class Project(unittest.TestCase):
    """Fresh minimal Unity project with a .studio folder per test."""

    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="u2d-")
        for d in ("Assets/_Project/Code/Gameplay", "ProjectSettings", ".studio/handoff"):
            os.makedirs(os.path.join(self.root, d))
        self.put("ProjectSettings/ProjectVersion.txt", "m_EditorVersion: 6000.0.58f2\n")
        self.put(".studio/brief.md", "# Brief · Test\n")

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def put(self, rel, text):
        path = os.path.join(self.root, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)
        return path

    def hook(self, mode, **event):
        event.setdefault("cwd", self.root)
        return script("hooks.py", mode, stdin=json.dumps(event))


CLEAN = "namespace A.Gameplay\n{\n    public class Clean\n    {\n        [UnityEngine.SerializeField] private int _speed;\n        public int Speed => _speed;\n    }\n}\n"
BAD = ("namespace A.Gameplay\n{\n    /// <summary>bad</summary>\n    public class Bad : UnityEngine.MonoBehaviour\n    {\n        public int speed;\n"
       "        void Update() { GetComponent<UnityEngine.Rigidbody2D>(); UnityEngine.GameObject.Find(\"x\"); }\n    }\n}\n")


class CheckCode(Project):
    def test_clean_file_passes(self):
        self.put("Assets/_Project/Code/Gameplay/Clean.cs", CLEAN)
        r = script("check_code.py", self.root, "--files", "Assets/_Project/Code/Gameplay/Clean.cs")
        self.assertEqual(r.returncode, 0, r.stdout)

    def test_each_rule_fires(self):
        self.put("Assets/_Project/Code/Gameplay/Bad.cs", BAD)
        r = script("check_code.py", self.root, "--files", "Assets/_Project/Code/Gameplay/Bad.cs")
        self.assertEqual(r.returncode, 1)
        for rule in ("doc-comment", "public-field", "update-getcomponent", "find"):
            self.assertIn(f"[{rule}]", r.stdout)

    def test_outside_project_and_allowed_roots(self):
        self.put("Assets/Stray.cs", CLEAN)
        self.put("Assets/Plugins/Lib.cs", CLEAN)
        self.put("Assets/InputSystem_Actions.inputactions", "{}")
        r = script("check_code.py", self.root)
        self.assertIn("Assets/Stray.cs:1 [location]", r.stdout)
        self.assertNotIn("Plugins", r.stdout)
        self.assertNotIn("inputactions", r.stdout)

    def test_template_scene_is_pending_until_setup_runs(self):
        scene = "--- !u!1 &1\nGameObject:\n  m_Name: Main Camera\n--- !u!1 &2\nGameObject:\n  m_Name: Global Light 2D\n"
        self.put("Assets/Scenes/SampleScene.unity", scene)
        self.assertIn("[location]", script("check_code.py", self.root).stdout)  # no StudioSetup: a real stray file
        self.put("Assets/_Project/Code/Editor/StudioSetup.cs", CLEAN)
        r = script("check_code.py", self.root)
        self.assertEqual(r.returncode, 0, r.stdout)
        self.assertIn("pending Tools > Studio > Setup Project", r.stdout)
        self.put("Assets/Scenes/SampleScene.unity", scene + "--- !u!1 &3\nGameObject:\n  m_Name: Level\n")
        self.assertEqual(script("check_code.py", self.root).returncode, 1)  # user work: Setup moves it, still flagged

    def test_studio_tools_need_their_using(self):
        body = "namespace A.Editor\n{\n    public static class B\n    {\n        public static void Go() => StudioEditorUtil.EnsureFolder(\"x\");\n    }\n}\n"
        self.put("Assets/_Project/Code/Editor/B.cs", body)
        self.put("Assets/_Project/Code/Editor/C.cs", "using Studio.EditorTools;\n" + body.replace("class B", "class C"))
        r = script("check_code.py", self.root, "--files", "Assets/_Project/Code/Editor/B.cs", "Assets/_Project/Code/Editor/C.cs")
        self.assertIn("B.cs:5 [missing-using]", r.stdout)
        self.assertNotIn("C.cs", r.stdout)

    def test_unity6_urp_template_files_are_not_flagged(self):
        for rel in ("Assets/Settings/PC_RPAsset.asset", "Assets/TutorialInfo/Scripts/Readme.cs", "Assets/Readme.asset"):
            self.put(rel, "x")
        self.assertEqual(script("check_code.py", self.root).returncode, 0)

    def test_long_file_and_comment_block(self):
        self.put("Assets/_Project/Code/Gameplay/Long.cs", "// a\n// b\n" + "\n" * 310)
        r = script("check_code.py", self.root, "--files", "Assets/_Project/Code/Gameplay/Long.cs")
        self.assertIn("[file-length]", r.stdout)
        self.assertIn("[comment-block]", r.stdout)


SCENE = """%YAML 1.1
--- !u!1 &100
GameObject:
  m_Name: Ground
--- !u!66 &200
CompositeCollider2D:
  m_GameObject: {fileID: 100}
  m_CompositePaths:
    m_Paths: []
--- !u!114 &300
MonoBehaviour:
  m_GameObject: {fileID: 100}
  m_Script: {fileID: 11500000, guid: 0123456789abcdef0123456789abcdef, type: 3}
  _target: {fileID: 0}
"""


class SceneCheck(Project):
    def test_empty_composite_and_unassigned_ref(self):
        self.put("Assets/_Project/Code/Gameplay/Mover.cs.meta", "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\n")
        self.put("Assets/_Project/Scenes/Level.unity", SCENE)
        r = script("scene_check.py", self.root)
        self.assertEqual(r.returncode, 1)
        self.assertIn("Ground: CompositeCollider2D has no paths", r.stdout)
        self.assertIn("unassigned ref _target (optional by design?", r.stdout)

    def test_several_empty_refs_are_a_wiring_bug(self):
        self.put("Assets/_Project/Code/Gameplay/Mover.cs.meta", "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\n")
        self.put("Assets/_Project/Scenes/Two.unity", SCENE.replace("  _target: {fileID: 0}\n", "  _target: {fileID: 0}\n  _prefab: {fileID: 0}\n"))
        r = script("scene_check.py", self.root)
        self.assertIn("wiring bug", r.stdout)
        self.assertIn("BuildScene", r.stdout)

    def test_clean_scene(self):
        self.put("Assets/_Project/Scenes/Empty.unity", "%YAML 1.1\n--- !u!1 &1\nGameObject:\n  m_Name: A\n")
        self.assertEqual(script("scene_check.py", self.root).returncode, 0)


class Hooks(Project):
    def edit(self, rel, text, agent=None):
        path = self.put(rel, text)
        event = {"tool_name": "Write", "tool_input": {"file_path": path}}
        if agent:
            event["agent_id"] = agent
        return self.hook("post-edit", **event)

    def test_parallel_subagents_keep_their_own_reports(self):
        self.edit("Assets/_Project/Code/Gameplay/A.cs", BAD.replace("Bad", "A"), agent="agent-a")
        self.edit("Assets/_Project/Code/Gameplay/B.cs", BAD.replace("Bad", "B"), agent="agent-b")
        a = self.hook("stop", agent_id="agent-a", agent_type="unity-2d-studio:scribe")
        self.assertEqual(a.returncode, 2)
        self.assertIn("A.cs", a.stderr)
        self.assertNotIn("B.cs", a.stderr)
        b = self.hook("stop", agent_id="agent-b", agent_type="unity-2d-studio:scribe")
        self.assertIn("B.cs", b.stderr)
        self.assertNotIn("A.cs", b.stderr)
        self.assertEqual(self.hook("stop").returncode, 0, "main agent must not get subagent files")

    def test_same_report_is_shown_once_then_clean_stop_cleans_up(self):
        self.edit("Assets/_Project/Code/Gameplay/Bad.cs", BAD)
        self.assertEqual(self.hook("stop").returncode, 2)
        self.assertEqual(self.hook("stop").returncode, 0, "unchanged report repeats")
        self.edit("Assets/_Project/Code/Gameplay/Bad.cs", CLEAN.replace("Clean", "Bad"))
        self.assertEqual(self.hook("stop").returncode, 0)
        self.assertFalse(os.path.exists(os.path.join(self.root, ".studio", ".pending_check")))

    def test_studio_subagent_without_handoff_is_asked_once(self):
        ev = {"agent_id": "c1", "agent_type": "unity-2d-studio:gameplay-coder"}
        self.hook("subagent-start", **ev)
        first = self.hook("stop", **ev)
        self.assertEqual(first.returncode, 2)
        self.assertIn("handoff", first.stderr)
        self.assertEqual(self.hook("stop", **ev).returncode, 0, "asked once only")
        self.assertEqual([f for f in os.listdir(os.path.join(self.root, ".studio")) if f.endswith(".c1")], [])

    def test_subagent_with_handoff_passes(self):
        ev = {"agent_id": "c2", "agent_type": "unity-2d-studio:architect"}
        self.hook("subagent-start", **ev)
        time.sleep(0.05)
        self.put(".studio/handoff/T-001.md", "# T-001 x · architect · 2026-11-10\ngoal: x\n")
        self.assertEqual(self.hook("stop", **ev).returncode, 0)

    def test_other_subagents_are_not_asked_for_handoffs(self):
        ev = {"agent_id": "e1", "agent_type": "Explore"}
        self.hook("subagent-start", **ev)
        self.assertEqual(self.hook("stop", **ev).returncode, 0)

    def test_scene_saved_during_subagent_is_checked(self):
        ev = {"agent_id": "v1", "agent_type": "unity-2d-studio:vfx-artist"}
        self.put(".studio/handoff/T-002.md", "# T-002 boom · vfx-artist · 2026-11-10\n")
        self.hook("subagent-start", **ev)
        time.sleep(0.05)
        self.put("Assets/_Project/Scenes/Level.unity", SCENE)
        r = self.hook("stop", **ev)
        self.assertEqual(r.returncode, 2)
        self.assertIn("CompositeCollider2D", r.stderr)

    def test_handoff_without_header_is_flagged_once(self):
        ev = {"agent_id": "h1", "agent_type": "unity-2d-studio:gameplay-coder"}
        self.hook("subagent-start", **ev)
        time.sleep(0.05)
        self.put(".studio/handoff/T-005.md", "# T-005 handoff\ngoal: x\n")
        first = self.hook("stop", **ev)
        self.assertEqual(first.returncode, 2)
        self.assertIn("header missing in T-005.md", first.stderr)
        self.assertEqual(self.hook("stop", **ev).returncode, 0)

    def test_session_start_reports_scenes_saved_between_sessions(self):
        self.hook("stop")
        time.sleep(0.05)
        self.put("Assets/_Project/Scenes/Level.unity", SCENE)
        first = self.hook("session-start")
        self.assertIn("CompositeCollider2D has no paths", first.stdout)
        self.assertNotIn("CompositeCollider2D", self.hook("stop").stderr, "already reported at session start")

    def test_bad_input_never_blocks(self):
        r = script("hooks.py", "stop", stdin="{not json", cwd=self.root)
        self.assertEqual(r.returncode, 0)
        self.assertTrue(os.path.exists(os.path.join(self.root, ".studio", "hook-errors.log")))

    def test_outside_unity_project_is_silent(self):
        empty = tempfile.mkdtemp()
        try:
            for mode in ("session-start", "subagent-start", "stop"):
                r = script("hooks.py", mode, stdin=json.dumps({"cwd": empty}))
                self.assertEqual((r.returncode, r.stdout, r.stderr), (0, "", ""), mode)
        finally:
            shutil.rmtree(empty)

    def test_bad_ref_in_note_is_marked(self):
        path = self.put(".studio/handoff/T-003.md", "# T-003\nfiles: see `Code/Gameplay/Nope.cs:12`\n")
        r = self.hook("post-edit", tool_name="Write", tool_input={"file_path": path})
        self.assertEqual(r.returncode, 2)
        self.assertIn("UNVERIFIED", text(path))


class Status(Project):
    def test_session_start_prints_open_rows_and_console(self):
        self.put(".studio/board.md", "| ID | task | owner | status | acceptance |\n|---|---|---|---|---|\n"
                 "| T-001 | jump | gameplay-coder | done | ok |\n| T-002 | dash | gameplay-coder | review | ok |\n")
        self.put(".studio/console.log", "# 10:00:00 compile failed: 1 error(s) · 1 entry\ncompile-error A.cs:3 CS1002\n")
        self.put(".studio/test-results.txt", "# 10:01:00 passed 3 · failed 1 · skipped 0 · inconclusive 0\n")
        r = self.hook("session-start")
        self.assertEqual(r.returncode, 0)
        self.assertIn("1 open board row", r.stdout)
        self.assertIn("T-002 dash | gameplay-coder | review", r.stdout)
        self.assertNotIn("T-001", r.stdout)
        self.assertIn("1 error line", r.stdout)
        self.assertIn("failed 1", r.stdout)

    def test_console_play_marker_packages_and_stale_handoff(self):
        self.put(".studio/board.md", "| ID | task | owner | status | acceptance |\n| T-003 | vfx | vfx-artist | review | ok |\n")
        self.put(".studio/handoff/T-003.md", "# T-003 vfx · vfx-artist · 2026-11-10\nopen: blocked - no art bible\n")
        self.put(".studio/console.log", "# 10:00:00 play · 3 entries\n[09:59:00] package-error [edit] Assembly X already exists (Packages/a)\n"
                 "[10:00:00] play start\n[10:00:01] exception [play] Assets/_Project/A.cs:3 NullReferenceException (x4)\n")
        out = self.hook("session-start").stdout
        self.assertIn("1 error line(s)", out)
        self.assertIn("1 from Unity packages", out)
        self.assertIn("NullReferenceException", out)
        self.assertIn("stale handoffs (board review/done, handoff says blocked): T-003", out)

    def test_no_brief_no_output(self):
        os.remove(os.path.join(self.root, ".studio", "brief.md"))
        self.assertEqual(self.hook("session-start").stdout, "")


    def test_last_play_check_line(self):
        self.put(".studio/play-check.txt", "# 17:19:57 play check · Assets/_Project/Scenes/Puzzle.unity · 1 s · clean\nframes 41\n")
        self.assertIn("last play check: 17:19:57 play check · Assets/_Project/Scenes/Puzzle.unity · 1 s · clean",
                      self.hook("session-start").stdout)


class Install(Project):
    def setUp(self):
        super().setUp()
        self.put(".studio/brief.md", "# Brief · Test\nns: Acme.Game\n")

    def asmdef(self, module):
        folder = os.path.join(self.root, "Assets", "_Project", "Code", module)
        return json.loads(text(os.path.join(folder, [f for f in os.listdir(folder) if f.endswith(".asmdef")][0])))

    def test_editor_defines_and_lock_packages(self):
        self.put("Packages/manifest.json", json.dumps({"dependencies": {"com.unity.ugui": "2.0.0", "com.unity.cinemachine": "3.1.4"}}))
        self.put("Packages/packages-lock.json", json.dumps({"dependencies": {"com.unity.2d.animation": {"version": "10.1.4"},
                                                                             "com.unity.ugui": {"version": "1.0.0"}}}))
        self.assertEqual(script("asmdefs.py", self.root).returncode, 0)
        editor = self.asmdef("Editor")
        defines = {d["define"] for d in editor["versionDefines"]}
        self.assertTrue({"STUDIO_UGUI", "STUDIO_TMP", "STUDIO_CINEMACHINE"} <= defines)
        self.assertIn("Unity.Cinemachine", editor["references"])
        self.assertIn("Unity.2D.Animation.Runtime", self.asmdef("Gameplay")["references"], "lock-file package counts")

    def test_existing_editor_asmdef_gets_missing_defines_once(self):
        self.put("Assets/_Project/Code/Editor/Acme.Game.Editor.asmdef", json.dumps({"name": "Acme.Game.Editor", "references": []}))
        script("asmdefs.py", self.root)
        script("asmdefs.py", self.root)
        defines = [d["define"] for d in self.asmdef("Editor")["versionDefines"]]
        self.assertIn("STUDIO_URP", defines)
        self.assertEqual(len(defines), len(set((d["name"], d["define"]) for d in self.asmdef("Editor")["versionDefines"])))

    def test_pack_replaces_namespace_and_keeps_existing(self):
        mine = self.put("Assets/_Project/Code/Puzzle/Runtime/Rng.cs", "// mine\n")
        r = script("install_unity.py", self.root, "--pack", "match3")
        self.assertEqual(r.returncode, 0, r.stderr)
        self.assertIn("kept 1 existing", r.stdout)
        self.assertEqual(text(mine), "// mine\n")
        code = os.path.join(self.root, "Assets", "_Project", "Code", "Puzzle")
        asm = json.loads(text(os.path.join(code, "Runtime", "Acme.Game.Puzzle.asmdef")))
        self.assertEqual(asm["name"], "Acme.Game.Puzzle")
        self.assertIn("namespace Acme.Game.Puzzle", text(os.path.join(code, "Runtime", "Board.cs")))
        self.assertTrue(os.path.exists(os.path.join(self.root, "Assets", "_Project", "Data", "Levels", "001_easy.txt")))
        for folder, _, files in os.walk(code):
            for f in files:
                self.assertNotIn("__NS__", f + text(os.path.join(folder, f)), f)
        self.assertFalse(os.path.exists(os.path.join(code, "Sim~")), "dotnet-only folders stay in the skill")
        self.assertTrue(os.path.exists(os.path.join(self.root, "Assets", "_Project", "Code", "Editor", "StudioPlayCheck.cs")))

    def test_check_code_wants_using_for_new_tools(self):
        self.put("Assets/_Project/Code/Editor/B.cs", "namespace A.Editor\n{\n    public static class B\n    {\n"
                 "        public static void Run() => StudioPlayCheck.Run(\"\");\n    }\n}\n")
        r = script("check_code.py", self.root, "--files", "Assets/_Project/Code/Editor/B.cs")
        self.assertEqual(r.returncode, 1)
        self.assertIn("Studio.EditorTools", r.stdout)


class ProjectMap(Project):
    def test_map_lists_types(self):
        self.put("Assets/_Project/Code/Gameplay/Clean.cs", CLEAN)
        r = script("project_map.py", self.root)
        self.assertEqual(r.returncode, 0, r.stderr)
        self.assertIn("Clean", text(os.path.join(self.root, ".studio", "PROJECT_MAP.md")))


class CompileCheck(unittest.TestCase):
    LOG = ("[ScriptCompilation] Requested script compilation because: x\n"
           "Assets\\_Project\\Code\\A.cs(3,5): error CS0103: The name 'Foo' does not exist in the current context\n"
           "*** Tundra build failed (1.0 seconds), 2 items updated\n"
           "Assets\\_Project\\Code\\A.cs(3,5): error CS0103: The name 'Foo' does not exist in the current context\n")

    def setUp(self):
        sys.path.insert(0, SCRIPTS)
        import compile_check
        self.cc = compile_check

    def test_last_failed_compile_errors_deduped(self):
        self.assertEqual(self.cc.last_compile_errors(self.LOG),
                         ["Assets/_Project/Code/A.cs(3,5): error CS0103: The name 'Foo' does not exist in the current context"])

    def test_success_after_failure_is_clean(self):
        log = self.LOG + "[ScriptCompilation] Requested script compilation because: y\n*** Tundra build success (1.0 seconds)\n"
        self.assertEqual(self.cc.last_compile_errors(log), [])

    def test_dotnet_absolute_paths(self):
        line = "C:\\P\\Assets\\_Project\\B.cs(9,1): error CS1002: ; expected [C:\\P\\Game.csproj]"
        self.assertEqual(self.cc.errors_from(line), ["Assets/_Project/B.cs(9,1): error CS1002: ; expected"])

    def test_assembly_from_asmdef_or_default(self):
        root = tempfile.mkdtemp()
        try:
            os.makedirs(os.path.join(root, "Assets", "_Project", "Code", "Gameplay"))
            os.makedirs(os.path.join(root, "Assets", "Misc", "Editor"))
            with open(os.path.join(root, "Assets", "_Project", "Code", "Gameplay", "G.asmdef"), "w") as fh:
                fh.write('{"name": "Acme.Game.Gameplay"}')
            self.assertEqual(self.cc.assembly_of(root, "Assets/_Project/Code/Gameplay/Sub/X.cs"), "Acme.Game.Gameplay")
            self.assertEqual(self.cc.assembly_of(root, "Assets/Misc/Editor/Y.cs"), "Assembly-CSharp-Editor")
            self.assertEqual(self.cc.assembly_of(root, "Assets/Misc/Z.cs"), "Assembly-CSharp")
        finally:
            shutil.rmtree(root)


class Specs(Project):
    def test_vfx_spec_without_art_bible_uses_placeholder_roles(self):
        r = script("vfx_spec.py", "new", "Boom", "--preset", "explosion", "--root", self.root)
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
        self.assertIn("placeholder roles", r.stdout)
        self.assertEqual(script("vfx_spec.py", "build", "--root", self.root).returncode, 0)

    def test_devlog_takes_agent_from_board_when_header_lacks_it(self):
        self.put(".studio/board.md", "| ID | task | owner | status | acceptance |\n| T-002 | x | gameplay-coder | review | ok |\n")
        self.put(".studio/handoff/T-002.md", "# T-002 handoff 2026-11-10\ngoal: move player\nfiles: Code/Gameplay/A.cs · B.cs\n")
        self.put("Assets/_Project/Code/Gameplay/A.cs", CLEAN)
        self.put("Assets/_Project/Code/Gameplay/B.cs", CLEAN)
        r = script("devlog.py", "producer", "--summary", "--root", self.root, "--date", "2026-11-10")
        self.assertIn("2026-11-10-gameplay-coder.md", r.stdout)
        self.assertIn("T-002: move player", r.stdout)
        self.assertNotIn("UNVERIFIED", r.stdout)


MAGENTA, BLUE, RED, CLEAR_PX = (255, 0, 255, 255), (40, 60, 200, 255), (200, 40, 40, 255), (0, 0, 0, 0)


def raw_png(width, height, ctype, rows, kind=0, bpp=4, extra=b""):
    """Hand-built 8-bit PNG; every row encoded with the given filter type (0 none, 4 Paeth)."""
    import struct
    import zlib

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    body, prev = b"", bytes(len(rows[0]))
    for row in rows:
        enc = bytearray(row)
        if kind == 4:
            for i in range(len(row)):
                a = row[i - bpp] if i >= bpp else 0
                b, c = prev[i], prev[i - bpp] if i >= bpp else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                enc[i] = (row[i] - (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        body += bytes([kind]) + bytes(enc)
        prev = row
    head = struct.pack(">IIBBBBB", width, height, 8, ctype, 0, 0, 0)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", head) + extra + chunk(b"IDAT", zlib.compress(body)) + chunk(b"IEND", b"")


class SpriteFromImage(Project):
    def setUp(self):
        super().setUp()
        sys.path.insert(0, SCRIPTS)
        import sprite_from_image
        import placeholder_sprites
        self.sfi, self.png = sprite_from_image, placeholder_sprites.png

    def image(self, name, width, height, rects, bg=MAGENTA):
        """rects: (x0, y0, x1, y1, colour) painted in order over a flat background."""
        pixels = [bg] * (width * height)
        for x0, y0, x1, y1, colour in rects:
            for y in range(y0, y1):
                pixels[y * width + x0:y * width + x1] = [colour] * (x1 - x0)
        path = os.path.join(self.root, name)
        with open(path, "wb") as fh:
            fh.write(self.png(width, height, pixels))
        return path

    def run_sfi(self, path, *args):
        return script("sprite_from_image.py", path, "--root", self.root, *args)

    def read(self, rel):
        with open(os.path.join(self.root, rel), "rb") as fh:
            w, h, rgba = self.sfi.decode_png(fh.read())
        return w, h, [tuple(rgba[i:i + 4]) for i in range(0, len(rgba), 4)]

    def column(self, px, w, h, x):
        return [px[y * w + x] for y in range(h)]

    def test_key_removal_keeps_interior_island(self):
        path = self.image("Blob.png", 20, 20, [(4, 4, 16, 16, BLUE), (9, 9, 11, 11, MAGENTA)])
        r = self.run_sfi(path, "--cell", "20x20", "--no-palette")
        self.assertEqual(r.returncode, 0, r.stderr)
        self.assertIn("key #ff00ff", r.stdout)
        w, h, px = self.read("Assets/_Project/Art/Sprites/Blob.png")
        self.assertEqual((w, h), (20, 20))
        self.assertEqual(px[0], CLEAR_PX)
        self.assertEqual(px[13 * 20 + 9], MAGENTA)  # island: 12x12 block sits at x 4..15, y 8..19
        self.assertEqual(px[19 * 20 + 4], BLUE)

    def test_frames_detected_with_uneven_gaps(self):
        path = self.image("Bat.png", 60, 12, [(2, 2, 9, 12, BLUE), (13, 4, 23, 12, BLUE), (40, 0, 48, 12, BLUE)])
        r = self.run_sfi(path, "--cell", "16x16", "--frames", "3", "--no-palette")
        self.assertEqual(r.returncode, 0, r.stderr)
        self.assertIn("Bat_sheet_16x16.png · 3 frames 16x16 · key", r.stdout)
        self.assertNotIn("equal split", r.stdout)
        w, h, px = self.read("Assets/_Project/Art/Sprites/Bat_sheet_16x16.png")
        widths = [sum(px[15 * w + f * 16 + x] != CLEAR_PX for x in range(16)) for f in range(3)]
        self.assertEqual(widths, [7, 10, 8])

    def test_equal_division_fallback(self):
        path = self.image("Bar.png", 40, 10, [(0, 2, 40, 10, BLUE)])
        r = self.run_sfi(path, "--cell", "20x10", "--frames", "2", "--key", "#ff00ff", "--no-palette")
        self.assertEqual(r.returncode, 0, r.stderr)
        self.assertIn("(equal split)", r.stdout)
        w, h, px = self.read("Assets/_Project/Art/Sprites/Bar_sheet_20x10.png")
        self.assertEqual(w, 40)
        self.assertTrue(all(px[9 * w + x] == BLUE for x in range(w)))

    def test_one_scale_short_frame_stays_short_and_bottom_aligned(self):
        path = self.image("Hop.png", 40, 40, [(4, 4, 12, 36, BLUE), (24, 20, 32, 36, RED)])
        r = self.run_sfi(path, "--cell", "16x16", "--frames", "2", "--no-palette")
        self.assertEqual(r.returncode, 0, r.stderr)
        w, h, px = self.read("Assets/_Project/Art/Sprites/Hop_sheet_16x16.png")
        tall = self.column(px, w, h, 8)
        short = self.column(px, w, h, 24)
        self.assertEqual(sum(p == BLUE for p in tall), 16)
        self.assertEqual(sum(p == RED for p in short), 8)
        self.assertEqual(short[15], RED)  # feet on the bottom row
        self.assertEqual(short[7], CLEAR_PX)
        self.assertEqual(sum(p != CLEAR_PX for p in px[15 * w:16 * w]), 8)  # 4 px wide each after the 2x scale

    def test_palette_set_reports_every_problem_at_once(self):
        r = script("palette.py", "set", "bg_dark=#1b1f3b", "x=#123", "--root", self.root)
        self.assertNotEqual(r.returncode, 0)
        for part in ("'x=#123' is not role=#rrggbb", "1 colours", "'background'"):
            self.assertIn(part, r.stderr)
        r = script("palette.py", "set", "Background=#1b1f3b", "player_main=#f2f2e9", "enemy=#e04f5f", "reward=#ffd34d",
                   "ground=#4f7a4a", "--root", self.root)
        self.assertEqual(r.returncode, 0, r.stderr)
        self.assertIn("| player-main |", r.stdout)

    def test_palette_mapping_leaves_only_palette_colours(self):
        self.assertEqual(script("palette.py", "set", "background=#1b1f3b", "midground=#3e4a7a", "interactive=#f2f2e9",
                                "hazard=#e04f5f", "reward=#ffd34d", "--root", self.root).returncode, 0)
        path = self.image("Gem.png", 16, 16, [(4, 4, 12, 16, (230, 90, 100, 255)), (6, 6, 10, 10, (250, 215, 80, 255))])
        r = self.run_sfi(path, "--cell", "16x16")
        self.assertEqual(r.returncode, 0, r.stderr)
        self.assertIn("palette 5 roles, drift", r.stdout)
        allowed = {(0x1b, 0x1f, 0x3b), (0x3e, 0x4a, 0x7a), (0xf2, 0xf2, 0xe9), (0xe0, 0x4f, 0x5f), (0xff, 0xd3, 0x4d)}
        _, _, px = self.read("Assets/_Project/Art/Sprites/Gem.png")
        used = {p[:3] for p in px if p[3]}
        self.assertTrue(used and used <= allowed, used)

    def test_outline_only_on_transparent_neighbours(self):
        path = self.image("Box.png", 12, 12, [(4, 4, 8, 8, BLUE)])
        r = self.run_sfi(path, "--cell", "8x8", "--no-palette", "--outline", "#000000")
        self.assertEqual(r.returncode, 0, r.stderr)
        _, _, px = self.read("Assets/_Project/Art/Sprites/Box.png")
        block = {(x, y) for x in range(2, 6) for y in range(4, 8)}
        ring = {(x + dx, y + dy) for x, y in block for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))} - block
        ring = {(x, y) for x, y in ring if 0 <= y < 8}
        for y in range(8):
            for x in range(8):
                want = BLUE if (x, y) in block else (0, 0, 0, 255) if (x, y) in ring else CLEAR_PX
                self.assertEqual(px[y * 8 + x], want, (x, y))

    def test_no_overwrite_without_force(self):
        path = self.image("Coin.png", 8, 8, [(2, 2, 6, 6, BLUE)])
        self.assertEqual(self.run_sfi(path, "--cell", "8x8", "--no-palette").returncode, 0)
        again = self.run_sfi(path, "--cell", "8x8", "--no-palette")
        self.assertEqual(again.returncode, 1)
        self.assertIn("--force", again.stderr)
        self.assertEqual(self.run_sfi(path, "--cell", "8x8", "--no-palette", "--force").returncode, 0)

    def test_custom_out_without_sheet_suffix_warns(self):
        path = self.image("Walk.png", 30, 10, [(1, 1, 9, 10, BLUE), (20, 1, 28, 10, BLUE)])
        out = os.path.join(self.root, "Assets", "_Project", "Art", "Sprites", "Walk.png")
        r = self.run_sfi(path, "--cell", "10x10", "--frames", "2", "--out", out, "--no-palette")
        self.assertEqual(r.returncode, 0, r.stderr)
        self.assertIn("lacks _sheet_10x10", r.stderr)
        self.assertTrue(os.path.exists(out))

    def test_missing_palette_note(self):
        path = self.image("Orb.png", 8, 8, [(2, 2, 6, 6, BLUE)])
        r = self.run_sfi(path, "--cell", "8x8")
        self.assertEqual(r.returncode, 0, r.stderr)
        self.assertIn("no palette (python palette.py set ...)", r.stdout)
        self.assertEqual(len(r.stdout.strip().splitlines()), 1)

    def test_builtin_decoder_paeth_and_rgb(self):
        rgba_rows = [bytes((x * 30 + y * 7) % 256 for x in range(5 * 4)) for y in range(4)]
        w, h, rgba = self.sfi.decode_png(raw_png(5, 4, 6, rgba_rows, kind=4, bpp=4))
        self.assertEqual((w, h, bytes(rgba)), (5, 4, b"".join(rgba_rows)))
        import struct
        import zlib
        trns = b"tRNS" + struct.pack(">HHH", 255, 0, 255)
        trns = struct.pack(">I", 6) + trns + struct.pack(">I", zlib.crc32(trns) & 0xFFFFFFFF)
        rgb_rows = [bytes([255, 0, 255, 10, 20, 30]), bytes([40, 50, 60, 255, 0, 255])]
        w, h, rgba = self.sfi.decode_png(raw_png(2, 2, 2, rgb_rows, kind=4, bpp=3, extra=trns))
        self.assertEqual(bytes(rgba), bytes([255, 0, 255, 0, 10, 20, 30, 255, 40, 50, 60, 255, 255, 0, 255, 0]))
        interlaced = bytearray(raw_png(1, 1, 6, [bytes(4)]))
        interlaced[28] = 1  # IHDR interlace byte (CRC is not checked)
        with self.assertRaisesRegex(ValueError, "interlaced"):
            self.sfi.decode_png(bytes(interlaced))


class BlenderSprites(Project):
    def test_new_writes_spec_once(self):
        r = script("blender_sprites.py", "new", "gems", "--root", self.root)
        self.assertEqual(r.returncode, 0, r.stderr)
        spec = json.loads(text(os.path.join(self.root, ".studio", "art", "blender", "gems.json")))
        self.assertEqual(len(spec["items"]), 6)
        self.assertNotEqual(script("blender_sprites.py", "new", "gems", "--root", self.root).returncode, 0, "never overwrites")

    def test_unknown_palette_role_is_rejected_before_blender(self):
        sys.path.insert(0, SCRIPTS)
        import blender_sprites
        spec = {"out": "Assets/x", "items": [{"name": "A", "kind": "gem", "color": "nope"}]}
        with self.assertRaises(SystemExit):
            blender_sprites.resolve(self.root, spec, None, None)
        spec["items"][0]["color"] = "#ff0000"
        self.assertEqual(blender_sprites.resolve(self.root, spec, None, 64)["size"], 64)

    def test_render_when_blender_installed(self):
        if script("blender_sprites.py", "check").returncode != 0:
            self.skipTest("Blender not installed")
        self.put(".studio/art/blender/one.json", json.dumps({"out": "Assets/_Project/Art/Sprites", "size": 32, "view": "top",
                                                             "items": [{"name": "Gem_A", "kind": "gem", "sides": 4, "color": "#3d8be8"}]}))
        r = script("blender_sprites.py", "render", "one", "--root", self.root)
        self.assertEqual(r.returncode, 0, r.stderr)
        self.assertTrue(os.path.exists(os.path.join(self.root, "Assets", "_Project", "Art", "Sprites", "Gem_A.png")))
        self.assertTrue(os.path.exists(os.path.join(self.root, ".studio", "art", "blender", "one_preview.png")))


class PluginFiles(unittest.TestCase):
    ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

    def test_agents_have_required_frontmatter(self):
        folder = os.path.join(self.ROOT, "agents")
        for name in os.listdir(folder):
            head = text(os.path.join(folder, name)).split("---")[1]
            for field in ("name:", "description:", "model:", "maxTurns:"):
                self.assertIn(field, head, f"{name} lacks {field}")
            self.assertTrue("disallowedTools:" in head or "tools:" in head, f"{name} can spawn subagents")

    def test_hook_scripts_exist(self):
        hooks = json.loads(text(os.path.join(self.ROOT, "hooks", "hooks.json")))["hooks"]
        modes = {h["command"].split()[-1] for groups in hooks.values() for g in groups for h in g["hooks"]}
        self.assertEqual(modes, {"session-start", "subagent-start", "post-edit", "stop"})

    def test_hook_launcher_finds_python(self):
        if not shutil.which("sh"):
            self.skipTest("no sh")
        r = subprocess.run(["sh", os.path.join(self.ROOT, "hooks", "run.sh"), "session-start"], input="{}", capture_output=True,
                           text=True, cwd=tempfile.gettempdir())
        self.assertEqual(r.returncode, 0)
        self.assertNotIn("not found", r.stderr)

    def test_versions_match(self):
        plugin = json.loads(text(os.path.join(self.ROOT, ".claude-plugin", "plugin.json")))
        changelog = text(os.path.join(self.ROOT, "CHANGELOG.md"))
        self.assertIn(f"## v{plugin['version']}", changelog)


if __name__ == "__main__":
    unittest.main()
