using System;
using System.Collections.Generic;
using RuinRunner.Backend;
using RuinRunner.Generation;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class GameController
    {
        public bool SupplyUsesMana { get; set; }
        public bool ChestPrefersAccessories { get; set; }
        public bool IsRewardReserved { get; private set; }
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
            }
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
            UseService();SessionMessage="서약을 새겼습니다.";
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
                if(receipt!=null){ApplyEconomyReceipt(receipt);PrototypeAudio.Play(PrototypeSound.Upgrade,AudioChannel.Interface,.55f);SessionMessage="유물이 깨어났습니다.";PendingExchangeRecipe=null;}
            }
            finally{EconomyOperationPending=false;}
        }
        public void ResolveReservedReward()
        {
            if(!IsRewardReserved||EnemiesAlive>0||IsPaused)return;
            IsRewardReserved=false;CurrentAccessoryChoices=RunContent.Choices(RunSeed,accessoryOfferIndex++,Player.Stats.Luck);IsChoosingAccessory=true;RefreshTimeScale();
        }
        public bool CanSurveyRegion(int region)
        {
            if(!ActiveTalents.Has(TalentId.SurveyNext)||DungeonMap?.Graph==null)return false;
            foreach(var edge in DungeonMap.Graph.Connections)
                if(edge.FromIndex==CurrentRoom&&edge.ToIndex==region||edge.ToIndex==CurrentRoom&&edge.FromIndex==region)return true;
            return false;
        }
        public string RegionPurpose(int region)
        {
            if(DungeonMap?.Graph==null)return "미지";
            switch(DungeonMap.Graph.Regions[region].Role)
            {case RegionRole.Boss:return "수호자";case RegionRole.Merchant:return "상인";case RegionRole.FieldUpgrade:return "유적 시설";case RegionRole.Reward:return "보급함";case RegionRole.Entrance:return "입구";default:return "전투";}
        }
        public HashSet<int> ReturnTrailConnections()
        {
            var result=new HashSet<int>();var graph=DungeonMap?.Graph;if(graph==null||!ActiveTalents.Has(TalentId.ReturnTrail))return result;
            var queue=new Queue<int>();var seen=new HashSet<int>{CurrentRoom};var parents=new Dictionary<int,int>();var used=new Dictionary<int,int>();queue.Enqueue(CurrentRoom);
            while(queue.Count>0)
            {
                int from=queue.Dequeue();if(from==DungeonMap.EntranceRoomIndex)break;
                for(int i=0;i<graph.Connections.Length;i++){var edge=graph.Connections[i];int to=edge.FromIndex==from?edge.ToIndex:edge.ToIndex==from?edge.FromIndex:-1;if(to<0||!DungeonMap.Regions[to].Visited||!seen.Add(to))continue;parents[to]=from;used[to]=i;queue.Enqueue(to);}
            }
            int at=DungeonMap.EntranceRoomIndex;while(parents.TryGetValue(at,out int from)){result.Add(used[at]);at=from;}
            return result;
        }
    }
}
