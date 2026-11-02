using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public sealed class PlayerProjectile : MonoBehaviour
    {
        private readonly HashSet<EnemyController> hitEnemies = new HashSet<EnemyController>();
        private Vector2 velocity;
        private float damage;
        private bool critical;
        private int remainingHits;
        private float lifetime;
        private static Material trailMaterial;
        private WeaponFamily family;
        private bool special,finished,exploded;
        private bool explosiveShot;
        private string weaponId;
        private BoxCollider2D damageBox;
        private PlayerController owner;
        private int sourceAction;
        private int upgradeLevel;
        private SpriteRenderer castingBody;
        private TrailRenderer trace;
        private System.Action<EnemyController,Vector2> contactCallback;
        private bool secondary;
        private int launchEpoch;
        private long sourceWeaponIdentity;
        private int ricochets;
        private bool anchorRound;
        public PlayerProjectile OnContact(System.Action<EnemyController,Vector2> callback){contactCallback=callback;return this;}
        public PlayerProjectile AsSecondary(){secondary=true;return this;}
        public PlayerProjectile WithMutation(bool ricochet,bool anchor){ricochets=ricochet?1:0;anchorRound=anchor;return this;}

        public static PlayerProjectile Create(
            Vector2 position,
            Vector2 direction,
            float speed,
            float attackDamage,
            bool isCritical,
            Color color,
            Vector2 size,
            int pierceCount,
            WeaponFamily family = WeaponFamily.Rifle,
            bool special = false,
            string weaponId = null,
            PlayerController sourceOwner = null,
            int action = 0,
            int upgrade = 0)
        {
            var projectileObject = new GameObject("PlayerSpecialProjectile");
            projectileObject.layer = GameLayers.Projectile;
            projectileObject.transform.position = position;
            projectileObject.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            var castingBody=sourceOwner==null?null:sourceOwner.transform.Find("VisualRoot")?.GetComponent<SpriteRenderer>();
            CombatPresentation.Projectile(projectileObject.transform,family,special,weaponId,out var trailWidth,out var traceColor,castingBody);
            // A short taper keeps fast ammunition legible between rendered frames.
            // The trail is presentation only; it never extends a hit backwards.
            if(trailMaterial==null)trailMaterial=new Material(Resources.Load<Shader>("Shaders/PixelTrail")){name="Player projectile trails"};
            var trail=projectileObject.AddComponent<TrailRenderer>();
            // Projectile lifetime belongs to this component, never the trail.
            trail.autodestruct=false;trail.emitting=true;
            trail.sharedMaterial=trailMaterial;trail.time=family==WeaponFamily.Cannon?.14f:.095f;
            trail.minVertexDistance=.04f;trail.sortingOrder=1999;
            trail.startWidth=trailWidth;
            trail.endWidth=.02f;
            trail.startColor=traceColor;traceColor.a=0;trail.endColor=traceColor;
            trail.Clear();
            if(castingBody!=null){trail.sortingLayerID=castingBody.sortingLayerID;trail.sortingOrder=castingBody.sortingOrder-3;}

            var collider = projectileObject.AddComponent<BoxCollider2D>();
            collider.size = size;
            // Adding a collider after a TrailRenderer makes Unity infer an
            // offset from the initial trail bounds, including the world origin.
            // Collision must stay on the projectile at every map coordinate.
            collider.offset = Vector2.zero;
            collider.isTrigger = true;
            var body = projectileObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var projectile = projectileObject.AddComponent<PlayerProjectile>();
            projectile.velocity = direction.normalized * speed;
            projectile.damage = attackDamage;
            projectile.critical = isCritical;
            projectile.remainingHits = Mathf.Max(1, pierceCount);
            projectile.lifetime = 1.8f;
            projectile.family=family;projectile.special=special;projectile.damageBox=collider;
            projectile.weaponId=weaponId;
            projectile.owner=sourceOwner;projectile.sourceAction=action;
            projectile.launchEpoch=sourceOwner==null?0:sourceOwner.CombatEpoch;
            projectile.sourceWeaponIdentity=sourceOwner==null?0:sourceOwner.CombatWeaponIdentity;
            projectile.upgradeLevel=upgrade;
            projectile.castingBody=castingBody;projectile.trace=trail;
            projectile.explosiveShot=family==WeaponFamily.Cannon||special&&weaponId=="flintlock";
            return projectile;
        }

        private void FixedUpdate()
        {
            if(finished)return;
            if(owner!=null&&(owner.ItemEffectsSuspended||special&&!owner.IsCombatEpochValid(launchEpoch))){finished=true;Destroy(gameObject);return;}
            var step = velocity * Time.fixedDeltaTime;
            var wall=Physics2D.BoxCast(transform.position,damageBox.size,transform.eulerAngles.z,step.normalized,step.magnitude,1<<GameLayers.Ground);
            if (wall.collider!=null)
            {
                if(ricochets>0){ricochets--;velocity=Vector2.Reflect(velocity,wall.normal);transform.position=wall.centroid+wall.normal*.025f;transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg);trace.Clear();return;}
                HitWall(wall.point+wall.normal*.025f);
                return;
            }
            transform.position += (Vector3)step;
            lifetime -= Time.fixedDeltaTime;
            if (lifetime <= 0f) Destroy(gameObject);
        }

        private void LateUpdate()
        {
            // Launching north must not paint the shaft or persistent trace over
            // the caster's face. This only changes draw order, never collision.
            if(castingBody!=null&&trace!=null){trace.sortingLayerID=castingBody.sortingLayerID;trace.sortingOrder=castingBody.sortingOrder-3;}
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if(finished)return;
            var prop = other.GetComponent<EnvironmentProp>();
            if (prop != null) { prop.Hit(); return; }
            var enemy = EnemyController.DamageTarget(other);
            if (enemy == null)
            {
                if (other.gameObject.layer == GameLayers.Ground)HitWall(other.ClosestPoint(transform.position));
                return;
            }
            if (!hitEnemies.Add(enemy)) return;

            var point=other.ClosestPoint(transform.position);
            if(Physics2D.Linecast(transform.position,point,1<<GameLayers.Ground).collider!=null)return;
            if(owner!=null&&!secondary)owner.OnDirectCombatHit(enemy,sourceAction,damage,false,sourceWeaponIdentity:sourceWeaponIdentity,magazineHit:!special);
            contactCallback?.Invoke(enemy,point);
            if(secondary&&owner!=null)owner.SecondaryHit(enemy,damage,velocity.normalized*4f);else enemy.TakeDamage(damage, velocity.normalized * 4f, critical);
            if(anchorRound&&owner!=null){anchorRound=false;if(owner.IsCombatEpochValid(launchEpoch))owner.PlantAwakenedRound(point,damage*.45f,sourceAction,sourceWeaponIdentity);}
            CombatPresentation.Impact(family,point,velocity.normalized,special,critical,false,weaponId);
            if(explosiveShot){Blast(point);remainingHits=1;}
            remainingHits--;
            if (remainingHits <= 0){finished=true;Destroy(gameObject);}
        }

        private void HitWall(Vector2 point)
        {
            if(finished)return;finished=true;
            CombatPresentation.Impact(family,point,velocity.normalized,special,critical,true,weaponId);
            if(explosiveShot)Blast(point);
            Destroy(gameObject);
        }

        private void Blast(Vector2 point)
        {
            if(exploded)return;exploded=true;
            var radius=family==WeaponFamily.Cannon?(special?1.8f:1.15f):.95f;
            if (upgradeLevel >= 2) radius += family == WeaponFamily.Cannon ? .35f : .25f;
            CombatPresentation.Explosion(point,radius,family==WeaponFamily.Cannon,special);
            foreach(var hit in Physics2D.OverlapCircleAll(point,radius,1<<GameLayers.Enemy))
            {
                var enemy=EnemyController.DamageTarget(hit);
                if(enemy==null||hitEnemies.Contains(enemy)||
                    Physics2D.Linecast(point,hit.ClosestPoint(point),1<<GameLayers.Ground).collider!=null)continue;
                hitEnemies.Add(enemy);
                var push=((Vector2)enemy.transform.position-point).normalized;
                if(owner!=null&&!secondary)owner.OnDirectCombatHit(enemy,sourceAction,damage*.35f,false,sourceWeaponIdentity:sourceWeaponIdentity,magazineHit:!special);
                if(secondary&&owner!=null)owner.SecondaryHit(enemy,damage*.35f,push*4f);else enemy.TakeDamage(damage*.35f,push*4f,critical);
            }
            CombatAudio.PlayImpact(true);
        }
    }
}
