#if STUDIO_CINEMACHINE
using Unity.Cinemachine;
using UnityEngine;

namespace Studio.EditorTools
{
    // Cinemachine 3 follow camera for builders. Only compiles when the project already has Cinemachine 3 (never added by the studio).
    public static class StudioCameraUtil
    {
        // Dead zone + damping stop micro-jitter on small moves; lookahead shows where the player is going; bounds keep the view in the level.
        public static CinemachineCamera Follow2D(Camera main, Transform target, float orthographicSize, Collider2D bounds = null,
            float lookahead = 0.2f, bool pixelPerfect = false)
        {
            main.orthographic = true;
            if (!main.TryGetComponent<CinemachineBrain>(out _)) main.gameObject.AddComponent<CinemachineBrain>();
            var go = new GameObject("CM Follow Camera");
            var cam = go.AddComponent<CinemachineCamera>();
            cam.Follow = target;
            var lens = cam.Lens;
            lens.OrthographicSize = orthographicSize;
            cam.Lens = lens;

            var composer = go.AddComponent<CinemachinePositionComposer>();
            composer.CameraDistance = 10f;
            composer.Damping = new Vector3(0.3f, 0.3f, 0f);
            var composition = composer.Composition;
            composition.DeadZone.Enabled = true;
            composition.DeadZone.Size = new Vector2(0.15f, 0.1f);
            composer.Composition = composition;
            composer.Lookahead = new LookaheadSettings { Enabled = lookahead > 0f, Time = lookahead, Smoothing = 5f, IgnoreY = true };

            if (bounds != null)
            {
                var confiner = go.AddComponent<CinemachineConfiner2D>();
                confiner.BoundingShape2D = bounds;
                confiner.InvalidateBoundingShapeCache();
            }

            // Screen shake: CinemachineImpulseSource on the cause (explosion, hit) reaches this listener.
            go.AddComponent<CinemachineImpulseListener>();
            if (pixelPerfect) go.AddComponent<CinemachinePixelPerfect>();
            return cam;
        }
    }
}
#endif
