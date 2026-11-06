using UnityEngine;

namespace RuinRunner
{
    /// <summary>Locked prediction and damage share the same world-space shape.</summary>
    public sealed class ArenaHazard : MonoBehaviour
    {
        private EnemyController owner;
        private PlayerController player;
        private float warning, active, elapsed, damage;
        private Vector2 size;
        private bool round, capsule, attempted;
        private float arcDegrees = 360;
        private HazardGroundFootprint footprint;
        private SpriteRenderer view;
        private Color tint;
        private ArenaHazardArt art;
        public ArenaHazardStyle Style { get; private set; }
        public bool IsActive => elapsed >= warning && elapsed < warning + active;
        public static ArenaHazard Create(EnemyController owner, PlayerController player, Vector2 position, Vector2 size,
            float angle, bool circle, float warning, float active, float damage, Color color,
            ArenaHazardStyle style = ArenaHazardStyle.Contour, bool omitCenter = false, float arcDegrees = 360, bool capsule = false)
        {
            var root = new GameObject("Hazard_" + owner.Type);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0,0,angle);
            var hazard = root.AddComponent<ArenaHazard>();
            hazard.owner=owner; hazard.player=player; hazard.size=size; hazard.round=circle;
            hazard.capsule=capsule;
            hazard.arcDegrees=Mathf.Clamp(arcDegrees,1,360);
            hazard.warning=warning; hazard.active=active; hazard.damage=damage; hazard.tint=color;
            var visual=new GameObject("WarningContour");visual.transform.SetParent(root.transform,false);
            hazard.view = visual.AddComponent<SpriteRenderer>();
            hazard.view.sprite = IndividualArt.Sprite("Campaign/Hazards/"+(circle?"Circle":"Rectangle"));
            hazard.view.sortingLayerName="WorldFloor";
            hazard.view.sortingOrder = 8;
            if(circle)visual.transform.localScale=new Vector3(size.x,size.y,1);
            else {hazard.view.drawMode=SpriteDrawMode.Sliced;hazard.view.size=size;}
            hazard.view.color = new Color(color.r,color.g,color.b,.65f);
            hazard.Style = style;
            hazard.footprint=HazardGroundFootprint.Create(root.transform,size,circle,color,hazard.arcDegrees,capsule);
            // A circle outline would suggest danger behind a frontal sector.
            if(capsule || (circle && hazard.arcDegrees<359))hazard.view.enabled=false;
            if (style != ArenaHazardStyle.Contour) hazard.art = new ArenaHazardArt(root.transform,style,size,omitCenter);
            return hazard;
        }
        public static ArenaHazard CreateChargeRoute(EnemyController owner, PlayerController player,
            Vector2 origin, Vector2 direction, float distance, float warning, float active, Color color, float contactRadius)
        {
            direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.right;
            distance = Mathf.Max(0, distance);
            float diameter = contactRadius * 2;
            // The actual contact test sweeps a circle along the committed route.
            // Include both end caps: the previous thin rectangle omitted targets
            // beside the body and beyond the planned landing point.
            return Create(owner, player, origin + direction * distance * .5f,
                new Vector2(distance + diameter, diameter), Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg,
                false, warning, active, 0, color, capsule: true);
        }
        public bool Contains(Vector2 point)
        {
            var local = (Vector2)transform.InverseTransformPoint(point);
            if(capsule)
            {
                float radius=size.y*.5f;
                float halfSegment=Mathf.Max(0,size.x*.5f-radius);
                local.x-=Mathf.Clamp(local.x,-halfSegment,halfSegment);
                return local.sqrMagnitude<=radius*radius;
            }
            local=new Vector2(local.x/size.x,local.y/size.y);
            return round ? local.sqrMagnitude <= .25f && (arcDegrees>=359 ||
                Mathf.Abs(Mathf.Atan2(local.y,local.x)*Mathf.Rad2Deg)<=arcDegrees*.5f) :
                Mathf.Abs(local.x) <= .5f && Mathf.Abs(local.y) <= .5f;
        }
        private bool ContainsActive(Vector2 point)
        {
            if(!Contains(point))return false;
            if(Style==ArenaHazardStyle.SealSweep)
            {
                var local=(Vector2)transform.InverseTransformPoint(point);
                float radius=size.x*.5f;
                float front=Mathf.Lerp(.65f,radius-.15f,Mathf.Clamp01((elapsed-warning)/active));
                return Mathf.Abs(local.magnitude-front)<=.48f;
            }
            if(Style==ArenaHazardStyle.TideLane)
            {
                var local=(Vector2)transform.InverseTransformPoint(point);
                bool vertical=size.y>size.x;float length=vertical?size.y:size.x;
                float along=vertical?local.y:local.x;
                // Water damage advances on the same clock as the native crests.
                float travel=Mathf.Max(.012f,(Mathf.CeilToInt(length/1.25f)-1)*.012f);
                float covered=Mathf.Clamp01((elapsed-warning)/travel);
                return along<=-length*.5f+length*covered;
            }
            return true;
        }
        private void Update()
        {
            if (owner == null || owner.IsDead || player == null) { Destroy(gameObject); return; }
            if (owner.AnimationPaused) return;
            elapsed += Time.deltaTime;
            if (elapsed >= warning + active + (art == null ? 0 : ArenaHazardArt.TailSeconds)) { Destroy(gameObject); return; }
            if (art != null) art.Sample(elapsed,warning,active);
            footprint.gameObject.SetActive(elapsed < warning + active);
            footprint.Sample(elapsed,warning,IsActive,active);
            view.enabled = !capsule && elapsed < warning + active && (!round || arcDegrees>=359);
            view.color = new Color(tint.r,tint.g,tint.b,IsActive ? 1f : .55f + .35f * Mathf.Clamp01(elapsed / warning));
            // The warning is drawn on the floor, so it is the hero's feet that stand in it or not.
            if (damage<=0 || !IsActive || attempted || !ContainsActive(player.FeetPoint)) return;
            attempted = true;
            player.TakeDamage(damage, ((Vector2)player.transform.position - (Vector2)transform.position).normalized);
        }
    }
}
