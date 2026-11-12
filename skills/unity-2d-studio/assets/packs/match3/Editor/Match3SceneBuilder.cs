using System.Linq;
using __NS__.Puzzle.View;
using Studio.EditorTools;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace __NS__.Puzzle.EditorTools
{
    // One click (or .studio/menu.request) → a playable portrait match-3 scene with placeholder art, juice and HUD.
    public static class Match3SceneBuilder
    {
        private const string Scene = "Assets/_Project/Scenes/Puzzle.unity";
        private const string Art = "Assets/_Project/Art/Puzzle/Placeholder";
        private const string Data = "Assets/_Project/Data/Puzzle";
        private const string FirstLevel = "Assets/_Project/Data/Levels/001_easy.txt";

        [MenuItem("Tools/Studio/Match3/Build Board Scene")]
        public static void Build()
        {
            StudioEditorUtil.BuildScene(Scene, scene =>
            {
                var level = AssetDatabase.LoadAssetAtPath<TextAsset>(FirstLevel);
                if (level == null) Debug.LogError($"[Match3SceneBuilder] {FirstLevel} missing: run install_unity.py --pack match3");
                var skin = Skin();
                var feel = StudioEditorUtil.LoadOrCreate<BoardFeelSO>($"{Data}/BoardFeel.asset");
                var parsed = level != null ? LevelParser.Parse(level.text) : null;
                var width = parsed?.Width ?? 9;
                var height = parsed?.Height ?? 9;

                StudioEditorUtil.Separator("--- CAMERAS ---");
                var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
                cam.orthographic = true;
                // Portrait 9:16: fit the board width with a margin, leave room above for the HUD.
                cam.orthographicSize = (width + 0.8f) * 16f / 9f / 2f;
                cam.transform.position = new Vector3(0f, 0.6f, -10f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.16f, 0.12f, 0.26f);

                StudioEditorUtil.Separator("--- GAMEPLAY ---");
                var mask = new GameObject("BoardMask").AddComponent<SpriteMask>();
                mask.sprite = Match3PlaceholderArt.Square(Art);
                mask.transform.localScale = new Vector3(width, height, 1f);
                var board = new GameObject("Board").AddComponent<BoardView>();
                var input = new GameObject("Input").AddComponent<SwipeInput>();
                StudioEditorUtil.Wire(input, ("_camera", cam));
                StudioEditorUtil.Wire(board, ("_level", level), ("_feel", feel), ("_skin", skin), ("_input", input), ("_cellSize", 1f));

                var juice = new GameObject("Juice").AddComponent<BoardJuice>();
                var audio = juice.gameObject.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                StudioEditorUtil.Wire(juice, ("_board", board), ("_feel", feel), ("_burst", Burst()), ("_shakeTarget", cam.transform),
                    ("_audio", audio));
                var colors = new SerializedObject(juice);
                var list = colors.FindProperty("_colors");
                list.arraySize = Match3PlaceholderArt.Colors.Length;
                for (var i = 0; i < list.arraySize; i++) list.GetArrayElementAtIndex(i).colorValue = Match3PlaceholderArt.Colors[i];
                colors.ApplyModifiedPropertiesWithoutUndo();

                StudioEditorUtil.Separator("--- UI ---");
                Hud(board);
            });
            StudioEditorUtil.AddToBuildSettings(Scene);
        }

        private static BoardSkinSO Skin()
        {
            var skin = StudioEditorUtil.LoadOrCreate<BoardSkinSO>($"{Data}/BoardSkin.asset");
            var pieces = Enumerable.Range(0, Match3PlaceholderArt.Colors.Length).Select(c => (Object)Match3PlaceholderArt.Piece(Art, c)).ToArray();
            StudioEditorUtil.Wire(skin, ("_pieces", pieces),
                ("_rocketH", Match3PlaceholderArt.Rocket(Art, true)), ("_rocketV", Match3PlaceholderArt.Rocket(Art, false)),
                ("_bomb", Match3PlaceholderArt.Bomb(Art)), ("_colorBomb", Match3PlaceholderArt.ColorBomb(Art)),
                ("_cell", Match3PlaceholderArt.Cell(Art)),
                ("_crate", Enumerable.Range(1, 3).Select(hp => (Object)Match3PlaceholderArt.Crate(Art, hp)).ToArray()),
                ("_ice", Enumerable.Range(1, 2).Select(hp => (Object)Match3PlaceholderArt.Ice(Art, hp)).ToArray()),
                ("_chain", Enumerable.Range(1, 2).Select(hp => (Object)Match3PlaceholderArt.Chain(Art, hp)).ToArray()));
            var so = new SerializedObject(skin);
            var tints = so.FindProperty("_specialTints");
            tints.arraySize = Match3PlaceholderArt.Colors.Length;
            for (var i = 0; i < tints.arraySize; i++) tints.GetArrayElementAtIndex(i).colorValue = Color.Lerp(Match3PlaceholderArt.Colors[i], Color.white, 0.2f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skin);
            return skin;
        }

        // One shared burst system; BoardJuice emits into it per cleared piece with that piece's colour.
        private static ParticleSystem Burst()
        {
            var ps = new GameObject("Burst").AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
            main.gravityModifier = 1.2f;
            main.maxParticles = 600;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.25f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { mainTexture = Match3PlaceholderArt.Spark(Art).texture };
            AssetDatabase.CreateAsset(material, $"{Data}/MAT_Burst.mat");
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 30;
            return ps;
        }

        private static void Hud(BoardView board)
        {
            var canvas = StudioUiUtil.Canvas("HUD", new Vector2(1080, 1920), 0f);
            var safe = StudioUiUtil.SafeArea(canvas);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            var top = StudioUiUtil.Place(StudioUiUtil.Panel(safe, "TopBar", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.35f)),
                new Vector2(0.5f, 1f), new Vector2(1000, 220), new Vector2(0, -30));
            var movesBox = StudioUiUtil.Place(StudioUiUtil.Panel(top, "Moves", Vector2.zero, Vector2.one), new Vector2(0f, 0.5f), new Vector2(260, 200), new Vector2(20, 0));
            StudioUiUtil.Place((RectTransform)StudioUiUtil.Label(movesBox, "Caption", "MOVES", 40).transform, new Vector2(0.5f, 1f), new Vector2(260, 60), Vector2.zero);
            var moves = (TMP_Text)StudioUiUtil.Label(movesBox, "Value", "0", 110);
            var goals = (TMP_Text)StudioUiUtil.Label(top, "Goals", string.Empty, 46, TextAnchor.MiddleRight);
            StudioUiUtil.Place(goals.rectTransform, new Vector2(1f, 0.5f), new Vector2(680, 200), new Vector2(-30, 0));
            var result = (TMP_Text)StudioUiUtil.Label(safe, "Result", string.Empty, 96);
            var hud = canvas.gameObject.AddComponent<PuzzleHud>();
            StudioEditorUtil.Wire(hud, ("_board", board), ("_moves", moves), ("_goals", goals), ("_result", result));
        }
    }
}
