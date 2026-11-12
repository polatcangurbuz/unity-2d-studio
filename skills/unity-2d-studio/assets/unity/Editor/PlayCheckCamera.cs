using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Studio.EditorTools
{
    // Renders Camera.main into a small PNG. A camera render works in the background and in batch mode, unlike the Game view.
    public static class PlayCheckCamera
    {
        private const int OverlayOrder = 32000;

        // Returns null on success, else why no shot was taken.
        public static string Capture(string path, int longSide)
        {
            var cam = Camera.main;
            if (cam == null) return "no camera tagged MainCamera";
            var (w, h) = Size(longSide);
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var previousTarget = cam.targetTexture;
            var previousActive = RenderTexture.active;
            // Screen Space Overlay canvases never reach a camera texture; point them at the camera for this one render.
            var overlays = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isLoaded)
                .SelectMany(s => s.GetRootGameObjects()).SelectMany(r => r.GetComponentsInChildren<Canvas>())
                .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var orders = overlays.Select(c => c.sortingOrder).ToArray();
            try
            {
                foreach (var c in overlays)
                {
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = Mathf.Max(cam.nearClipPlane * 2f, 0.5f);
                    c.sortingOrder = OverlayOrder;
                }

                Canvas.ForceUpdateCanvases();
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return null;
            }
            finally
            {
                cam.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(rt);
                for (var i = 0; i < overlays.Length; i++)
                {
                    overlays[i].renderMode = RenderMode.ScreenSpaceOverlay;
                    overlays[i].sortingOrder = orders[i];
                }
            }
        }

        // Portrait-locked games always shoot 9:16 (the Game view is often left landscape); else the Game view's size.
        private static (int w, int h) Size(int longSide)
        {
            float sw = Screen.width, sh = Screen.height;
            var portrait = PlayerSettings.defaultInterfaceOrientation == UIOrientation.Portrait
                           || PlayerSettings.defaultInterfaceOrientation == UIOrientation.PortraitUpsideDown;
            if (portrait) (sw, sh) = (9f, 16f);
            else if (Application.isBatchMode || sw < 16 || sh < 16) (sw, sh) = (16f, 9f);

            var scale = longSide / Mathf.Max(sw, sh);
            return (Mathf.Max(16, Mathf.RoundToInt(sw * scale)), Mathf.Max(16, Mathf.RoundToInt(sh * scale)));
        }
    }
}
