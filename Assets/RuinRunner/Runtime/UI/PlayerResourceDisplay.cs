using UnityEngine;
using UnityEngine.Rendering;

namespace RuinRunner
{
    public sealed class PlayerResourceDisplay : MonoBehaviour
    {
        private const int ForegroundGaugeOrder = 32760;
        private PlayerController player;
        private GameObject ammoRoot;
        private Transform ammoFill;
        private SpriteRenderer ammoFillRenderer;
        private DashChargeGraphic dashGauge;

        public void Initialize(PlayerController owner)
        {
            player = owner;
            ammoRoot = new GameObject("AmmoGauge");
            ammoRoot.transform.SetParent(transform, false);
            KeepGaugeAboveCombat(ammoRoot);
            // Both gauges stay centered; ammo/reload occupies the lower row.
            ammoRoot.transform.localPosition = new Vector3(0f, -1.6875f, 0f);

            var ammoBackground = CreateBarPart("AmmoGaugeBackground", ammoRoot.transform, new Vector3(.76f, .52f, 1f), new Color(.03f, .04f, .07f, .9f), 28);
            var ammoFrame = PixelWorldArt.GetSprite(WorldInteractionArt.AmmoFrame);
            if (ammoFrame != null)
            {
                ammoBackground.GetComponent<SpriteRenderer>().sprite = ammoFrame;
                ammoBackground.GetComponent<SpriteRenderer>().color = Color.white;
                ammoBackground.transform.localScale = Vector3.one * .75f;
            }
            var fill = CreateBarPart("AmmoGaugeFill", ammoRoot.transform, new Vector3(.56f, .2f, 1f), new Color(1f, .78f, .2f), 29);
            ammoFill = fill.transform;
            ammoFillRenderer = fill.GetComponent<SpriteRenderer>();

            var gauge = new GameObject("DashCharges");
            gauge.transform.SetParent(transform, false);
            KeepGaugeAboveCombat(gauge);
            gauge.transform.localPosition = new Vector3(0, -1.25f, 0);
            dashGauge = gauge.AddComponent<DashChargeGraphic>();
            dashGauge.SetCharges(player.DashCharges, player.DashRechargeProgress);
        }

        private void LateUpdate()
        {
            if (player == null) return;
            if(player.IsDead)
            {
                ammoRoot.SetActive(false);
                dashGauge.gameObject.SetActive(false);
                return;
            }
            var slot = player.ActiveWeaponState;
            var showAmmo = slot != null && slot.Weapon != null && slot.Weapon.AttackType == WeaponAttackType.Ranged;
            ammoRoot.SetActive(showAmmo);

            if (showAmmo)
            {
                var ratio = slot.IsReloading ? slot.ReloadProgress : slot.Ammo / (float)slot.Weapon.MagazineSize;
                var width = .56f * Mathf.Clamp01(ratio);
                ammoFill.localScale = new Vector3(width, .2f, 1f);
                ammoFill.localPosition = new Vector3((width - .56f) * .5f, 0, 0);
                ammoFillRenderer.color = slot.IsReloading ? new Color(1f, .45f, .15f) : new Color(1f, .78f, .2f);
            }

            dashGauge.SetCharges(player.DashCharges, player.DashRechargeProgress);
        }

        private static void KeepGaugeAboveCombat(GameObject root)
        {
            // A world-positioned HUD still needs to stay legible over large combat arcs.
            // Group sorting preserves the background/fill order inside each gauge.
            var group = root.AddComponent<SortingGroup>();
            group.sortingLayerName = "Default";
            group.sortingOrder = ForegroundGaugeOrder;
        }

        private static GameObject CreateBarPart(string name, Transform parent, Vector3 scale, Color color, int sortingOrder)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localScale = scale;
            var renderer = PrototypeVisuals.AddSprite(part, color, sortingOrder);
            renderer.sprite = PrototypeVisuals.Capsule;
            return part;
        }
    }
}
