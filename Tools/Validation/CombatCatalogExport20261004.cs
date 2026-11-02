using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using RuinRunner;

public static class CombatCatalogExport20261004
{
    public static string Main()
    {
        var items=new JArray();
        foreach(var w in WeaponLootTable.All)
        {
            var action=WeaponActionCatalog.Get(w);int price=18+(int)w.Rarity*15;
            items.Add(new JObject{{"id",w.Id},{"kind","weapon"},{"name",w.Name},{"description",w.SpecialDescription},{"rarity",(int)w.Rarity},{"price",price},{"sellPrice",Math.Max(1,price/4)},{"dropWeight",100},{"minStage",0},{"maxStage",2},
                {"stats",new JObject{{"attackPower",w.AttackPower},{"attackDelayMultiplier",w.AttackDelayMultiplier},{"magazineSize",w.MagazineSize},{"reloadSeconds",w.ReloadSeconds},{"projectileSpeed",w.ProjectileSpeed},{"projectilePierce",w.ProjectilePierce},{"heavyDamageMultiplier",w.HeavyDamageMultiplier},{"guardDamageReduction",w.GuardDamageReduction},{"specialManaCost",action.Mana},{"specialCooldown",action.Cooldown}}}});
        }
        foreach(var a in AccessoryLootTable.All)
        {int price=14+(int)a.Rarity*13;items.Add(new JObject{{"id",a.Id},{"kind","accessory"},{"name",a.Name},{"description",a.Description},{"rarity",(int)a.Rarity},{"price",price},{"sellPrice",Math.Max(1,price/4)},{"dropWeight",100},{"minStage",0},{"maxStage",2},{"stats",new JObject()}});}
        const string path="ArtDirection/Review-20261004/combat-expansion/catalog-items.initial-migration.json";
        Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,items.ToString(),new UTF8Encoding(false));return path+" : "+items.Count+" entries";
    }
}
