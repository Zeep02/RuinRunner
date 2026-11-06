using System;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>What a kill's gold looks like: it falls with the experience and is counted when it reaches the hero.</summary>
    public sealed class GoldCoin : MonoBehaviour
    {
        public static int InFlight { get; private set; }
        Transform target;Func<bool> canCollect;Vector2 drift;SpriteRenderer view;Sprite[] frames;
        int amount;float homingSpeed=4,age,settleDelay;bool counted;

        public static void Burst(Vector2 position,int total,Transform target,Func<bool> canCollect)
        {
            if(total<=0||target==null)return;
            int count=Mathf.Clamp(total/2,1,8);
            for(int i=0;i<count;i++)
                Create(position,total/count+(i<total%count?1:0),target,canCollect);
        }
        static void Create(Vector2 position,int amount,Transform target,Func<bool> canCollect)
        {
            var root=new GameObject("GoldCoin");root.transform.position=position;
            var coin=root.AddComponent<GoldCoin>();
            coin.frames=IndividualArt.Frames("Feedback/Gold",8);
            coin.view=root.AddComponent<SpriteRenderer>();coin.view.sortingOrder=14;
            coin.target=target;coin.amount=amount;coin.canCollect=canCollect;
            coin.drift=UnityEngine.Random.insideUnitCircle*2.2f+Vector2.up*1.6f;coin.settleDelay=UnityEngine.Random.Range(.2f,.4f);
            coin.age=UnityEngine.Random.value*3;coin.counted=true;InFlight+=amount;
            coin.view.sprite=coin.frames[Mathf.FloorToInt(coin.age*10f)%coin.frames.Length];
        }
        void Update()
        {
            if(Time.deltaTime<=0)return;
            if(target==null){Destroy(gameObject);return;}
            age+=Time.deltaTime;
            // A tiny yellow orb pulses at native size, like the experience pickup.
            view.sprite=frames[Mathf.FloorToInt(age*10f)%frames.Length];
            if(settleDelay>0||canCollect!=null&&!canCollect())
            {
                settleDelay-=Time.deltaTime;
                transform.position+=(Vector3)(drift*Time.deltaTime);
                drift=Vector2.Lerp(drift,Vector2.zero,3*Time.deltaTime);homingSpeed=4;return;
            }
            homingSpeed=Mathf.MoveTowards(homingSpeed,16,22*Time.deltaTime);
            transform.position=Vector2.MoveTowards(transform.position,target.position,homingSpeed*Time.deltaTime);
            if(Vector2.Distance(transform.position,target.position)<=.42f)
            {CombatAudio.Native("coin",AudioChannel.Interface,.32f);Destroy(gameObject);}
        }
        void OnDestroy(){if(counted){counted=false;InFlight=Mathf.Max(0,InFlight-amount);}}
    }
}
