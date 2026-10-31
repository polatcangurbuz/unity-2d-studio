using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Studio.EditorTools
{
    // Same layout and package rules as scripts/asmdefs.py, so either one can create the asmdefs first.
    public static class StudioAsmdefs
    {
        private const string TestRunnerDefine = "STUDIO_TEST_RUNNER";

        private static readonly (string package, string reference, string[] modules)[] PackageRefs =
        {
            ("com.unity.inputsystem", "Unity.InputSystem", new[] { "Gameplay", "UI" }),
            ("com.unity.ugui", "UnityEngine.UI", new[] { "UI", "Editor" }),
            ("com.unity.render-pipelines.universal", "Unity.RenderPipelines.Core.Runtime", new[] { "Gameplay", "Editor" }),
            ("com.unity.render-pipelines.universal", "Unity.RenderPipelines.Universal.Runtime", new[] { "Gameplay", "Editor" }),
            ("com.unity.render-pipelines.universal", "Unity.RenderPipelines.Universal.2D.Runtime", new[] { "Gameplay", "Editor" }),
            ("com.unity.test-framework", "UnityEngine.TestRunner", new[] { "Editor" }),
            ("com.unity.test-framework", "UnityEditor.TestRunner", new[] { "Editor" }),
        };

        public static void Create(string ns, StringBuilder log)
        {
            var packages = ReadPackages();
            var testRefs = new[] { $"{ns}.Core", $"{ns}.Gameplay", "UnityEngine.TestRunner" };
            var editor = new[] { "Editor" };

            Write("Core", ns, new string[0], null, false, packages, log);
            Write("Gameplay", ns, new[] { $"{ns}.Core" }, null, false, packages, log);
            Write("UI", ns, new[] { $"{ns}.Core", $"{ns}.Gameplay" }, null, false, packages, log);
            Write("Editor", ns, new[] { $"{ns}.Core", $"{ns}.Gameplay", $"{ns}.UI" }, editor, false, packages, log);
            Write("Tests/EditMode", ns, testRefs.Append("UnityEditor.TestRunner").ToArray(), editor, true, packages, log);
            Write("Tests/PlayMode", ns, testRefs, null, true, packages, log);
        }

        private static Dictionary<string, string> ReadPackages()
        {
            var manifest = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Packages", "manifest.json");
            var text = File.Exists(manifest) ? File.ReadAllText(manifest) : string.Empty;
            return Regex.Matches(text, "\"(com\\.[\\w.-]+)\"\\s*:\\s*\"([^\"]*)\"").Cast<Match>()
                .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
        }

        private static List<string> PackageReferences(string module, Dictionary<string, string> packages)
        {
            var refs = PackageRefs.Where(r => packages.ContainsKey(r.package) && r.modules.Contains(module))
                .Select(r => r.reference).ToList();
            // TMP ships inside uGUI 2.x (Unity 6); older projects have it as its own package.
            var ugui2 = packages.TryGetValue("com.unity.ugui", out var v) && !v.StartsWith("1.");
            if ((module == "UI" || module == "Editor") && (ugui2 || packages.ContainsKey("com.unity.textmeshpro")))
                refs.Add("Unity.TextMeshPro");
            return refs;
        }

        private static void Write(string module, string ns, string[] own, string[] platforms, bool isTest,
            Dictionary<string, string> packages, StringBuilder log)
        {
            var dir = Path.Combine(Application.dataPath, "_Project", "Code", module);
            Directory.CreateDirectory(dir);
            var extra = isTest ? new List<string>() : PackageReferences(module, packages);
            var testRunner = module == "Editor" && packages.ContainsKey("com.unity.test-framework");
            var existing = Directory.GetFiles(dir, "*.asmdef").FirstOrDefault();
            if (existing != null)
            {
                Merge(existing, extra, testRunner, log);
                return;
            }

            var name = $"{ns}.{module.Replace('/', '.')}";
            var json = new StringBuilder("{\n");
            json.Append($"    \"name\": \"{name}\",\n");
            json.Append($"    \"rootNamespace\": \"{(isTest ? ns + ".Tests" : name)}\",\n");
            json.Append($"    \"references\": {JsonArray(own.Concat(extra))},\n");
            json.Append($"    \"includePlatforms\": {JsonArray(platforms ?? new string[0])},\n");
            json.Append("    \"excludePlatforms\": [],\n");
            json.Append("    \"allowUnsafeCode\": false,\n");
            json.Append($"    \"overrideReferences\": {(isTest ? "true" : "false")},\n");
            json.Append($"    \"precompiledReferences\": {JsonArray(isTest ? new[] { "nunit.framework.dll" } : new string[0])},\n");
            json.Append($"    \"autoReferenced\": {(isTest ? "false" : "true")},\n");
            json.Append($"    \"defineConstraints\": {JsonArray(isTest ? new[] { "UNITY_INCLUDE_TESTS" } : new string[0])},\n");
            json.Append($"    \"versionDefines\": {(testRunner ? TestRunnerVersionDefine : "[]")},\n");
            json.Append("    \"noEngineReferences\": false\n}\n");
            File.WriteAllText(Path.Combine(dir, name + ".asmdef"), json.ToString());
            log.AppendLine($"+ asmdef {name}" + (extra.Count > 0 ? $" (+ {string.Join(", ", extra)})" : string.Empty));
        }

        // Existing asmdefs keep everything; only missing package references (and the test-runner define) are added.
        private static void Merge(string path, List<string> extra, bool testRunner, StringBuilder log)
        {
            var text = File.ReadAllText(path);
            var match = Regex.Match(text, "\"references\"\\s*:\\s*\\[([^\\]]*)\\]");
            if (!match.Success) return;
            var current = Regex.Matches(match.Groups[1].Value, "\"([^\"]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            var missing = extra.Where(r => !current.Contains(r)).ToList();
            var updated = missing.Count == 0 ? text
                : text.Substring(0, match.Index) + $"\"references\": {JsonArray(current.Concat(missing))}" + text.Substring(match.Index + match.Length);
            if (testRunner && !updated.Contains(TestRunnerDefine))
                updated = Regex.Replace(updated, "\"versionDefines\"\\s*:\\s*\\[\\s*\\]", "\"versionDefines\": " + TestRunnerVersionDefine);
            if (updated == text) return;
            File.WriteAllText(path, updated);
            log.AppendLine($"* asmdef {Path.GetFileNameWithoutExtension(path)}" + (missing.Count > 0 ? $" + {string.Join(", ", missing)}" : string.Empty));
        }

        private static string TestRunnerVersionDefine =>
            $"[{{ \"name\": \"com.unity.test-framework\", \"expression\": \"\", \"define\": \"{TestRunnerDefine}\" }}]";

        private static string JsonArray(IEnumerable<string> items) =>
            "[" + string.Join(", ", items.Select(i => $"\"{i}\"")) + "]";
    }
}
