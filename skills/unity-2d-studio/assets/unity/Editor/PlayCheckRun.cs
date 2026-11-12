using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Studio.EditorTools
{
    // State of one play check. Serialized into SessionState because entering and leaving Play Mode reloads the domain.
    [Serializable]
    public class PlayCheckRun
    {
        private const int ShotLongSide = 960;
        private const int MaxErrors = 5;
        private const int WarmupFrames = 3;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly Regex StackFrame = new Regex(@"\(at (Assets/[^)]+:\d+)\)");

        [SerializeField] private string _scene = string.Empty;
        [SerializeField] private float _seconds = 4f;
        [SerializeField] private List<float> _shotTimes = new List<float>();
        [SerializeField] private List<string> _watch = new List<string>();
        [SerializeField] private bool _batch;
        [SerializeField] private string _previousStartScene = string.Empty;
        [SerializeField] private double _startedAt = -1;
        [SerializeField] private int _startFrame;
        [SerializeField] private int _frames;
        [SerializeField] private int _nextShot;
        [SerializeField] private double _stopAt = -1;
        [SerializeField] private float _worstMs;
        [SerializeField] private double _gcBytes;
        [SerializeField] private int _gcSamples;
        [SerializeField] private int _errors;
        [SerializeField] private int _warnings;
        [SerializeField] private int _unityErrors;
        [SerializeField] private List<string> _firstErrors = new List<string>();
        [SerializeField] private List<string> _watchStart = new List<string>();
        [SerializeField] private List<string> _watchNotes = new List<string>();
        [SerializeField] private List<string> _shots = new List<string>();
        [SerializeField] private bool _done;
        [SerializeField] private bool _aborted;

        public string Scene => _scene;
        public bool Batch => _batch;
        public int Errors => _errors;
        public bool Done => _done;
        public bool Passed => _done && !_aborted && _errors == 0;
        public string PreviousStartScene { get => _previousStartScene; set => _previousStartScene = value ?? string.Empty; }

        private static string Studio => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", ".studio");
        public static string ReportPath => Path.Combine(Studio, "play-check.txt");
        private static string ShotFolder => Path.Combine(Studio, "shots");

        // "scene: Assets/…/Level_01.unity | seconds: 4 | shots: 0.5,4 | watch: Player, Boss" (any key optional, '|' or newlines).
        public static PlayCheckRun Parse(string request, bool batch)
        {
            var run = new PlayCheckRun { _batch = batch };
            foreach (var part in (request ?? string.Empty).Split('|', '\n'))
            {
                var kv = part.Split(new[] { ':' }, 2);
                if (kv.Length < 2) continue;
                var value = kv[1].Trim();
                switch (kv[0].Trim().ToLowerInvariant())
                {
                    case "scene": run._scene = value; break;
                    case "seconds": float.TryParse(value, NumberStyles.Float, Inv, out run._seconds); break;
                    case "shots": run._shotTimes = Floats(value); break;
                    case "watch": run._watch = value.Split(',').Select(w => w.Trim()).Where(w => w.Length > 0).ToList(); break;
                }
            }

            run._seconds = Mathf.Clamp(run._seconds <= 0 ? 4f : run._seconds, 0.5f, 60f);
            if (run._shotTimes.Count == 0) run._shotTimes = new List<float> { Mathf.Min(0.5f, run._seconds), run._seconds };
            run._shotTimes = run._shotTimes.Select(t => Mathf.Clamp(t, 0f, run._seconds)).Distinct().OrderBy(t => t).Take(4).ToList();
            return run;
        }

        public void Begin(string scenePath)
        {
            _scene = scenePath;
            Directory.CreateDirectory(ShotFolder);
            // Old shots would be mistaken for this run's.
            foreach (var old in Directory.GetFiles(ShotFolder, "*.png")) File.Delete(old);
        }

        public void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Warning) _warnings++;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            var at = StackFrame.Matches(stackTrace ?? string.Empty).Cast<Match>().FirstOrDefault(m => !m.Value.Contains("/Studio"));
            // No project frame in the stack: Unity or a package (e.g. URP's upgrade dialog in batch mode), not the game's code.
            if (at == null && !(message ?? string.Empty).Contains("Assets/"))
            {
                _unityErrors++;
                return;
            }

            _errors++;
            if (_firstErrors.Count >= MaxErrors) return;
            var line = $"{type.ToString().ToLowerInvariant()} {(at != null ? at.Groups[1].Value + " " : string.Empty)}{(message ?? string.Empty).Split('\n')[0].Trim()}";
            if (!_firstErrors.Contains(line)) _firstErrors.Add(line);
        }

        // Called every editor update while playing; returns true once the check is complete and Play Mode should stop.
        public bool Tick(long gcLastFrame)
        {
            var now = EditorApplication.timeSinceStartup;
            if (_startedAt < 0)
            {
                // Editor play mode pauses in the background unless this is set; it is not saved to Player Settings.
                Application.runInBackground = true;
                _startedAt = now;
                _startFrame = Time.frameCount;
                _watchStart = _watch.Count > 0 || Watched().Any() ? Watched().Select(Describe).ToList() : new List<string>();
                return false;
            }

            _frames = Time.frameCount - _startFrame;
            if (_frames > WarmupFrames)
            {
                _worstMs = Mathf.Max(_worstMs, Time.unscaledDeltaTime * 1000f);
                _gcBytes += gcLastFrame;
                _gcSamples++;
            }

            var elapsed = (float)(now - _startedAt);
            while (_nextShot < _shotTimes.Count && elapsed >= _shotTimes[_nextShot]) Shoot(_shotTimes[_nextShot++]);
            if (_stopAt < 0 && elapsed >= _seconds && _nextShot >= _shotTimes.Count) _stopAt = now;
            if (_stopAt < 0) return false;
            WriteReport(null);
            return true;
        }

        public void Abort(string why)
        {
            if (_done) return;
            _aborted = true;
            WriteReport(why);
        }

        private void Shoot(float t)
        {
            var name = $"{Path.GetFileNameWithoutExtension(_scene)}-{t.ToString("0.0", Inv)}s.png";
            var path = Path.Combine(ShotFolder, name);
            var note = PlayCheckCamera.Capture(path, ShotLongSide);
            _shots.Add(note == null ? $".studio/shots/{name}" : $"(no shot at {t.ToString("0.0", Inv)} s: {note})");
            foreach (var go in Watched())
            {
                var state = Describe(go);
                if (state.Contains("out of view") && !_watchNotes.Any(n => n.StartsWith(go.name + " "))) _watchNotes.Add($"{go.name} out of view at {t.ToString("0.0", Inv)} s");
            }
        }

        // Objects named in the request, else everything tagged Player; found by walking loaded scenes (no Find in studio code).
        private IEnumerable<GameObject> Watched()
        {
            var all = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isLoaded)
                .SelectMany(s => s.GetRootGameObjects()).SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject);
            return _watch.Count > 0 ? all.Where(g => _watch.Contains(g.name)).GroupBy(g => g.name).Select(g => g.First())
                : all.Where(g => g.CompareTag("Player")).Take(3);
        }

        private static string Describe(GameObject go)
        {
            var p = go.transform.position;
            var cam = Camera.main;
            var view = cam != null ? cam.WorldToViewportPoint(p) : Vector3.zero;
            var inView = cam == null || (view.z > 0 && view.x >= 0 && view.x <= 1 && view.y >= 0 && view.y <= 1);
            return $"{go.name} ({p.x.ToString("0.0", Inv)},{p.y.ToString("0.0", Inv)}){(go.activeInHierarchy ? string.Empty : " inactive")}{(inView ? string.Empty : " out of view")}";
        }

        private void WriteReport(string aborted)
        {
            _done = true;
            var missing = _watch.Where(w => !_watchStart.Any(s => s.StartsWith(w + " "))).ToList();
            var end = Watched().Select(Describe).ToList();
            var fps = _startedAt < 0 ? 0 : _frames / Math.Max(0.001, EditorApplication.timeSinceStartup - _startedAt);
            var verdict = aborted != null ? $"aborted: {aborted}"
                : _errors > 0 ? $"errors {_errors}"
                : _watchNotes.Count > 0 || missing.Count > 0 ? "check watch" : "clean";
            var lines = new List<string>
            {
                $"# {DateTime.Now:HH:mm:ss} play check · {_scene} · {_seconds.ToString("0.#", Inv)} s · {verdict}",
                $"frames {_frames} · fps ~{fps.ToString("0", Inv)} (editor) · worst frame {_worstMs.ToString("0", Inv)} ms · gc ~{(_gcSamples > 0 ? _gcBytes / _gcSamples : 0).ToString("0", Inv)} B/frame (editor, indicative)",
                $"errors {_errors} · warnings {_warnings}{(_unityErrors > 0 ? $" · {_unityErrors} from Unity/packages (not project code)" : string.Empty)}",
            };
            if (_watchStart.Count > 0 || end.Count > 0) lines.Add($"watch start: {string.Join(" · ", _watchStart)} | end: {string.Join(" · ", end)}");
            lines.AddRange(_watchNotes.Select(n => "watch: " + n));
            lines.AddRange(missing.Select(m => $"watch: {m} not found in loaded scenes"));
            lines.Add("shots: " + (_shots.Count > 0 ? string.Join(" · ", _shots) : "none"));
            lines.AddRange(_firstErrors.Select(e => "- " + e));
            Directory.CreateDirectory(Studio);
            File.WriteAllLines(ReportPath, lines);
        }

        private static List<float> Floats(string csv) =>
            csv.Split(',').Select(s => float.TryParse(s.Trim(), NumberStyles.Float, Inv, out var f) ? f : -1f).Where(f => f >= 0).ToList();
    }
}
