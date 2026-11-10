using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Studio.EditorTools
{
    // Builds particle prefabs from Art/VFX/Specs/*.vfx.json (written by scripts/vfx_spec.py) so effects stay data, not hand edits.
    public static class VfxBuilder
    {
        private const string SpecFolder = "Assets/_Project/Art/VFX/Specs";
        private const string MaterialFolder = "Assets/_Project/Art/VFX/Materials";
        private const string PrefabFolder = "Assets/_Project/Prefabs/VFX";

        // Plain JSON DTOs; vfx_spec.py writes every field, so no defaults are needed here.
        [Serializable] private struct Spec { public string name, sortingLayer; public float duration; public bool loop; public int order; public Layer[] layers; }

        [Serializable]
        private struct Layer
        {
            public string name, shape, sizeOverLife, colorStart, colorEnd, blend, texture;
            public float radius, angle, rate, lifetimeMin, lifetimeMax, speedMin, speedMax, sizeMin, sizeMax, gravity, noise, delay;
            public int burst;
            public bool fade;
        }

        [MenuItem("Tools/Studio/Build VFX From Specs")]
        public static void BuildAll()
        {
            if (!Directory.Exists(SpecFolder))
            {
                Debug.LogWarning($"[VfxBuilder] no {SpecFolder}; ask Claude to run vfx_spec.py build");
                return;
            }
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(PrefabFolder);
            var built = 0;
            foreach (var file in Directory.GetFiles(SpecFolder, "*.vfx.json"))
            {
                var spec = JsonUtility.FromJson<Spec>(File.ReadAllText(file));
                if (spec.layers == null || spec.layers.Length == 0)
                {
                    Debug.LogError($"[VfxBuilder] {file}: no layers");
                    continue;
                }
                Build(spec);
                built++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[VfxBuilder] {built} prefab(s) in {PrefabFolder}");
        }

        private static void Build(Spec spec)
        {
            var root = new GameObject(spec.name);
            // Root emits nothing; its duration covers every layer, so one stop callback means the whole effect is done.
            var rootSystem = root.AddComponent<ParticleSystem>();
            var rootMain = rootSystem.main;
            rootMain.duration = Mathf.Max(spec.duration, 0.05f);
            rootMain.loop = spec.loop;
            rootMain.stopAction = ParticleSystemStopAction.Callback;
            var rootEmission = rootSystem.emission;
            rootEmission.enabled = false;
            root.GetComponent<ParticleSystemRenderer>().enabled = false;

            foreach (var layer in spec.layers)
            {
                var child = new GameObject(layer.name);
                child.transform.SetParent(root.transform, false);
                Configure(child.AddComponent<ParticleSystem>(), layer, spec);
            }

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{spec.name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void Configure(ParticleSystem system, Layer layer, Spec spec)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.duration = Mathf.Max(spec.duration, 0.05f);
            main.loop = spec.loop;
            main.startDelay = layer.delay;
            main.startLifetime = new ParticleSystem.MinMaxCurve(layer.lifetimeMin, layer.lifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(layer.speedMin, layer.speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(layer.sizeMin, layer.sizeMax);
            main.startColor = Hex(layer.colorStart);
            main.gravityModifier = layer.gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(layer.burst + Mathf.CeilToInt(layer.rate * layer.lifetimeMax) + 1, 1);

            var emission = system.emission;
            emission.rateOverTime = layer.rate;
            emission.SetBursts(layer.burst > 0 ? new[] { new ParticleSystem.Burst(0f, (short)layer.burst) } : new ParticleSystem.Burst[0]);

            var shape = system.shape;
            shape.enabled = layer.shape != "point";
            shape.shapeType = layer.shape == "cone" ? ParticleSystemShapeType.Cone
                : layer.shape == "edge" ? ParticleSystemShapeType.SingleSidedEdge : ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(layer.radius, 0.001f);
            shape.angle = layer.angle;
            if (layer.shape == "cone") shape.rotation = new Vector3(-90f, 0f, 0f);

            var end = string.IsNullOrEmpty(layer.colorEnd) ? Hex(layer.colorStart) : Hex(layer.colorEnd);
            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Hex(layer.colorStart), 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(layer.fade ? 0f : 1f, 1f) });
            color.color = gradient;

            if (layer.sizeOverLife != "none")
            {
                var size = system.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, layer.sizeOverLife == "grow"
                    ? AnimationCurve.Linear(0f, 0.3f, 1f, 1f) : AnimationCurve.Linear(0f, 1f, 1f, 0f));
            }

            if (layer.noise > 0f)
            {
                var noise = system.noise;
                noise.enabled = true;
                noise.strength = layer.noise;
            }

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MaterialFor(layer.blend, layer.texture);
            renderer.sortingLayerName = string.IsNullOrEmpty(spec.sortingLayer) ? "Default" : spec.sortingLayer;
            renderer.sortingOrder = spec.order;
        }

        private static Material MaterialFor(string blend, string texture)
        {
            var path = $"{MaterialFolder}/VFX_{blend}_{texture}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var material = UrpParticleMaterial(blend == "additive") ?? LegacyParticleMaterial(blend == "additive");
            material.mainTexture = texture == "soft" ? AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd") : PixelTexture();
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // Legacy particle shaders render nothing under URP; its Particles/Unlit needs the transparent setup done by hand.
        private static Material UrpParticleMaterial(bool additive)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return null;
            var m = new Material(shader);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        private static Material LegacyParticleMaterial(bool additive)
        {
            var shader = Shader.Find(additive ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            return new Material(shader);
        }

        // Hard-edged square particles match pixel art; the built-in soft particle would blur the style.
        private static Texture2D PixelTexture()
        {
            var path = $"{MaterialFolder}/VFX_pixel.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null) return texture;
            texture = new Texture2D(2, 2) { filterMode = FilterMode.Point, name = "VFX_pixel" };
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        private static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.magenta;
    }
}
