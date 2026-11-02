using UnityEngine;

namespace RuinRunner
{
    /// <summary>Weapon silhouettes and distinct release/contact/aftermath, independent of gameplay damage.</summary>
    public static class CombatPresentation
    {
        static readonly Color Powder=new Color(1f,.93f,.78f),Dust=new Color(.82f,.78f,.69f,.64f);
        static float Angle(Vector2 aim)=>Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg;
        public static Color Tint(WeaponFamily family,bool special=false,string weaponId=null)
        {
            if(weaponId=="void_revolver")return new Color(.82f,.67f,1f);
            if(family==WeaponFamily.Bow)return new Color(.84f,1f,.91f);
            if(family==WeaponFamily.Crossbow||family==WeaponFamily.Rifle)return new Color(.8f,.94f,1f);
            if(family==WeaponFamily.Cannon||family==WeaponFamily.Shotgun)return Powder;
            return Color.white;
        }
        static void Fragments(string clip,Vector2 point,Vector2 axis,int count,float spread,float speed,float life,float scale,int order,Color color,float gravity=0,SpriteRenderer owner=null)
            =>WeaponSecondaryParticles.Fan(clip,point,axis,count,spread,speed,life,scale,order,color,gravity,owner);
        public static void Muzzle(WeaponFamily family,Vector2 point,Vector2 aim,bool special,string weaponId,int order,SpriteRenderer owner=null)
        {
            var angle=Angle(aim);var front=point+aim*.10f;
            if(Physics2D.Linecast(point,front,1<<GameLayers.Ground).collider!=null)front=point;
            NativeCombatEffect Release(string clip,Vector2 position,float rotation,float diameter,float life,Color tint,int layer,float delay=0)
            {
                var fx=NativeCombatEffect.Create(clip,position,rotation,diameter,life,tint,layer,delay:delay);fx?.BelowActor(owner);return fx;
            }
            void ReleaseFragments(string clip,Vector2 position,Vector2 axis,int count,float spread,float speed,float life,float scale,int layer,Color tint,float gravity=0)
                =>WeaponSecondaryParticles.Fan(clip,position,axis,count,spread,speed,life,scale,layer,tint,gravity,owner);
            switch(family)
            {
                case WeaponFamily.Bow:
                    Release("Wind",front,angle-90,special?1.25f:.70f,.24f,Tint(family),order);
                    if(special)ReleaseFragments("LeafChip",front,aim,5,95,2.8f,.40f,.58f,order,Color.white);
                    break;
                case WeaponFamily.Crossbow:
                    Release("CutStreak",front,angle-45,special?1.1f:.64f,.14f,Color.white,order);
                    if(special)Release("RifleWake",front,angle,1.4f,.18f,Color.white,order-1);
                    break;
                case WeaponFamily.Rifle:
                    Release("RifleFlash",front,angle,special?1.60f:.98f,.13f,Color.white,order);
                    if(special)Release("RifleWake",front+aim*.5f,angle,2.2f,.20f,Color.white,order-1);
                    break;
                case WeaponFamily.Shotgun:
                    var fan=Release("Ember",front+aim*.20f,angle,special?1.45f:.95f,.19f,Powder,order);
                    if(fan!=null){var shape=fan.transform.localScale;shape.y*=.62f;fan.transform.localScale=shape;}
                    ReleaseFragments("MetalSpark",front,aim,special?7:4,special?92:56,6,.18f,.62f,order, Powder);
                    Release("Smoke",point,angle-90,.65f,.43f,Dust,order-1,delay:.05f);
                    break;
                case WeaponFamily.Cannon:
                    Release("PowderFlash",front,angle-90,special?1.65f:1.2f,.22f,Color.white,order);
                    Release("GroundDust",point,angle-90,1.45f,.48f,Dust,order-1,delay:.06f);
                    ReleaseFragments("MetalSpark",front,aim,5,60,5,.24f,.7f,order,Powder);
                    break;
                default:
                    if(weaponId=="void_revolver")Release("Void",front,angle-90,special?1.1f:.60f,.23f,Color.white,order);
                    else
                    {
                        Release("PowderFlash",front,angle-90,weaponId=="flintlock"?.82f:.53f,.15f,Color.white,order);
                        if(weaponId=="flintlock")Release("Smoke",point,angle-90,.68f,.48f,Dust,order-1,delay:.04f);
                        else ReleaseFragments("MetalSpark",front,aim,2,22,5,.13f,.42f,order,Powder);
                    }
                    break;
            }
            CombatAudio.PlayWeapon(family,special,weaponId);
        }
        public static void Impact(WeaponFamily family,Vector2 point,Vector2 aim,bool special,bool critical,bool wall=false,string weaponId=null)
        {
            var angle=Angle(aim);float emphasis=critical?1.2f:1;
            // Explosions are emitted once by Blast, using the actual blast radius.
            if(family==WeaponFamily.Cannon||special&&weaponId=="flintlock")return;
            if(weaponId=="void_revolver")
            {
                NativeCombatEffect.Create("Void",point,angle-90,(special?1.35f:.85f)*emphasis,.34f,Color.white);
                NativeCombatEffect.Create("ParryCross",point,angle,.65f*emphasis,.17f,Tint(family,special,weaponId));
            }
            else switch(family)
            {
                case WeaponFamily.Bow:
                    NativeCombatEffect.Create("CutStreak",point,angle-45,.65f*emphasis,.18f,Color.white);
                    Fragments(wall?"StoneChip":"LeafChip",point,-aim,wall?3:4,100,2.5f,.32f,.55f,2003,Color.white,2);
                    if(special)NativeCombatEffect.Create("Wind",point,angle-90,1.10f,.28f,Tint(family),2001);
                    break;
                case WeaponFamily.Crossbow:
                    NativeCombatEffect.Create("PiercingHit",point,angle-90,(special?1.3f:.85f)*emphasis,.24f,Color.white);
                    Fragments("MetalSpark",point,aim,3,28,5,.21f,.65f,2003,Color.white);
                    break;
                case WeaponFamily.Rifle:
                    NativeCombatEffect.Create("RifleFlash",point,angle,(special?1.75f:.95f)*emphasis,.18f,Color.white);
                    NativeCombatEffect.Create("RicochetHit",point,angle,.63f*emphasis,.20f,Color.white);
                    Fragments("MetalSpark",point,aim,critical?5:3,36,6,.18f,.58f,2003,Color.white);
                    break;
                case WeaponFamily.Shotgun:
                    NativeCombatEffect.Create("MetalSpark",point,angle,.38f*emphasis,.18f,Powder);
                    Fragments(wall?"StoneChip":"MetalSpark",point,-aim,2,90,3,.25f,.52f,2003,Powder,1.5f);
                    break;
                default:
                    NativeCombatEffect.Create("MetalSpark",point,angle,.60f*emphasis,.17f,Powder);
                    Fragments(wall?"StoneChip":"MetalSpark",point,-aim,3,120,3,.27f,.52f,2003,Powder,2);
                    break;
            }
            if(wall)CombatAudio.Native(family==WeaponFamily.Bow?"impact":"block",AudioChannel.Monster,.18f,family==WeaponFamily.Crossbow?.8f:1.25f);
        }
        public static void Explosion(Vector2 point,float radius,bool cannon,bool special,int order=2002)
        {
            NativeCombatEffect.Create(cannon?"HeavyBlast":"CannonBlast",point,0,radius*1.85f,cannon?.62f:.34f,Color.white,order);
            NativeCombatEffect.Create("GroundDust",point,0,radius*2,.55f,Dust,order-2,delay:.09f);
            Fragments("StoneChip",point,Vector2.up,cannon?8:5,300,Mathf.Min(6,radius*3.5f),.58f,.65f,order-1,Color.white,6);
            Fragments("MetalSpark",point,Vector2.up,cannon?7:4,290,5,.30f,.72f,order+1,Powder,3);
        }
        public static void MeleeContact(WeaponFamily family,Vector2 point,Vector2 aim,bool heavy,bool critical,int order,SpriteRenderer owner=null)
        {
            if(family==WeaponFamily.Mace)
            {Fragments("StoneChip",point,-aim,heavy?5:3,160,3.3f,.38f,.62f,order,Color.white,3,owner);return;}
            if(family==WeaponFamily.Katana)
                NativeCombatEffect.Create("ParryCross",point,Angle(aim),critical?.95f:.65f,.18f,Color.white,order)?.AboveWeapon(owner);
            else NativeCombatEffect.Create("CutStreak",point,Angle(aim)-45,critical?1.05f:.72f,.16f,Color.white,order)?.AboveWeapon(owner);
            if(heavy||critical)Fragments("MetalSpark",point,aim,4,90,4,.22f,.62f,order,Powder,owner:owner);
        }
        public static void Melee(WeaponFamily family,MeleeAttackMotion motion,Vector2 point,float angle,Vector2 size,bool heavy,bool finisher,int order,SpriteRenderer owner=null)
        {
            if(family==WeaponFamily.Mace&&(motion==MeleeAttackMotion.Impact||heavy))
            {
                // A physical downward impact: ground compression, expanding dust,
                // then falling chips. The old four-pose white pillar hid that motion.
                float diameter=Mathf.Max(size.x,size.y);
                var bloom=NativeCombatEffect.Create("SlamDust",point,0,diameter*.9f,.38f,new Color(.95f,.86f,.67f,.78f),order-5);
                bloom?.BelowActor(owner);
                var dust=NativeCombatEffect.Create("GroundDust",point,0,diameter,.45f,Dust,order-6,delay:.05f);
                dust?.BelowActor(owner,-3);
                Fragments("StoneChip",point,Vector2.up,7,300,4.2f,.48f,.72f,order-1,Color.white,5,owner);
            }
            else if(family==WeaponFamily.Greatsword&&motion==MeleeAttackMotion.Cleave)
            {
                var dust=NativeCombatEffect.Create("GroundDust",point,angle,Mathf.Max(size.x,size.y)*.72f,.42f,Dust,order-5,delay:.06f);
                dust?.BelowActor(owner);
                Fragments("MetalSpark",point,new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad)),heavy?6:4,100,5,.28f,.70f,order,Powder,owner:owner);
            }
            // Dagger thrust, longsword thrust, and katana cross-cut already have
            // their own authored silhouettes. No unrelated ring/fire overlays.
        }
        public static void MaceSlam(Vector2 point,float diameter,int order,SpriteRenderer owner=null)
        {
            var bloom=NativeCombatEffect.Create("SlamDust",point,0,diameter*.84f,.58f,new Color(.95f,.87f,.68f,.58f),order-4);
            bloom?.BelowActor(owner);
            var dust=NativeCombatEffect.Create("GroundDust",point,0,diameter*.92f,.55f,Dust,order-4,delay:.045f);
            dust?.BelowActor(owner,-3);
            Fragments("StoneChip",point,Vector2.up,10,330,5.6f,.62f,.78f,order-1,Color.white,6,owner);
            Fragments("MetalSpark",point,Vector2.up,6,310,4,.28f,.70f,order+1,Powder,3,owner);
        }
        public static void Defense(Vector2 point,Vector2 aim,bool parry,int order,SpriteRenderer owner=null,Vector2? bladeContact=null)
        {
            var contact=bladeContact??point+aim*.52f;
            var flash=NativeCombatEffect.Create(parry?"ParryCross":"MetalSpark",contact,Angle(aim),parry?1.40f:.8f,parry?.25f:.16f,Color.white,order);
            flash?.AboveWeapon(owner);
            Fragments("MetalSpark",contact,-aim,parry?6:3,140,parry?5:3.5f,.24f,.65f,order+1,Powder);
            if(parry)
            {
                var counter=NativeCombatEffect.Create("TwinCrescent",point+aim*.85f,Angle(aim)+90,2.7f,.24f,Color.white,order-1,delay:.065f);
                counter?.AboveWeapon(owner);
            }
        }
        public static void Projectile(Transform root,WeaponFamily family,bool special,string weaponId,out float trailWidth,out Color trace,SpriteRenderer owner=null)
        {
            string clip;float width;
            switch(family)
            {
                case WeaponFamily.Bow:clip="Arrow";width=.68f;break;
                case WeaponFamily.Crossbow:clip="Bolt";width=.76f;break;
                case WeaponFamily.Rifle:clip="Slug";width=.13f;break;
                case WeaponFamily.Cannon:clip="Shell";width=.54f;break;
                case WeaponFamily.Shotgun:clip="Slug";width=.10f;break;
                default:clip="Slug";width=.14f;break;
            }
            if(weaponId=="void_revolver"){clip="VoidBolt";width=special?.95f:.65f;}
            else if(special&&weaponId=="flintlock"){clip="Fireball";width=.50f;}
            else if(special&&(family==WeaponFamily.Bow||family==WeaponFamily.Crossbow))width*=1.1f;
            NativeCombatEffect.Create(clip,root.position,root.eulerAngles.z,width,0,Color.white,2000,root,true)?.BelowActor(owner);
            if(special&&family==WeaponFamily.Rifle)
                NativeCombatEffect.Create("RifleWake",root.position,root.eulerAngles.z,1.4f,0,Color.white,1998,root,true)?.BelowActor(owner,-3);
            trailWidth=family==WeaponFamily.Cannon?.12f:family==WeaponFamily.Rifle?(special?.18f:.07f):family==WeaponFamily.Shotgun?.045f:.06f;
            trace=weaponId=="void_revolver"?new Color(.61f,.37f,.92f,.65f):family==WeaponFamily.Cannon?new Color(.65f,.58f,.48f,.38f):
                family==WeaponFamily.Bow?new Color(.84f,.99f,.88f,.40f):family==WeaponFamily.Crossbow||family==WeaponFamily.Rifle?new Color(.75f,.92f,1f,.65f):new Color(1f,.85f,.54f,.60f);
        }
    }
}
