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
