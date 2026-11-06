using UnityEngine;

namespace RuinRunner
{
    /// <summary>A door that opens as the player comes within reach of it and closes again when they walk away.
    /// Opening partway and turning back reverses from where it is. Interaction is handled by whoever owns the door.</summary>
    public sealed class ProximityGate : MonoBehaviour
    {
        public const string ArtRoot = "Campaign/Gate";
        public const int FrameCount = 12;
        private const float OpenSeconds = .55f, CloseSeconds = .45f;
        private SpriteRenderer view;
        private Sprite[] frames;
        private float reach, open;
        private bool wasOpening;
        public float OpenAmount => open;

        public static ProximityGate Attach(GameObject target, SpriteRenderer view, float reach)
        {
            var gate = target.AddComponent<ProximityGate>();
            gate.view = view; gate.reach = reach;
            gate.frames = IndividualArt.Frames(ArtRoot, FrameCount);
            view.sprite = gate.frames[0];
            return gate;
        }

        private void Update()
        {
            var opening = false;
            foreach (var player in PlayerController.ActiveCombatants)
                if (player != null && !player.IsDead && Vector2.Distance(player.transform.position, transform.position) <= reach) opening = true;
            if (opening != wasOpening)
            {
                CombatAudio.Native("block", AudioChannel.Ambience, .2f, opening ? .62f : .5f);
                wasOpening = opening;
            }
            open = Mathf.MoveTowards(open, opening ? 1f : 0f, Time.deltaTime / (opening ? OpenSeconds : CloseSeconds));
            view.sprite = frames[Mathf.Clamp(Mathf.RoundToInt(open * (FrameCount - 1)), 0, FrameCount - 1)];
        }
    }
}
