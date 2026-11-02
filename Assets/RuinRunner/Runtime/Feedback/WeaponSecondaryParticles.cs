using UnityEngine;

namespace RuinRunner
{
    /// <summary>Bounded native fragments. Cosmetic motion cannot deal damage or change run randomness.</summary>
    public sealed class WeaponSecondaryParticles : MonoBehaviour
    {
        static int active;
        Vector2 velocity;float age,duration,gravity;Color tint;SpriteRenderer view;
        SpriteRenderer caster;
        public static int ActiveCount=>active;
        public static void Fan(string clip,Vector2 point,Vector2 axis,int count,float spread,float speed,float lifetime,float scale,int order,Color color,float gravity=0,SpriteRenderer owner=null)
        {
            var frames=NativeCombatEffect.Frames(clip);var forward=Mathf.Atan2(axis.y,axis.x)*Mathf.Rad2Deg;
            for(int i=0;i<count&&active<64;i++)
            {
                float offset=count==1?0:Mathf.Lerp(-spread*.5f,spread*.5f,i/(float)(count-1));
                var rad=(forward+offset)*Mathf.Deg2Rad;var root=new GameObject("WeaponFragment_"+clip);root.transform.position=point;
                root.transform.localScale=Vector3.one*scale;
                var fx=root.AddComponent<WeaponSecondaryParticles>();fx.duration=lifetime*(.85f+(i%3)*.12f);fx.gravity=gravity;
                fx.velocity=new Vector2(Mathf.Cos(rad),Mathf.Sin(rad))*speed*(.72f+(i%4)*.12f);fx.tint=color;
                fx.view=root.AddComponent<SpriteRenderer>();fx.view.sprite=frames[i%frames.Length];fx.view.color=color;fx.view.sortingOrder=order;
                fx.caster=owner;if(owner!=null){fx.view.sortingLayerID=owner.sortingLayerID;fx.view.sortingOrder=owner.sortingOrder-2;}
                active++;
            }
        }
        void Update()
        {
            var dt=Time.deltaTime;if(dt<=0)return;age+=dt;if(age>=duration){Destroy(gameObject);return;}
            var origin=(Vector2)transform.position;velocity.y-=gravity*dt;var next=origin+velocity*dt;
            var wall=Physics2D.Linecast(origin,next,1<<GameLayers.Ground);
            if(wall.collider!=null){Destroy(gameObject);return;}
            // Fixed native particle pixels; never interpolate their sampling.
            transform.position=new Vector3(Mathf.Round(next.x*64)/64,Mathf.Round(next.y*64)/64,0);
            var c=tint;c.a*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,age/duration));view.color=c;
        }
        void LateUpdate(){if(caster!=null)view.sortingOrder=caster.sortingOrder-2;}
        void OnDestroy(){active--;}
    }
}
