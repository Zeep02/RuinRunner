using System;
using UnityEngine;
using UnityEngine.U2D;

namespace RuinRunner
{
    public sealed class CameraFollow : MonoBehaviour
    {
        public const int ReferenceWidth = 640;
        public const int ReferenceHeight = 360;
        public const int WorldPixelsPerUnit = 32;
        public const float ViewHalfHeight = ReferenceHeight / (2f * WorldPixelsPerUnit);
        public Transform Target { get; set; }

        private Vector3 shakeOffset;
        private float shakeTime;
        private Vector2 roomCenter;
        private Vector2 roomSize = new Vector2(22.6f, 11.8f);
        private Camera viewCamera;
        private bool presentationFocus;
        private Vector2 presentationPoint;
        public bool HasPresentationFocus => presentationFocus;
        public void FocusPresentation(Vector2 point)
        { presentationPoint=point;presentationFocus=true;shakeTime=0;shakeOffset=Vector3.zero; }
        public void ReleasePresentation() { presentationFocus=false; }
        private float nextImpactAt;
        private float nextFinisherImpactAt;
        private Action<bool, bool> hitImpactCallback;
        private Action bossDefeatCallback;
        private Action finisherImpactCallback;

        public static CameraFollow Create()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = ViewHalfHeight;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.allowDynamicResolution = false;
            QualitySettings.antiAliasing = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            var pixels = cameraObject.AddComponent<PixelPerfectCamera>();
            pixels.assetsPPU = WorldPixelsPerUnit;
            pixels.refResolutionX = ReferenceWidth;
            pixels.refResolutionY = ReferenceHeight;
            // Keep the authored framing and sharp source pixels, but rasterize
            // motion and rotated weapons at the window resolution. A 640x360
            // intermediate buffer quantized every contour/translation to 3px
            // jumps at 1080p and discarded the finer detail in supplied actors.
            pixels.upscaleRT = false;
            pixels.pixelSnapping = false;
            pixels.cropFrameX = true;
            pixels.cropFrameY = true;
            pixels.stretchFill = false;
            camera.backgroundColor = new Color(.085f, .073f, .135f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var follow = cameraObject.AddComponent<CameraFollow>();
            follow.viewCamera = camera;
            return follow;
        }

        public void ConfigureCombatFeedback(Action<bool, bool> hitCallback, Action bossCallback, Action finisherCallback = null)
        {
            hitImpactCallback = hitCallback;
            bossDefeatCallback = bossCallback;
            finisherImpactCallback = finisherCallback;
        }

        public void MeleeFinisherImpact()
        {
            // A cleave can contact several colliders. Emphasize that one landed
            // action without multiplying time effects for each target or pellet.
            if (Time.unscaledTime < nextFinisherImpactAt) return;
            nextFinisherImpactAt = Time.unscaledTime + .16f;
            Shake(.055f);
            finisherImpactCallback?.Invoke();
        }

        public void Impact(bool critical, bool boss)
        {
            if (Time.unscaledTime < nextImpactAt) return;
            nextImpactAt = Time.unscaledTime + .12f;
            if (critical) Shake(.055f);
            if (hitImpactCallback != null) hitImpactCallback(critical, boss);
        }

        public void BossDefeated()
        {
            Shake(.65f);
            if (bossDefeatCallback != null) bossDefeatCallback();
        }

        public void Shake(float duration = .12f)
        {
            shakeTime = Mathf.Max(shakeTime, duration);
        }

        public void FocusRoom(Vector2 center, bool snap = false)
        {
            roomCenter = center;
            if (snap) transform.position = new Vector3(center.x, center.y, -10f);
        }

        public void FocusRoom(Vector2 center, Vector2 size, bool snap = false)
        {
            roomSize = size;
            FocusRoom(center, false);
            if (snap) transform.position = DesiredPosition();
        }

        private Vector3 DesiredPosition()
        {
            var position = presentationFocus ? presentationPoint : Target == null ? roomCenter : (Vector2)Target.position;
            var halfView = new Vector2(ViewHalfHeight * ReferenceWidth / ReferenceHeight, ViewHalfHeight);
            var travel = Vector2.Max(Vector2.zero, roomSize * .5f + Vector2.one * .55f - halfView);
            position.x = Mathf.Clamp(position.x, roomCenter.x - travel.x, roomCenter.x + travel.x);
            position.y = Mathf.Clamp(position.y, roomCenter.y - travel.y, roomCenter.y + travel.y);
            return new Vector3(position.x, position.y, -10f);
        }

        private void LateUpdate()
        {
            var desired = DesiredPosition();

            if (shakeTime > 0f)
            {
                shakeTime -= Time.unscaledDeltaTime;
                shakeOffset = UnityEngine.Random.insideUnitCircle * .13f;
            }
            else
            {
                shakeOffset = Vector3.zero;
            }

            transform.position = Vector3.Lerp(transform.position, desired + shakeOffset, 8f * Time.unscaledDeltaTime);
            // PixelPerfectCamera owns the projection. Fractional zoom pulses made
            // the whole map shimmer; hit stop and positional shake retain the impact.
        }
    }
}
