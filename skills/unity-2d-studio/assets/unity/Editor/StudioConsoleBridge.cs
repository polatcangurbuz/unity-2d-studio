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
        private static readonly string Folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", ".studio");
        private static readonly string LogPath = Path.Combine(Folder, "console.log");
        private static readonly Regex CompilerLine = new Regex(@"\): (error|warning) CS\d+:");
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
            Entries.AddRange(previous.Where(l => !l.StartsWith("compile-error")).TakeLast(MaxEntries));
            if (fresh) Flush("session start");
            else if (Entries.Count != previous.Count) Flush("compile ok");
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            Application.logMessageReceived += OnLog;
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
                Add($"compile-error {FirstLine(text)}");
            }
        }

        private static void OnCompilationFinished(object context) =>
            Flush(_compileErrors == 0 ? "compile ok" : $"compile failed: {_compileErrors} error(s)");

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            // Compiler errors are echoed here too; OnAssemblyCompiled already recorded them.
            if (type == LogType.Log || type == LogType.Warning || CompilerLine.IsMatch(message ?? string.Empty)) return;
            // First frame outside the Studio helpers points at the caller, e.g. the builder line that passed a bad field name.
            var at = StackFrame.Matches(stackTrace ?? string.Empty).Cast<Match>().FirstOrDefault(m => !m.Value.Contains("/Studio"));
            var mode = EditorApplication.isPlaying ? "play" : "edit";
            Add($"{type.ToString().ToLowerInvariant()} [{mode}] {(at != null ? at.Groups[1].Value + " " : string.Empty)}{FirstLine(message)}");
            Flush(null);
        }

        private static void Add(string entry)
        {
            // Errors in Update repeat every frame; one line with a count is enough.
            var last = Entries.Count - 1;
            var repeat = Regex.Match(last >= 0 ? Entries[last] : string.Empty, @"^(.*) \(x(\d+)\)$");
            if (last >= 0 && (Entries[last] == entry || repeat.Success && repeat.Groups[1].Value == entry))
                Entries[last] = $"{entry} (x{(repeat.Success ? int.Parse(repeat.Groups[2].Value) + 1 : 2)})";
            else Entries.Add(entry);
            if (Entries.Count > MaxEntries) Entries.RemoveRange(0, Entries.Count - MaxEntries);
        }

        private static void Flush(string status)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var header = $"# {DateTime.Now:HH:mm:ss} {status ?? "log"} · {Entries.Count} entr{(Entries.Count == 1 ? "y" : "ies")}";
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
