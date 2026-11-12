using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Studio.EditorTools
{
    // Lets Claude drive an open editor without the user clicking: it writes request files into .studio/, Unity polls them.
    [InitializeOnLoad]
    public static class StudioRequests
    {
        private const double PollSeconds = 1.0;
        private const double ResolveSeconds = 10.0;
        private const string MenuPrefix = "Tools/";
        private const string ManifestKey = "Studio.Requests.Manifest";
        private static readonly string Folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", ".studio");
        private static readonly string MenuRequest = Path.Combine(Folder, "menu.request");
        private static readonly string MenuResult = Path.Combine(Folder, "menu-result.txt");
        private static readonly string PlayRequest = Path.Combine(Folder, "play-check.request");
        private static double _nextPoll;

        static StudioRequests()
        {
            if (!Application.isBatchMode) EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now < _nextPoll || StudioPlayCheck.Busy || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            _nextPoll = now + PollSeconds;
            var menu = File.Exists(MenuRequest);
            if (!menu && !File.Exists(PlayRequest)) return;
            // A manifest.json edit (new package) is only resolved on focus too; Refresh alone leaves the package missing.
            var manifest = File.GetLastWriteTimeUtc(Path.Combine(Folder, "..", "Packages", "manifest.json")).Ticks.ToString();
            if (SessionState.GetString(ManifestKey, string.Empty) != manifest)
            {
                SessionState.SetString(ManifestKey, manifest);
                UnityEditor.PackageManager.Client.Resolve();
                _nextPoll = now + ResolveSeconds;
                return;
            }

            // Unity imports only on focus: pick up Claude's edits first. The request stays until that compile is done.
            AssetDatabase.Refresh();
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorUtility.scriptCompilationFailed)
            {
                Answer(menu ? MenuRequest : PlayRequest, menu ? MenuResult : PlayCheckRun.ReportPath,
                    "# compile failed: fix the errors in .studio/console.log, then write the request again");
                return;
            }

            // Menu first, so one turn can ask for "build the scene, then play it"; the play check waits for any recompile.
            if (menu) RunMenus(Take(MenuRequest));
            else StudioPlayCheck.Run(Take(PlayRequest));
        }

        // One menu path per line; only Tools/… so a request can't quit the editor, build or delete through other menus.
        private static void RunMenus(string request)
        {
            var lines = new List<string> { $"# {DateTime.Now:HH:mm:ss} menu request" };
            foreach (var path in request.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))
            {
                if (!path.StartsWith(MenuPrefix))
                {
                    lines.Add($"refused {path} (only {MenuPrefix}…)");
                    continue;
                }

                StudioConsoleBridge.Mark($"menu {path}");
                bool ok;
                try
                {
                    ok = EditorApplication.ExecuteMenuItem(path);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    ok = false;
                }

                lines.Add($"{(ok ? "ran" : "failed")} {path}{(ok ? string.Empty : " (unknown menu path or it threw: see console.log)")}");
            }

            lines.Add("errors from these menus: .studio/console.log after `mark menu …`");
            File.WriteAllLines(MenuResult, lines);
        }

        private static string Take(string path)
        {
            var text = File.ReadAllText(path);
            File.Delete(path);
            return text;
        }

        private static void Answer(string request, string result, string line)
        {
            File.Delete(request);
            File.WriteAllText(result, line + Environment.NewLine);
        }
    }
}
