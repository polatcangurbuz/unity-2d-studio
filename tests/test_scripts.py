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
        self.assertIn("unassigned refs _target", r.stdout)

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
        self.put(".studio/handoff/T-002.md", "# T-002 · vfx-artist\n")
        self.hook("subagent-start", **ev)
        time.sleep(0.05)
        self.put("Assets/_Project/Scenes/Level.unity", SCENE)
        r = self.hook("stop", **ev)
        self.assertEqual(r.returncode, 2)
        self.assertIn("CompositeCollider2D", r.stderr)

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

    def test_no_brief_no_output(self):
        os.remove(os.path.join(self.root, ".studio", "brief.md"))
        self.assertEqual(self.hook("session-start").stdout, "")


class ProjectMap(Project):
    def test_map_lists_types(self):
        self.put("Assets/_Project/Code/Gameplay/Clean.cs", CLEAN)
        r = script("project_map.py", self.root)
        self.assertEqual(r.returncode, 0, r.stderr)
        self.assertIn("Clean", text(os.path.join(self.root, ".studio", "PROJECT_MAP.md")))


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

    def test_versions_match(self):
        plugin = json.loads(text(os.path.join(self.ROOT, ".claude-plugin", "plugin.json")))
        changelog = text(os.path.join(self.ROOT, "CHANGELOG.md"))
        self.assertIn(f"## v{plugin['version']}", changelog)


if __name__ == "__main__":
    unittest.main()
