using System;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.EditorTools
{
    // Claude can't press Play or see the Game view: plays a scene for a few seconds, leaves .studio/play-check.txt + shots/*.png.
    [InitializeOnLoad]
    public static class StudioPlayCheck
    {
        private const string StateKey = "Studio.PlayCheck.Run";
        private static PlayCheckRun _run;
        private static ProfilerRecorder _gc;

        public static bool Busy => _run != null;

        static StudioPlayCheck()
        {
            // After the domain reload of entering Play Mode this runs before the scene loads, so Awake errors are counted too.
            var saved = SessionState.GetString(StateKey, string.Empty);
            if (saved.Length > 0) Attach(JsonUtility.FromJson<PlayCheckRun>(saved));
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("Tools/Studio/Play Check")]
        public static void RunFromMenu() => Run(string.Empty);

        // "scene: Assets/…unity | seconds: 4 | shots: 0.5,4 | watch: Player" (all optional); StudioRequests calls this.
        public static void Run(string request) => Begin(PlayCheckRun.Parse(request, false));

        // -batchmode -executeMethod Studio.EditorTools.StudioPlayCheck.RunBatch [-studioCheck "…"], no -quit: exits itself, 1 = errors.
        public static void RunBatch()
        {
            var args = Environment.GetCommandLineArgs();
            var at = Array.IndexOf(args, "-studioCheck");
            Begin(PlayCheckRun.Parse(at >= 0 && at + 1 < args.Length ? args[at + 1] : string.Empty, true));
        }

        private static void Begin(PlayCheckRun run)
        {
            if (_run != null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PlayCheck] already playing; stop Play Mode first");
                return;
            }

            var path = !string.IsNullOrEmpty(run.Scene) ? run.Scene : DefaultScene();
            var asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            run.Begin(path);
            if (asset == null)
            {
                run.Abort($"scene not found: '{path}' (save the scene or pass scene: Assets/…unity)");
                Finish(run);
                return;
            }

            if (EditorUtility.scriptCompilationFailed)
            {
                run.Abort("scripts don't compile; fix .studio/console.log errors first");
                Finish(run);
                return;
            }

            run.PreviousStartScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene);
            // playModeStartScene plays that scene without touching what is open (or unsaved) in the editor.
            EditorSceneManager.playModeStartScene = asset;
            Attach(run);
            Save();
            StudioConsoleBridge.Mark($"play-check {path}");
            EditorApplication.EnterPlaymode();
        }

        private static void Attach(PlayCheckRun run)
        {
            _run = run;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        private static void OnLog(string message, string stackTrace, LogType type) => _run?.OnLog(message, stackTrace, type);

        private static void OnUpdate()
        {
            if (_run == null || !EditorApplication.isPlaying || _run.Done) return;
            // Batch mode and a background editor don't advance the player loop on their own.
            if (Application.isBatchMode || !UnityEditorInternal.InternalEditorUtility.isApplicationActive) EditorApplication.QueuePlayerLoopUpdate();
            if (!_run.Tick(_gc.Valid ? _gc.LastValue : 0)) return;
            Save();
            EditorApplication.ExitPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (_run == null) return;
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    _gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    _run.Abort("Play Mode stopped before the check finished");
                    if (_gc.Valid) _gc.Dispose();
                    Save();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(_run.PreviousStartScene)
                        ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(_run.PreviousStartScene);
                    Finish(_run);
                    break;
            }
        }

        private static void Finish(PlayCheckRun run)
        {
            SessionState.EraseString(StateKey);
            Application.logMessageReceived -= OnLog;
            _run = null;
            var report = File.Exists(PlayCheckRun.ReportPath) ? File.ReadLines(PlayCheckRun.ReportPath).FirstOrDefault() : null;
            StudioConsoleBridge.Mark($"play-check done {report?.TrimStart('#', ' ')}");
            if (run.Batch) EditorApplication.Exit(run.Passed ? 0 : 1);
        }

        private static void Save() => SessionState.SetString(StateKey, JsonUtility.ToJson(_run));

        // Bootstrap (build index 0) is how the game starts; without build scenes, the scene that is open.
        private static string DefaultScene()
        {
            var first = EditorBuildSettings.scenes.FirstOrDefault(s => s.enabled && File.Exists(s.path));
            return first != null ? first.path : EditorSceneManager.GetActiveScene().path;
        }
    }
}
