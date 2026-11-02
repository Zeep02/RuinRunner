using UnityEngine;
namespace RuinRunner
{
    /// <summary>Committed short mortar arc; the complete landing footprint is visible before the hit.</summary>
    public sealed class PlayerMortarShell:MonoBehaviour
    {
        PlayerController owner;Vector2 start,target;float age,damage;int epoch;NativeCombatEffect landing;
        public static void Create(PlayerController owner,Vector2 origin,Vector2 aim,float damage)
        {
            var root=new GameObject("Player mortar shell");root.transform.position=origin;
            var shell=root.AddComponent<PlayerMortarShell>();shell.owner=owner;shell.start=origin;shell.target=CombatActionGeometry.Clip(origin,aim,3.4f);shell.damage=damage;shell.epoch=owner.CombatEpoch;
            NativeCombatEffect.Create("Shell",origin,Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg,.55f,0,Color.white,0,root.transform,true)?.BelowActor(owner.BodyRenderer,-3);
            shell.landing=NativeCombatEffect.Create("RewardRing",shell.target,0,3.2f,0,new Color(.65f,.9f,1,.6f),0,null,true);shell.landing?.BelowActor(owner.BodyRenderer,-4);
        }
        void Update()
        {
            if(owner==null||!owner.IsCombatEpochValid(epoch)){Destroy(gameObject);return;}
            age+=Time.deltaTime;float t=Mathf.Clamp01(age/.38f);
            transform.position=Vector2.Lerp(start,target,t)+Vector2.up*(Mathf.Sin(t*Mathf.PI)*.7f);
            if(t<1)return;
            foreach(var enemy in CombatActionGeometry.Circle(target,1.6f))owner.SecondaryHit(enemy,damage,((Vector2)enemy.transform.position-target).normalized*7);
            CombatPresentation.Explosion(target,1.6f,true,true);CombatAudio.PlayImpact(true);Destroy(gameObject);
        }
        void OnDestroy(){if(landing!=null)Destroy(landing.gameObject);}
    }
}
