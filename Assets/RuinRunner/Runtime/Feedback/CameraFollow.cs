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

        public static Action<float, float> DramaticSlow;
        /// <summary>A melee blow's stop, seconds and time scale. Lengthens a shorter stop but never cuts into a longer slow-down.</summary>
        public static Action<float, float> MeleeHitStop;
        private Vector3 shakeOffset;
        private float shakeTime, shakeDuration = 1f, shakeStrength;
        private Vector2 kickOffset;
        private Vector3 followPosition;
        private bool hasFollowPosition;
        private Vector2 roomCenter;
        private Vector2 roomSize = new Vector2(22.6f, 11.8f);
        private Camera viewCamera;
        private bool presentationFocus;
        private Vector2 presentationPoint;
        public void FocusPresentation(Vector2 point)
        {
            presentationPoint = point;
            presentationFocus = true;
            shakeTime = 0;
            shakeOffset = Vector3.zero;
        }
        public void ReleasePresentation()
        {
            presentationFocus = false;
        }
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
            cameraObject.AddComponent<MasterLimiter>();
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
            if (Time.unscaledTime < nextFinisherImpactAt)
                return;
            nextFinisherImpactAt = Time.unscaledTime + .16f;
            Shake(.055f);
            finisherImpactCallback?.Invoke();
        }

        public void Impact(bool critical, bool boss)
        {
            if (Time.unscaledTime < nextImpactAt)
                return;
            nextImpactAt = Time.unscaledTime + .12f;
            Shake(critical ? .13f : .07f, critical ? .2f : .09f);
            if (hitImpactCallback != null)
                hitImpactCallback(critical, boss);
        }

        public void KillImpact()
        {
            Shake(.14f, .17f);
            finisherImpactCallback?.Invoke();
        }

        public void BossDefeated()
        {
            Shake(.8f, .4f);
            if (bossDefeatCallback != null)
                bossDefeatCallback();
        }

        public void Shake(float duration = .12f) => Shake(duration, .1f + duration * .55f);

        public void Shake(float duration, float strength)
        {
            if (duration < shakeTime && strength < shakeStrength)
                return;
            shakeTime = Mathf.Max(shakeTime, duration);
            shakeDuration = Mathf.Max(.001f, shakeTime);
            shakeStrength = Mathf.Max(shakeTime > 0 ? shakeStrength : 0, strength);
        }

        // A single directional shove: heavy blows push the frame the way they travel.
        public void Kick(Vector2 direction, float distance)
        {
            // One swing can hit a crowd; the shove must not add up to losing sight of the fight.
            kickOffset = Vector2.ClampMagnitude(kickOffset + (direction.sqrMagnitude > .0001f ? direction.normalized * distance : Vector2.zero), .35f);
        }

        public void FocusRoom(Vector2 center, bool snap = false)
        {
            roomCenter = center;
            if (snap)
            {
                transform.position = new Vector3(center.x, center.y, -10f);
                hasFollowPosition = false;
            }
        }

        public void FocusRoom(Vector2 center, Vector2 size, bool snap = false)
        {
            roomSize = size;
            FocusRoom(center, false);
            if (snap)
            {
                transform.position = DesiredPosition();
                hasFollowPosition = false;
            }
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
                shakeOffset = UnityEngine.Random.insideUnitCircle * shakeStrength * Mathf.Clamp01(shakeTime / shakeDuration);
            }
            else
            {
                shakeOffset = Vector3.zero;
                shakeStrength = 0f;
            }
            kickOffset = Vector2.MoveTowards(kickOffset, Vector2.zero, (2f + kickOffset.magnitude * 12f) * Time.unscaledDeltaTime);

            // Follow smoothly, but keep shake and kick out of the smoothing:
            // filtered through the follow lerp they all but disappeared.
            followPosition = Vector3.Lerp(hasFollowPosition ? followPosition : desired, desired, 8f * Time.unscaledDeltaTime);
            hasFollowPosition = true;
            transform.position = followPosition + shakeOffset + (Vector3)kickOffset;
            // PixelPerfectCamera owns the projection. Fractional zoom pulses made
            // the whole map shimmer; hit stop and positional shake retain the impact.
        }
    }
}
