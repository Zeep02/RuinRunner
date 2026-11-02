using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using RuinRunner.Backend;
using UnityEngine;

namespace RuinRunner
{
    internal static class CatalogDropRules
    {
        internal static void Validate(CatalogItem item)
        {if(item.dropWeight<0||item.dropWeight>1000000||item.minStage<0||item.maxStage>2||item.minStage>item.maxStage)throw new ArgumentException("Invalid catalog drop rules "+item.id);}
        internal static float Weight(int weight,WeaponRarity rarity,float luck)
            =>weight*(1f+Mathf.Clamp(luck,0,50)*(int)rarity*.0125f);
    }
    public static partial class WeaponLootTable
    {
        public static string PinnedCatalogVersion {get;private set;}
        static readonly Dictionary<string,int> dropWeights=new Dictionary<string,int>();
        static readonly Dictionary<string,Vector2Int> dropStages=new Dictionary<string,Vector2Int>();
        static int dropStage;
        public static void SetDropStage(int stage){if(stage<0||stage>2)throw new ArgumentOutOfRangeException(nameof(stage));dropStage=stage;}
        static List<WeaponDefinition> EligibleWeapons(WeaponRarity? rarity=null)
        {
            var choices=new List<WeaponDefinition>();foreach(var item in Weapons)
                if((!rarity.HasValue||item.Rarity==rarity.Value)&&(!dropStages.TryGetValue(item.Id,out var range)||dropStage>=range.x&&dropStage<=range.y)&&(!dropWeights.TryGetValue(item.Id,out var weight)||weight>0))choices.Add(item);
            return choices;
        }
        public static WeaponDefinition RollOfRarity(WeaponRarity rarity)
        {var choices=EligibleWeapons(rarity);if(choices.Count==0)throw new InvalidOperationException("No eligible weapon at requested rarity "+rarity);return WeightedWeapon(choices);}
        static float Number(JObject stats,string key,float fallback,float minimum,float maximum)
        {float value=stats?[key]?.Value<float>()??fallback;if(float.IsNaN(value)||float.IsInfinity(value)||value<minimum||value>maximum)throw new ArgumentException("Invalid weapon catalog field: "+key);return value;}
        public static void BindPinnedCatalog(ContentCatalog catalog)
        {
            if(catalog==null||string.IsNullOrWhiteSpace(catalog.version)||catalog.items==null)throw new ArgumentException("Missing pinned weapon catalog");
            var next=new List<WeaponDefinition>();var weights=new Dictionary<string,int>();var stages=new Dictionary<string,Vector2Int>();
            foreach(var item in catalog.items)
            {
                if(item.kind!="weapon")continue;
                var old=Find(item.id);if(weights.ContainsKey(item.id)||item.rarity<0||item.rarity>4||item.dropWeight<0)throw new ArgumentException("Invalid weapon catalog entry "+item.id);
                CatalogDropRules.Validate(item);stages.Add(item.id,new Vector2Int(item.minStage,item.maxStage));
                var s=item.stats;
                var d=new WeaponDefinition(old.Id,string.IsNullOrWhiteSpace(item.name)?old.Name:item.name,(WeaponRarity)item.rarity,
                    Number(s,"attackPower",old.BaseAttackPower,.1f,1000),Number(s,"attackDelayMultiplier",old.BaseAttackDelayMultiplier,.1f,5),old.Color,old.AttackType,
                    (int)Number(s,"magazineSize",old.BaseMagazineSize,0,100),Number(s,"reloadSeconds",old.BaseReloadSeconds,0,10),Number(s,"projectileSpeed",old.BaseProjectileSpeed,1,80),old.ProjectileSize,
                    (int)Number(s,"projectilePierce",old.ProjectilePierce,1,16),old.SpecialType,old.SpecialManaCost,old.SpecialDescription,old.SetType,old.Family,old.ComboProfileId,
                    Number(s,"heavyDamageMultiplier",old.BaseHeavyDamageMultiplier,1,4),Number(s,"guardDamageReduction",old.BaseGuardDamageReduction,0,.8f));
                d.CatalogSpecialMana=Number(s,"specialManaCost",old.SpecialManaCost,0,100);
                d.CatalogSpecialCooldown=Number(s,"specialCooldown",WeaponActionCatalog.Get(old).Cooldown,.25f,15);
                next.Add(d);weights.Add(d.Id,item.dropWeight);
            }
            if(next.Count!=Weapons.Length)throw new ArgumentException("Pinned catalog must contain every known weapon; unknown mechanics cannot be invented by numeric data");
            for(int stage=0;stage<3;stage++)if(!next.Exists(w=>weights[w.Id]>0&&stage>=stages[w.Id].x&&stage<=stages[w.Id].y))throw new ArgumentException("No eligible catalog weapon for stage "+stage);
            Weapons=next.ToArray();dropWeights.Clear();foreach(var row in weights)dropWeights.Add(row.Key,row.Value);dropStages.Clear();foreach(var row in stages)dropStages.Add(row.Key,row.Value);PinnedCatalogVersion=catalog.version;
        }
        static WeaponDefinition WeightedWeapon(List<WeaponDefinition> choices,float luck=0)
        {
            float total=0;foreach(var item in choices)total+=CatalogDropRules.Weight(dropWeights.TryGetValue(item.Id,out var w)?w:100,item.Rarity,luck);
            if(total<=0)throw new InvalidOperationException("No eligible weapon in rolled rarity");
            float roll=UnityEngine.Random.value*total;foreach(var item in choices){roll-=CatalogDropRules.Weight(dropWeights.TryGetValue(item.Id,out var w)?w:100,item.Rarity,luck);if(roll<0)return item;}return choices[choices.Count-1];
        }
    }
    public static partial class AccessoryLootTable
    {
        public static string PinnedCatalogVersion {get;private set;}
        static readonly Dictionary<string,int> dropWeights=new Dictionary<string,int>();
        static readonly Dictionary<string,Vector2Int> dropStages=new Dictionary<string,Vector2Int>();
        static int dropStage;
        public static void SetDropStage(int stage){if(stage<0||stage>2)throw new ArgumentOutOfRangeException(nameof(stage));dropStage=stage;}
        static List<AccessoryDefinition> EligibleAccessories(WeaponRarity? rarity=null)
        {
            var choices=new List<AccessoryDefinition>();foreach(var item in Accessories)
                if((!rarity.HasValue||item.Rarity==rarity.Value)&&(!dropStages.TryGetValue(item.Id,out var range)||dropStage>=range.x&&dropStage<=range.y)&&(!dropWeights.TryGetValue(item.Id,out var weight)||weight>0))choices.Add(item);
            return choices;
        }
        public static AccessoryDefinition RollOfRarity(WeaponRarity rarity)
        {var choices=EligibleAccessories(rarity);if(choices.Count==0)throw new InvalidOperationException("No eligible accessory at requested rarity "+rarity);return WeightedAccessory(choices);}
        public static void BindPinnedCatalog(ContentCatalog catalog)
        {
            if(catalog?.items==null||string.IsNullOrWhiteSpace(catalog.version))throw new ArgumentException("Missing pinned accessory catalog");
            var setDefinitions=ItemSetCatalog.Prepare(catalog);
            var next=new List<AccessoryDefinition>();var weights=new Dictionary<string,int>();var stages=new Dictionary<string,Vector2Int>();
            foreach(var item in catalog.items)
            {
                if(item.kind!="accessory")continue;
                var old=Find(item.id);if(old==null||weights.ContainsKey(item.id)||item.rarity<0||item.rarity>4||item.dropWeight<0)throw new ArgumentException("Unknown or invalid accessory catalog entry "+item.id);
                CatalogDropRules.Validate(item);stages.Add(item.id,new Vector2Int(item.minStage,item.maxStage));
                var tuning=AccessoryTuning.Parse(item.id,item.stats,catalog.accessoryTuningVersion);
                next.Add(new AccessoryDefinition(old.Id,old.Type,string.IsNullOrWhiteSpace(item.name)?old.Name:item.name,string.IsNullOrWhiteSpace(item.description)?old.Description:item.description,(WeaponRarity)item.rarity,old.SetType,old.Color,tuning));weights.Add(item.id,item.dropWeight);
            }
            if(next.Count!=Accessories.Length)throw new ArgumentException("Pinned catalog must contain every known accessory");
            for(int stage=0;stage<3;stage++)if(!next.Exists(a=>weights[a.Id]>0&&stage>=stages[a.Id].x&&stage<=stages[a.Id].y))throw new ArgumentException("No eligible catalog accessory for stage "+stage);
            Accessories=next.ToArray();ItemSetCatalog.Commit(setDefinitions);dropWeights.Clear();foreach(var row in weights)dropWeights.Add(row.Key,row.Value);dropStages.Clear();foreach(var row in stages)dropStages.Add(row.Key,row.Value);PinnedCatalogVersion=catalog.version;
        }
        static AccessoryDefinition WeightedAccessory(List<AccessoryDefinition> choices,float luck=0)
        {
            float total=0;foreach(var item in choices)total+=CatalogDropRules.Weight(dropWeights.TryGetValue(item.Id,out var w)?w:100,item.Rarity,luck);
            if(total<=0)throw new InvalidOperationException("No eligible accessory in rolled rarity");
            float roll=UnityEngine.Random.value*total;foreach(var item in choices){roll-=CatalogDropRules.Weight(dropWeights.TryGetValue(item.Id,out var w)?w:100,item.Rarity,luck);if(roll<0)return item;}return choices[choices.Count-1];
        }
    }
}
