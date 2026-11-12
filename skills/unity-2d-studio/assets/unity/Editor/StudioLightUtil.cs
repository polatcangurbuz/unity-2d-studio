#if STUDIO_URP
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Studio.EditorTools
{
    // URP 2D lights for builders. Lit sprites render black without a light, and Light2D affects no sorting layer unless told.
    public static class StudioLightUtil
    {
        // One Global light per scene: reuses an existing one so builders stay idempotent.
        public static Light2D Global(float intensity = 1f, Color? color = null)
        {
            WarnIfNot2DRenderer();
            var existing = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Light2D>(true)).FirstOrDefault(l => l.lightType == Light2D.LightType.Global);
            var light = existing != null ? existing : New("Global Light 2D", Light2D.LightType.Global);
            light.intensity = intensity;
            light.color = color ?? Color.white;
            return light;
        }

        // Soft point light (torch, pickup glow, muzzle flash). Falloff 0.5-0.8 reads well in pixel art.
        public static Light2D Point(Transform parent, float outerRadius, float intensity, Color color, bool shadows = false)
        {
            var light = New("Point Light 2D", Light2D.LightType.Point);
            light.transform.SetParent(parent, false);
            light.pointLightOuterRadius = outerRadius;
            light.pointLightInnerRadius = outerRadius * 0.25f;
            light.falloffIntensity = 0.6f;
            light.intensity = intensity;
            light.color = color;
            light.shadowsEnabled = shadows;
            light.shadowIntensity = shadows ? 0.7f : 0f;
            return light;
        }

        // Shape from the sprite outline; selfShadows off so characters don't darken themselves.
        public static ShadowCaster2D ShadowCaster(GameObject go, bool selfShadows = false)
        {
            if (!go.TryGetComponent<ShadowCaster2D>(out var caster)) caster = go.AddComponent<ShadowCaster2D>();
            caster.selfShadows = selfShadows;
            caster.castsShadows = true;
            return caster;
        }

        private static Light2D New(string name, Light2D.LightType type)
        {
            var light = new GameObject(name).AddComponent<Light2D>();
            light.lightType = type;
            var so = new SerializedObject(light);
            var layers = so.FindProperty("m_ApplyToSortingLayers");
            var ids = SortingLayer.layers.Select(l => l.id).ToArray();
            layers.arraySize = ids.Length;
            for (var i = 0; i < ids.Length; i++) layers.GetArrayElementAtIndex(i).intValue = ids[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return light;
        }

        private static void WarnIfNot2DRenderer()
        {
            // Renderer2D is internal in URP 17, so compare by name.
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && urp.scriptableRenderer?.GetType().Name != "Renderer2D")
                Debug.LogWarning("[StudioLightUtil] active URP renderer is not the 2D Renderer: Light2D has no effect (Project Settings > Graphics)");
        }
    }
}
#endif
