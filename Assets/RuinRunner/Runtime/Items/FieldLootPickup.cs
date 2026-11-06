using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public sealed class FieldLootPickup : MonoBehaviour
    {
        static readonly HashSet<FieldLootPickup> active=new HashSet<FieldLootPickup>();
        static int claimedInteractionFrame=-1;
        static int nextIdentity;
        int identity;
        public static int PendingDecisions
        {
            get
            {
                var count=0;
                foreach(var item in active)if(item!=null&&item.RequiresStageDecision)count++;
                return count;
            }
        }
        public bool RequiresStageDecision {get;set;}
        public WeaponRarity Rarity {get;private set;}
        public InventoryEntryKind InventoryKind {get;set;}=InventoryEntryKind.Accessory;
        public Vector2 LandingPosition {get;private set;}
        public bool IsSettled=>age>=delay+duration;
        public Vector3 ItemVisualCenter=>itemSpin.position;
        public Vector3 HaloCenter=>groundMarker.bounds.center;
        Func<bool> collect;
        PlayerController player;
        GameSettingsService settings;
        string displayName;
        GUIStyle promptStyle;
        Transform itemSpin;
        SpriteRenderer itemRenderer,groundMarker;
        Vector2 origin;
        Vector2 placementSize;
        float age,delay,duration,nextGlint;
        int depth;

        public static FieldLootPickup Create(Vector2 position,string name,Color rarityColor,PlayerController target,
            GameSettingsService gameSettings,Func<bool> collectCallback,int scatterIndex,Sprite displaySprite=null,
            WeaponRarity rarity=WeaponRarity.Common)
        {
            var root=new GameObject("FieldLoot_"+name);root.transform.position=position;
            var pickup=root.AddComponent<FieldLootPickup>();pickup.useGUILayout=false;
            pickup.identity=++nextIdentity;
            pickup.player=target;pickup.settings=gameSettings;pickup.collect=collectCallback;
            pickup.displayName=name;pickup.Rarity=rarity;pickup.origin=position;
            pickup.placementSize=displaySprite==null?new Vector2(.38f,.5f):(Vector2)displaySprite.bounds.size;
            pickup.LandingPosition=FindLanding(position,scatterIndex,target,pickup.placementSize,out var validOrigin);
            if(!validOrigin){pickup.origin=pickup.LandingPosition;root.transform.position=pickup.origin;}
            pickup.delay=Mathf.Min(scatterIndex*.085f,.34f);pickup.duration=RewardPresentation.RevealDuration(rarity);
            pickup.nextGlint=2.5f+scatterIndex*.3f;
            var spin=new GameObject("ItemSpin").transform;spin.SetParent(root.transform,false);pickup.itemSpin=spin;
            var visual=new GameObject("ItemVisual");visual.transform.SetParent(spin,false);
            visual.transform.localScale=displaySprite==null?new Vector3(.38f,.5f,1):Vector3.one;
            var view=PrototypeVisuals.AddSprite(visual,displaySprite==null?rarityColor:Color.white,2000);
            if(displaySprite!=null)view.sprite=displaySprite;
            visual.transform.localPosition=-Vector3.Scale(view.sprite.bounds.center,visual.transform.localScale);
            pickup.itemRenderer=view;view.enabled=false;
            var glow=new GameObject("Glow");glow.transform.SetParent(root.transform,false);
            var marker=PrototypeVisuals.AddSprite(glow,RewardPresentation.Color(rarity),1999);
            marker.sprite=IndividualArt.Sprite("CombatFX/RewardRing/03");marker.color=new Color(marker.color.r,marker.color.g,marker.color.b,.28f);
            glow.transform.localScale=Vector3.one*.65f;
            pickup.groundMarker=marker;marker.enabled=false;
            active.Add(pickup);return pickup;
        }

        static Vector2 FindLanding(Vector2 from,int index,PlayerController target,Vector2 imageSize,out bool validOrigin)
        {
            var game=UnityEngine.Object.FindAnyObjectByType<GameController>();
            var terrain=game!=null&&game.Player==target?game.DungeonMap?.Terrain:null;
            validOrigin=(terrain==null||StageNavigation.IsClear(terrain,from))&&
                Physics2D.OverlapCircle(from,.25f,1<<GameLayers.Ground)==null;
            var laneOrigin=validOrigin||target==null?from:(Vector2)target.transform.position+RoomLayoutRules.FootOffset;
            bool Clear(Vector2 point)
            {
                if(terrain!=null&&!StageNavigation.IsClear(terrain,point))return false;
                if(Physics2D.OverlapCircle(point,.30f,1<<GameLayers.Ground)!=null)return false;
                // Reserve the visible icon, including its settled hover, rather than only a pickup point.
                var center=point+Vector2.up*.225f;
                var size=imageSize+Vector2.one*.16f;
                if(Physics2D.OverlapBox(center,size,0,1<<GameLayers.Ground)!=null)return false;
                if(terrain!=null)
                    for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)
                        if(!terrain.IsOpen(center+Vector2.Scale(new Vector2(x,y),size*.5f)))return false;
                if(!StageNavigation.HasCombatLane(laneOrigin,point,.25f))return false;
                foreach(var other in active)
                {
                    if(other==null)continue;
                    var delta=other.LandingPosition-point;
                    if(delta.sqrMagnitude<.64f)return false;
                    var combined=(other.placementSize+imageSize)*.5f+Vector2.one*.12f;
                    if(Mathf.Abs(delta.x)<combined.x&&Mathf.Abs(delta.y)<combined.y)return false;
                }
                return true;
            }
            for(int attempt=0;attempt<64;attempt++)
            {
                var angle=(-150f+index*95f+attempt*137.508f)*Mathf.Deg2Rad;
                var radius=.95f+(attempt/8)*.45f;
                var point=from+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                if(Clear(point))return point;
            }
            if(!validOrigin&&target!=null)
                for(int attempt=0;attempt<32;attempt++)
                {
                    var angle=(index*95f+attempt*137.508f)*Mathf.Deg2Rad;
                    var point=laneOrigin+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(.95f+(attempt/8)*.45f);
                    if(Clear(point))return point;
                }
            // Crowded narrow passages retain the reward on an accessible existing anchor.
            if(target!=null&&Clear(target.transform.position))return target.transform.position;
            return from;
        }

        public bool IsSelected
        {
            get
            {
                if(!IsSettled||player==null)return false;
                var distance=((Vector2)player.transform.position-LandingPosition).sqrMagnitude;
                if(distance>1.35f*1.35f)return false;
                foreach(var other in active)
                {
                    if(other==null||other==this||!other.IsSettled||other.player!=player)continue;
                    var d=((Vector2)player.transform.position-other.LandingPosition).sqrMagnitude;
                    if(d<distance-.0001f||Mathf.Abs(d-distance)<.0001f&&other.identity<identity)return false;
                }
                return true;
            }
        }
        void Update()
        {
            if(Time.timeScale<=0)return;
            if(player==null){Destroy(gameObject);return;}
            var previousAge=age;age+=Time.deltaTime;
            if(age<delay)return;
            if(!itemRenderer.enabled)
            {
                itemRenderer.enabled=true;groundMarker.enabled=true;
                RewardPresentation.Reveal(origin+Vector2.up*.35f,Rarity,1001-Mathf.RoundToInt(origin.y*100),false);
                if((int)Rarity>=(int)WeaponRarity.Rare)SigFx.Play("LootBeam",origin,0,-1,true,false,.7f);
            }
            var progress=Mathf.Clamp01((age-delay)/duration);
            var ease=progress*progress*(3-2*progress);
            transform.position=Vector2.Lerp(origin,LandingPosition,ease);
            var lift=IsSettled?.20f+Mathf.Sin((age-duration-delay)*2.4f)*.025f:.20f+Mathf.Sin(progress*Mathf.PI)*.65f;
            itemSpin.localPosition=Vector3.up*lift;
            depth=1000-Mathf.RoundToInt(transform.position.y*100);
            itemRenderer.sortingOrder=depth+2;groundMarker.sortingOrder=depth-1;
            if(previousAge<delay+duration&&IsSettled)
                NativeCombatEffect.Create("RewardRing",LandingPosition,0,.65f,.24f,RewardPresentation.Color(Rarity),depth);
            if(!IsSettled)return;
            if((int)Rarity>=(int)WeaponRarity.Epic&&age>=nextGlint&&Vector2.Distance(player.transform.position,LandingPosition)<12)
            {
                NativeCombatEffect.Create("RewardSpark",ItemVisualCenter+Vector3.up*.14f,0,.38f,.5f,RewardPresentation.Color(Rarity),depth+3);
                nextGlint=age+(Rarity==WeaponRarity.Legendary?2.5f:3.4f);
            }
            if(IsSelected&&claimedInteractionFrame!=Time.frameCount&&settings.GetKeyDown(GameAction.Interact))
            {
                claimedInteractionFrame=Time.frameCount;
                if(collect!=null&&collect())
                {
                    RequiresStageDecision=false;RewardPresentation.Collect(ItemVisualCenter,Rarity,depth+3);
                    active.Remove(this);Destroy(gameObject);
                }
            }
        }
        void OnGUI()
        {
            if(Time.timeScale<=0||!IsSelected||Camera.main==null)return;
            if(promptStyle==null)promptStyle=PixelUiSkin.CreateWorldPromptStyle(RewardPresentation.Color(Rarity),12,false);
            var screen=Camera.main.WorldToScreenPoint(ItemVisualCenter+Vector3.up*(placementSize.y*.5f));if(screen.z<=0)return;
            var binding=settings.GetBinding(GameAction.Interact);
            var key=binding.primary!=KeyCode.None?binding.primary:binding.alternate;
            var text=GameSettingsService.GetKeyName(key)+" 획득하기";
            var width=promptStyle.CalcSize(new GUIContent(text)).x+8;
            GUI.Box(new Rect(Mathf.Clamp(screen.x-width*.5f,8,Screen.width-width-8),Mathf.Max(8,Screen.height-screen.y-32),width,26),text,promptStyle);
        }
        void OnDestroy(){active.Remove(this);}
    }
}
