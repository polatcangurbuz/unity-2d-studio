using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
#if STUDIO_TEST_RUNNER
using UnityEditor.TestTools.TestRunner.Api;
#endif

namespace Studio.EditorTools
{
    // Without an MCP bridge Claude can't see the Console: compile errors and runtime errors go to .studio/console.log instead.
    [InitializeOnLoad]
    public static class StudioConsoleBridge
    {
        private const int MaxEntries = 200;
        private const string SessionKey = "Studio.ConsoleBridge.Started";
        private const string CompileKey = "Studio.ConsoleBridge.Compile";
        private static readonly string Folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", ".studio");
        private static readonly string LogPath = Path.Combine(Folder, "console.log");
        private static readonly Regex CompilerLine = new Regex(@"\): (error|warning) CS\d+:");
        private static readonly Regex TimePrefix = new Regex(@"^\[\d\d:\d\d:\d\d\] ");
        private static readonly Regex StackFrame = new Regex(@"\(at (Assets/[^)]+:\d+)\)");
        private static readonly List<string> Entries = new List<string>();
        private static int _compileErrors;

        static StudioConsoleBridge()
        {
            // The startup compile runs before this class loads, so a new editor session starts a fresh log.
            var fresh = !SessionState.GetBool(SessionKey, false);
            SessionState.SetBool(SessionKey, true);
            // A domain load means scripts compiled, so earlier compile errors are stale.
            var previous = !fresh && File.Exists(LogPath) ? File.ReadAllLines(LogPath).Skip(1).ToList() : new List<string>();
            Entries.AddRange(previous.Where(l => !Body(l).StartsWith("compile-error")).TakeLast(MaxEntries));
            // This class only loads after a successful compile (Unity keeps the old domain on errors).
            SessionState.SetString(CompileKey, "compile ok");
            if (fresh) Flush("session start");
            else if (Entries.Count != previous.Count) Flush("compile ok");
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            Application.logMessageReceived += OnLog;
            EditorApplication.playModeStateChanged += OnPlayMode;
#if STUDIO_TEST_RUNNER
            ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new StudioTestReporter());
#endif
        }

        private static void OnCompilationStarted(object context)
        {
            Entries.Clear();
            _compileErrors = 0;
            Flush("compiling");
        }

        private static void OnAssemblyCompiled(string assembly, CompilerMessage[] messages)
        {
            foreach (var m in messages.Where(m => m.type == CompilerMessageType.Error))
            {
                _compileErrors++;
                var text = !string.IsNullOrEmpty(m.file) && m.message.StartsWith(m.file) ? m.message : $"{m.file}:{m.line} {m.message}";
                Add($"{(IsPackage(m.file) ? "package-" : string.Empty)}compile-error {FirstLine(text)}");
            }
        }

        private static void OnCompilationFinished(object context)
        {
            SessionState.SetString(CompileKey, _compileErrors == 0 ? "compile ok" : $"compile failed: {_compileErrors} error(s)");
            Flush(SessionState.GetString(CompileKey, string.Empty));
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            // Compiler errors are echoed here too; OnAssemblyCompiled already recorded them.
            if (type == LogType.Log || type == LogType.Warning || CompilerLine.IsMatch(message ?? string.Empty)) return;
            // First frame outside the Studio helpers points at the caller, e.g. the builder line that passed a bad field name.
            var at = StackFrame.Matches(stackTrace ?? string.Empty).Cast<Match>().FirstOrDefault(m => !m.Value.Contains("/Studio"));
            var mode = EditorApplication.isPlaying ? "play" : "edit";
            // Errors raised by Unity packages (not our code) stay visible but are counted apart.
            var package = at == null && IsPackage(message) ? "package-" : string.Empty;
            Add($"{package}{type.ToString().ToLowerInvariant()} [{mode}] {(at != null ? at.Groups[1].Value + " " : string.Empty)}{FirstLine(message)}");
            Flush(null);
        }

        // Each Play replaces the last one's errors; cleared on leaving Edit Mode since Awake errors precede EnteredPlayMode.
        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            Entries.RemoveAll(e => Body(e).Contains(" [play] ") || Body(e).StartsWith("play start"));
            Add("play start");
            Flush("play");
        }

        // Builders call this (StudioEditorUtil.BuildScene does) so the log shows which build the following errors belong to.
        public static void Mark(string what)
        {
            Add($"mark {what}");
            Flush(null);
        }

        private static bool IsPackage(string text) =>
            !string.IsNullOrEmpty(text) && (text.Contains("Packages/") || text.Contains("PackageCache")) && !text.Contains("Assets/");

        private static void Add(string entry)
        {
            // Errors in Update repeat every frame; one line with a count (and the first time) is enough.
            var last = Entries.Count - 1;
            var repeat = Regex.Match(last >= 0 ? Entries[last] : string.Empty, @"^(\[[\d:]+\] )?(.*?)(?: \(x(\d+)\))?$");
            if (last >= 0 && repeat.Groups[2].Value == entry)
                Entries[last] = $"{repeat.Groups[1].Value}{entry} (x{(repeat.Groups[3].Success ? int.Parse(repeat.Groups[3].Value) + 1 : 2)})";
            else Entries.Add($"[{DateTime.Now:HH:mm:ss}] {entry}");
            if (Entries.Count > MaxEntries) Entries.RemoveRange(0, Entries.Count - MaxEntries);
        }

        private static string Body(string line) => TimePrefix.Replace(line, string.Empty);

        private static void Flush(string status)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                // The header always carries the last compile result, so a play/log flush does not hide it.
                var compile = SessionState.GetString(CompileKey, "compile ?");
                var what = status != null && status.StartsWith("compil") ? status : $"{status ?? "log"} · {compile}";
                var header = $"# {DateTime.Now:HH:mm:ss} {what} · {Entries.Count} entr{(Entries.Count == 1 ? "y" : "ies")}";
                File.WriteAllLines(LogPath, new[] { header }.Concat(Entries));
            }
            catch (IOException)
            {
                // File briefly locked by a reader; the next message rewrites it.
            }
        }

        private static string FirstLine(string text) => (text ?? string.Empty).Split('\n')[0].Trim();

#if STUDIO_TEST_RUNNER
        private class StudioTestReporter : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var failed = Leaves(result).Where(r => r.TestStatus == TestStatus.Failed).ToList();
                var lines = new List<string>
                {
                    $"# {DateTime.Now:HH:mm:ss} passed {result.PassCount} · failed {result.FailCount} · skipped {result.SkipCount} · inconclusive {result.InconclusiveCount}",
                };
                lines.AddRange(failed.Select(r => $"FAIL {r.FullName}: {FirstLine(r.Message)}"));
                Directory.CreateDirectory(Folder);
                File.WriteAllLines(Path.Combine(Folder, "test-results.txt"), lines);
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result) { }

            private static IEnumerable<ITestResultAdaptor> Leaves(ITestResultAdaptor node) =>
                node.HasChildren ? node.Children.SelectMany(Leaves) : new[] { node };
        }
#endif
    }
}
