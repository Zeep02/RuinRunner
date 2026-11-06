using System;
using System.Collections.Generic;
using RuinRunner.Backend;
using RuinRunner.Generation;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class GameController
    {
        public string PendingExchangeRecipe { get; private set; }
        readonly HashSet<string> usedServices=new HashSet<string>();
        public IReadOnlyList<PinnedExchangeRecipe> ExchangeRecipes { get; private set; }=Array.Empty<PinnedExchangeRecipe>();
        public string ExchangeCatalogVersion { get; private set; }
        void BindExchangeCatalog()
        {
            ExchangeRecipes=ExchangeCatalogBinding.Prepare(BackendCatalog);
            ExchangeCatalogVersion=BackendCatalog.version;PendingExchangeRecipe=null;
        }
        public PinnedExchangeRecipe FindExchangeRecipe(string id)
        {foreach(var recipe in ExchangeRecipes)if(recipe.Id==id)return recipe;return null;}
        void CreateFieldServices()
        {
            if(DungeonMap?.Graph==null)return;
            foreach(var r in DungeonMap.Graph.Regions)
            {
                FieldServiceKind kind;string label,art,animation=null;
                if(r.Role==RegionRole.Merchant){kind=FieldServiceKind.Merchant;label="필드 상인";art="External/TownQuesters/smith/00";animation="External/TownQuesters/smith";}
                else if(r.Service=="form-anvil"){kind=FieldServiceKind.ShapeAnvil;label="형상 모루";art="External/Cainos/Props/altar";}
                else if(r.Service=="oath-stone"){kind=FieldServiceKind.OathStone;label="서약석";art="External/Cainos/Props/statue";}
                else if(r.Service=="exchange-altar"){kind=FieldServiceKind.ExchangeAltar;label="교환 제단";art="External/Cainos/Props/altar";}
                else continue;
                var position=GeneratedStageUnityAdapter.Vector(r.ServicePosition);
                // Reserved module anchor is validated for navigation and outside every encounter.
                if(!StageNavigation.IsClear(DungeonMap.Terrain,position))throw new InvalidOperationException("Blocked service anchor: "+r.Id);
                var obj=FieldServiceObject.Create(DungeonMap.Root.transform,r.Id+":"+r.Service,kind,label,position,r.Index,art,animation);
                obj.Used=usedServices.Contains(obj.StableId);
                if(kind==FieldServiceKind.Merchant)SetUpMerchantGoods(r,position);
            }
        }
        // The wagon and the pack go where there is floor to spare on every side, so neither can narrow a way through the room.
        void SetUpMerchantGoods(GeneratedRegion region,Vector2 merchant)
        {
            var spots=new[]{new Vector2(-1.9f,.5f),new Vector2(1.9f,.5f),new Vector2(-2.3f,-.5f),new Vector2(2.3f,-.5f),new Vector2(-1.2f,1.5f),new Vector2(1.2f,1.5f),new Vector2(0f,1.8f)};
            var taken=new List<Vector2>();
            foreach(var clip in new[]{"PropWagon","PropBundle"})
                foreach(var spot in spots)
                {
                    var feet=merchant+spot;
                    if(!MerchantGoodsFit(region,clip,feet,taken))continue;
                    LivingProp.Create(DungeonMap.Root.transform,clip,feet,"Merchant "+clip);
                    taken.Add(feet);
                    break;
                }
        }
        bool MerchantGoodsFit(GeneratedRegion region,string clip,Vector2 feet,List<Vector2> taken)
        {
            var size=LivingProp.Footprint(clip);
            var center=feet+Vector2.up*(size.y*.5f-.1f);
            var clear=size*.5f+Vector2.one*.85f;
            for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)
                if(!DungeonMap.Terrain.IsOpen(center+new Vector2(clear.x*x*.5f,clear.y*y*.5f)))return false;
            if(Physics2D.OverlapBox(center,size+Vector2.one*.3f,0,1<<GameLayers.Ground)!=null)return false;
            foreach(var keep in new[]{region.PlayerPosition,region.RewardPosition,region.ExitPosition})
                if((feet-GeneratedStageUnityAdapter.Vector(keep)).sqrMagnitude<2.25f)return false;
            foreach(var other in taken)if((feet-other).sqrMagnitude<2.25f)return false;
            return true;
        }
        public void ApplyFieldMutation(EquipmentMutation mutation)
        {
            if(OpenService==null||OpenService.Kind!=FieldServiceKind.ShapeAnvil||OpenService.Used||EnemiesAlive>0||EconomyOperationPending||BackendHasPending)return;
            if(!Inventory.TryMutate(ServiceSelectedEquipment,mutation)){SessionMessage="이 무기에는 적용할 수 없습니다.";return;}
            UseService();Player.EquipWeapon(Inventory.EquippedWeapon);SessionMessage=EquipmentMutationRules.Name(mutation);
        }
        public bool CanBeginOath(OwnedWeapon item,EquipmentOath oath)
        {
            if(!EquipmentMutationRules.Compatible(item,oath,Inventory))return false;
            foreach(var owned in Inventory.AllWeapons())if(owned.Oath!=EquipmentOath.None&&!owned.Awakened)return false;
            if(CurrentStageIndex<CampaignStage.Count-1)return true;
            int remaining=0;
            for(int i=0;i<DungeonMap.RoomCount;i++)if(DungeonMap.Regions[i].Phase!=EncounterRegionPhase.Cleared&&DungeonMap.RequiresCombat(i))remaining+=i==CurrentRoom?EnemiesAlive:DungeonMap.GetRoom(i).enemies.Length;
            return oath==EquipmentOath.SteadyVolley?remaining>0:remaining>=EquipmentMutationRules.Required(oath);
        }
        public void AcceptEquipmentOath(EquipmentOath oath)
        {
            if(OpenService==null||OpenService.Kind!=FieldServiceKind.OathStone||OpenService.Used||EnemiesAlive>0||EconomyOperationPending||BackendHasPending)return;
            if(!CanBeginOath(Inventory.FindOwned(ServiceSelectedEquipment) as OwnedWeapon,oath)||!Inventory.TrySwear(ServiceSelectedEquipment,oath)){SessionMessage="진행 중인 서약 또는 호환 장비를 확인하세요.";return;}
            UseService();
        }
        void UseService(){usedServices.Add(OpenService.StableId);OpenService.Used=true;PrototypeAudio.Play(PrototypeSound.Upgrade,AudioChannel.Interface,.55f);}
        public void PreviewExchange(string id)
        {if(OpenService?.Kind==FieldServiceKind.ExchangeAltar&&FindExchangeRecipe(id)!=null)PendingExchangeRecipe=id;}
        public OwnedAccessory[] ExchangeIngredients(string id)
        {
            var recipe=FindExchangeRecipe(id);if(recipe==null)return Array.Empty<OwnedAccessory>();
            var list=new List<OwnedAccessory>();foreach(var required in recipe.InputIds)
            {foreach(var item in Inventory.OwnedAccessories)if(item.Definition.Id==required){list.Add(item);break;}}
            return list.Count==2?list.ToArray():Array.Empty<OwnedAccessory>();
        }
        public async void ConfirmExchange()
        {
            if(OpenService?.Kind!=FieldServiceKind.ExchangeAltar||OpenService.Used||EconomyOperationPending||BackendHasPending)return;
            var ingredients=ExchangeIngredients(PendingExchangeRecipe);if(ingredients.Length!=2){SessionMessage="필요한 장신구 두 개를 가져오세요.";return;}
            EconomyOperationPending=true;
            string operation=Guid.NewGuid().ToString();serviceByOperation[operation]=OpenService.StableId;
            try
            {
                var receipt=await TradeBackendAsync(new TradeRequest{operationId=operation,action="exchange",recipeId=PendingExchangeRecipe,
                    instanceIds=new[]{ingredients[0].ServerInstanceId,ingredients[1].ServerInstanceId},objectId=OpenService.StableId});
                if(receipt!=null){ApplyEconomyReceipt(receipt);PrototypeAudio.Play(PrototypeSound.Upgrade,AudioChannel.Interface,.55f);PendingExchangeRecipe=null;}
            }
            finally{EconomyOperationPending=false;}
        }
    }
}
