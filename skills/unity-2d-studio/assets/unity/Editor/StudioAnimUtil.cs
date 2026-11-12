using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Studio.EditorTools
{
    // Flipbook clips and Animator Controllers from sliced sheets, so builders never hand-write .anim/.controller YAML.
    public static class StudioAnimUtil
    {
        private static readonly Regex TrailingIndex = new Regex(@"_(\d+)$");

        // Sprites of one sliced sheet in frame order (Name_0, Name_1, … sorted numerically, not as text).
        public static Sprite[] SheetSprites(string texturePath)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>()
                .OrderBy(s => TrailingIndex.Match(s.name) is var m && m.Success ? int.Parse(m.Groups[1].Value) : 0).ToArray();
            if (sprites.Length == 0) Debug.LogError($"[StudioAnimUtil] no sprites in {texturePath} (not imported or not sliced?)");
            return sprites;
        }

        // first/count pick a row out of a sheet with several animations (e.g. idle 0-3, run 4-9).
        public static AnimationClip Flipbook(string clipPath, Sprite[] sprites, float fps, bool loop, int first = 0, int count = -1)
        {
            var frames = sprites.Skip(first).Take(count < 0 ? sprites.Length - first : count).ToArray();
            if (frames.Length == 0) Debug.LogError($"[StudioAnimUtil] {clipPath}: no frames in range {first}+{count}");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                StudioEditorUtil.EnsureFolder(Path.GetDirectoryName(clipPath)?.Replace('\\', '/'));
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.frameRate = fps;
            // Animator targets the SpriteRenderer on the same object: put the Animator on the "Visual" child.
            var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
            var keys = frames.Select((s, i) => new ObjectReferenceKeyframe { time = i / fps, value = s }).ToList();
            // The last frame needs its full duration before the loop wraps.
            if (frames.Length > 0) keys.Add(new ObjectReferenceKeyframe { time = frames.Length / fps, value = frames[frames.Length - 1] });
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        // Rebuilt from scratch each call (idempotent builders); the first state is the default.
        public static AnimatorController Controller(string path, params (string state, AnimationClip clip)[] states)
        {
            StudioEditorUtil.EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var (name, clip) in states)
            {
                var s = machine.AddState(name);
                s.motion = clip;
            }

            if (machine.states.Length > 0) machine.defaultState = machine.states[0].state;
            return controller;
        }

        public static void Param(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (controller.parameters.All(p => p.name != name)) controller.AddParameter(name, type);
        }

        // Instant transition (pixel art has no blending). from "Any" = Any State. Conditions: ("Speed", Greater, 0.1f), ("Grounded", If, 0).
        public static void Transition(AnimatorController controller, string from, string to,
            params (string param, AnimatorConditionMode mode, float threshold)[] conditions)
        {
            var machine = controller.layers[0].stateMachine;
            var target = State(machine, to);
            if (target == null) return;
            AnimatorStateTransition t;
            if (from == "Any")
            {
                t = machine.AddAnyStateTransition(target);
                t.canTransitionToSelf = false;
            }
            else
            {
                var source = State(machine, from);
                if (source == null) return;
                t = source.AddTransition(target);
            }

            t.duration = 0f;
            t.hasExitTime = conditions.Length == 0;
            t.exitTime = 1f;
            foreach (var (param, mode, threshold) in conditions)
            {
                if (controller.parameters.All(p => p.name != param)) Debug.LogError($"[StudioAnimUtil] {controller.name}: parameter '{param}' missing; call Param first");
                t.AddCondition(mode, threshold, param);
            }
        }

        private static AnimatorState State(AnimatorStateMachine machine, string name)
        {
            var found = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
            if (found == null) Debug.LogError($"[StudioAnimUtil] state '{name}' not in controller");
            return found;
        }
    }
}
