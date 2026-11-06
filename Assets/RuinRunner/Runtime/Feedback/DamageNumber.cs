using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>The number that jumps off a struck enemy. A critical is bigger, gold and lands harder; item damage stays small.</summary>
    public sealed class DamageNumber : MonoBehaviour
    {
        const int PoolSize=40,OutlineCopies=4,SortingOrder=6000;
        const float Lifetime=.62f,PopTime=.1f,CharacterSize=.036f;
        static readonly Color Normal=new Color(1f,.97f,.9f),Critical=new Color(1f,.76f,.16f),Item=new Color(.78f,.8f,.95f),Outline=new Color(.07f,.04f,.1f);
        static readonly Vector2[] Offsets={new Vector2(-1,0),new Vector2(1,0),new Vector2(0,-1),new Vector2(0,1)};
        static readonly Queue<DamageNumber> Free=new Queue<DamageNumber>();
        static readonly List<DamageNumber> Live=new List<DamageNumber>();
        readonly TextMesh[] layers=new TextMesh[OutlineCopies+1];
        Vector2 velocity;float age,size;Color color;

        public static void Show(Vector2 point,float amount,bool critical,bool original)
        {
            var number=Take();if(number==null)return;
            number.gameObject.SetActive(true);
            number.transform.position=point+new Vector2(Random.Range(-.22f,.22f),.5f);
            number.velocity=new Vector2(Random.Range(-.6f,.6f),critical?2.8f:2.2f);
            number.age=0;number.size=critical?1.45f:original?1f:.75f;
            number.color=critical?Critical:original?Normal:Item;
            var text=Mathf.Max(1,Mathf.RoundToInt(amount)).ToString()+(critical?"!":"");
            foreach(var layer in number.layers)layer.text=text;
            number.Paint(1);
            number.transform.localScale=Vector3.one*number.size*1.5f;
            Live.Add(number);
        }
        static DamageNumber Take()
        {
            while(Free.Count>0){var free=Free.Dequeue();if(free!=null)return free;}
            Live.RemoveAll(item=>item==null);
            if(Live.Count<PoolSize)return Create();
            var oldest=Live[0];Live.RemoveAt(0);return oldest;
        }
        static DamageNumber Create()
        {
            var font=GameTypography.Detail;if(font==null)return null;
            var root=new GameObject("DamageNumber");
            var number=root.AddComponent<DamageNumber>();
            for(int i=0;i<=OutlineCopies;i++)
            {
                var child=new GameObject(i<OutlineCopies?"Outline":"Text");child.transform.SetParent(root.transform,false);
                var mesh=child.AddComponent<TextMesh>();
                mesh.font=font;mesh.fontSize=64;mesh.characterSize=CharacterSize;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;
                var renderer=child.GetComponent<MeshRenderer>();renderer.sharedMaterial=font.material;renderer.sortingOrder=SortingOrder+(i<OutlineCopies?0:1);
                if(i<OutlineCopies)child.transform.localPosition=Offsets[i]*.024f;
                number.layers[i]=mesh;
            }
            return number;
        }
        void Paint(float alpha)
        {
            for(int i=0;i<OutlineCopies;i++)layers[i].color=new Color(Outline.r,Outline.g,Outline.b,alpha);
            layers[OutlineCopies].color=new Color(color.r,color.g,color.b,alpha);
        }
        void Update()
        {
            // Hit stop freezes the world, not the number.
            float delta=Time.unscaledDeltaTime;age+=delta;
            if(age>=Lifetime){Live.Remove(this);gameObject.SetActive(false);Free.Enqueue(this);return;}
            transform.position+=(Vector3)(velocity*delta);
            velocity.y-=7.5f*delta;velocity.x*=1-2.5f*delta;
            // Slams in oversized, settles, then fades over the last third.
            float pop=age<PopTime?Mathf.Lerp(1.5f,1f,age/PopTime):1f;
            transform.localScale=Vector3.one*size*pop;
            Paint(Mathf.Clamp01((Lifetime-age)/(Lifetime*.34f)));
        }
    }
}
