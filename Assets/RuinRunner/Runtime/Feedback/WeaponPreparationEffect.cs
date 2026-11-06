using UnityEngine;

namespace RuinRunner
{
    /// <summary>Charge follows the held blade. Cancel, swap and death cannot leave a stale glow.</summary>
    [DefaultExecutionOrder(220)]
    public sealed class WeaponPreparationEffect : MonoBehaviour
    {
        PlayerController player;
        NativeCombatEffect charge, readyFlash;
        bool ready;
        public void Initialize(PlayerController value)
        {
            player = value;
        }
        void LateUpdate()
        {
            if (player == null)
                return;
            bool charging = player.IsGreatswordCharging && !player.IsDead && player.EquippedWeapon != null && player.EquippedWeapon.Family == WeaponFamily.Greatsword;
            if (!charging)
            {
                Clear();
                return;
            }
            if (player.AnimationPaused)
                return;
            var pivot = transform.Find("HeldWeaponPivot");
            var held = GetComponent<HeldWeaponView>();
            if (pivot == null || held == null)
                return;
            var point = held.BladePoint(.42f);
            if (charge == null)
            {
                charge = NativeCombatEffect.Create("Charge", point, 0, .65f, 0, Color.white, 2010, pivot, true);
                charge?.BelowActor(transform.Find("VisualRoot").GetComponent<SpriteRenderer>());
                charge?.SetLoopFrameRate(18);
            }
            if (charge != null)
                charge.transform.position = point;
            if (!ready && player.GreatswordChargeProgress >= .98f)
            {
                ready = true;
                readyFlash = NativeCombatEffect.Create("MetalSpark", point, 0, .70f, .23f, Color.white, 2012, pivot);
                readyFlash?.BelowActor(transform.Find("VisualRoot").GetComponent<SpriteRenderer>());
                CombatAudio.Native("block", AudioChannel.Player, .17f, 1.4f);
            }
        }
        void Clear()
        {
            // A completed-charge glint belongs to this preparation too. It must
            // not drift over the face or survive release, dash or equipment swap.
            if (charge != null)
            {
                charge.GetComponent<SpriteRenderer>().enabled = false;
                Destroy(charge.gameObject);
                charge = null;
            }
            if (readyFlash != null)
            {
                readyFlash.GetComponent<SpriteRenderer>().enabled = false;
                Destroy(readyFlash.gameObject);
                readyFlash = null;
            }
            ready = false;
        }
        void OnDisable()
        {
            Clear();
        }
    }
}
