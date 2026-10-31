using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Studio.EditorTools
{
    public static class StudioSetup
    {
        private const string Root = "Assets/_Project";
        private const string BootstrapScenePath = Root + "/Scenes/Bootstrap.unity";
        private const string Templates = Root + "/Code/Editor/Templates~";
        private const string DefaultSceneFolder = "Assets/Scenes";
        private const string DefaultScenePath = DefaultSceneFolder + "/SampleScene.unity";
        private static readonly string[] DefaultSceneObjects = { "Main Camera", "Global Light 2D", "Directional Light" };

        private static readonly string[] Folders =
        {
            "Art/Sprites", "Art/Animations", "Art/Materials", "Art/Shaders", "Art/VFX", "Art/UI", "Art/Fonts",
            "Audio/Music", "Audio/SFX", "Audio/Mixers",
            "Code/Core", "Code/Gameplay", "Code/UI", "Code/Editor", "Code/Tests/EditMode", "Code/Tests/PlayMode",
            "Data", "Prefabs", "Scenes", "Settings",
        };

        private static readonly string[] Separators =
        {
            "--- MANAGERS ---", "--- ENVIRONMENT ---", "--- GAMEPLAY ---", "--- UI ---", "--- CAMERAS ---", "--- LIGHTING ---",
        };

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        [MenuItem("Tools/Studio/Setup Project")]
        public static void SetupProject()
        {
            var log = new StringBuilder("[StudioSetup]\n");
            var ns = ResolveNamespace(log);

            foreach (var folder in Folders.Select(f => "_Project/" + f).Append("ThirdParty"))
            {
                var path = Path.Combine(Application.dataPath, folder);
                if (Directory.Exists(path)) continue;
                Directory.CreateDirectory(path);
                log.AppendLine($"+ folder Assets/{folder}");
            }

            EnsurePackage("com.unity.test-framework", log);
            StudioAsmdefs.Create(ns, log);
            ConfigureEditorSettings(log);
            CopyGitFiles(log);
            AssetDatabase.Refresh();
            CreateBootstrapScene(log);
            RelocateDefaultScene(log);

            log.AppendLine(GitMergeHelp());
            Debug.Log(log.ToString());
        }

        [MenuItem("Tools/Studio/Add Hierarchy Separators")]
        public static void AddSeparatorsToOpenScene()
        {
            var added = AddSeparators(SceneManager.GetActiveScene());
            if (added > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[StudioSetup] {added} separator(s) added");
        }

        private static string ResolveNamespace(StringBuilder log)
        {
            var brief = Path.Combine(ProjectRoot, ".studio", "brief.md");
            if (File.Exists(brief))
            {
                var match = Regex.Match(File.ReadAllText(brief), @"\bns:\s*([A-Za-z_][\w.]*)");
                if (match.Success)
                {
                    log.AppendLine($"namespace {match.Groups[1].Value} (from .studio/brief.md)");
                    return match.Groups[1].Value;
                }
            }

            var ns = $"{Sanitize(PlayerSettings.companyName)}.{Sanitize(PlayerSettings.productName)}";
            log.AppendLine($"namespace {ns} (from Player Settings; add 'ns: X.Y' to .studio/brief.md to override)");
            return ns;
        }

        private static string Sanitize(string value)
        {
            var clean = Regex.Replace(value ?? string.Empty, @"[^A-Za-z0-9]", string.Empty);
            return clean.Length == 0 || char.IsDigit(clean[0]) ? "Game" + clean : clean;
        }

        // Test asmdefs reference NUnit, which only exists once the test framework package is installed.
        private static void EnsurePackage(string id, StringBuilder log)
        {
            var manifest = Path.Combine(ProjectRoot, "Packages", "manifest.json");
            if (File.Exists(manifest) && File.ReadAllText(manifest).Contains($"\"{id}\"")) return;

            var request = Client.Add(id);
            var deadline = EditorApplication.timeSinceStartup + 120;
            while (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) Thread.Sleep(100);

            if (request.Status == StatusCode.Success) log.AppendLine($"+ package {request.Result.packageId}");
            else log.AppendLine($"! package {id} not added ({request.Error?.message ?? "timeout"}); add it in Package Manager");
        }

        private static void ConfigureEditorSettings(StringBuilder log)
        {
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                EditorSettings.serializationMode = SerializationMode.ForceText;
                log.AppendLine("* serialization = Force Text");
            }

            if (VersionControlSettings.mode != "Visible Meta Files")
            {
                VersionControlSettings.mode = "Visible Meta Files";
                log.AppendLine("* version control = Visible Meta Files");
            }
        }

        private static void CopyGitFiles(StringBuilder log)
        {
            CopyIfMissing(Path.Combine(ProjectRoot, Templates, "gitignore.txt"), Path.Combine(ProjectRoot, ".gitignore"), log);
            CopyIfMissing(Path.Combine(ProjectRoot, Templates, "gitattributes.txt"), Path.Combine(ProjectRoot, ".gitattributes"), log);
        }

        private static void CopyIfMissing(string source, string target, StringBuilder log)
        {
            if (File.Exists(target)) return;
            if (!File.Exists(source))
            {
                log.AppendLine($"! {Path.GetFileName(source)} not found at {source}; copy it manually");
                return;
            }

            File.Copy(source, target);
            log.AppendLine($"+ {Path.GetFileName(target)}");
        }

        private static void CreateBootstrapScene(StringBuilder log)
        {
            if (!File.Exists(Path.Combine(ProjectRoot, BootstrapScenePath)))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    log.AppendLine("! Bootstrap scene skipped (unsaved scene kept open)");
                    return;
                }

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AddSeparators(scene);
                var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
                var camera = cameraGo.AddComponent<Camera>();
                camera.orthographic = true;
                cameraGo.transform.position = new Vector3(0f, 0f, -10f);
                cameraGo.transform.SetSiblingIndex(SeparatorIndex(scene, "--- CAMERAS ---") + 1);
                EditorSceneManager.SaveScene(scene, BootstrapScenePath);
                log.AppendLine($"+ scene {BootstrapScenePath}");
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.path != BootstrapScenePath).ToList();
            if (EditorBuildSettings.scenes.Length == 0 || EditorBuildSettings.scenes[0].path != BootstrapScenePath)
            {
                scenes.Insert(0, new EditorBuildSettingsScene(BootstrapScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                log.AppendLine("* Bootstrap = build index 0");
            }
        }

        private static void RelocateDefaultScene(StringBuilder log)
        {
            var fullPath = Path.Combine(ProjectRoot, DefaultScenePath);
            if (!File.Exists(fullPath)) return;

            // Unity's template scene holds only a camera/light; anything else is user work and must be kept.
            var names = Regex.Matches(File.ReadAllText(fullPath), @"--- !u!1 &\d+\s+GameObject:[\s\S]*?m_Name: (.*)")
                .Cast<Match>().Select(m => m.Groups[1].Value.Trim());
            var target = Root + "/Scenes/SampleScene.unity";
            if (names.All(DefaultSceneObjects.Contains))
            {
                AssetDatabase.DeleteAsset(DefaultScenePath);
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != DefaultScenePath).ToArray();
                log.AppendLine($"- {DefaultScenePath} (Unity default, empty)");
            }
            else if (!File.Exists(Path.Combine(ProjectRoot, target)))
            {
                var error = AssetDatabase.MoveAsset(DefaultScenePath, target);
                if (!string.IsNullOrEmpty(error))
                {
                    log.AppendLine($"! {DefaultScenePath} not moved: {error}");
                    return;
                }
                EditorBuildSettings.scenes = EditorBuildSettings.scenes
                    .Select(s => s.path == DefaultScenePath ? new EditorBuildSettingsScene(target, s.enabled) : s).ToArray();
                log.AppendLine($"> {DefaultScenePath} → {target}");
            }

            else log.AppendLine($"! {DefaultScenePath} kept: {target} already exists; merge or delete by hand");

            if (AssetDatabase.IsValidFolder(DefaultSceneFolder)
                && !Directory.EnumerateFileSystemEntries(Path.Combine(ProjectRoot, DefaultSceneFolder)).Any())
                AssetDatabase.DeleteAsset(DefaultSceneFolder);
        }

        private static int AddSeparators(Scene scene)
        {
            var existing = new HashSet<string>(scene.GetRootGameObjects().Select(go => go.name));
            var added = 0;
            foreach (var label in Separators)
            {
                if (existing.Contains(label)) continue;
                var go = new GameObject(label) { tag = "EditorOnly" };
                SceneManager.MoveGameObjectToScene(go, scene);
                added++;
            }

            return added;
        }

        private static int SeparatorIndex(Scene scene, string label) =>
            scene.GetRootGameObjects().First(go => go.name == label).transform.GetSiblingIndex();

        private static string GitMergeHelp()
        {
            var tool = Path.Combine(EditorApplication.applicationContentsPath, "Tools", "UnityYAMLMerge").Replace('\\', '/');
            if (Application.platform == RuntimePlatform.WindowsEditor) tool += ".exe";
            return "Smart merge (run once in the repo):\n" +
                   "git config merge.unityyamlmerge.name \"Unity SmartMerge\"\n" +
                   $"git config merge.unityyamlmerge.driver \"'{tool}' merge -p %O %B %A %A\"\n" +
                   "git lfs install";
        }
    }
}
