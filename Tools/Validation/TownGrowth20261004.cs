using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RuinRunner;

public static class TownGrowth20261004
{
    static readonly List<string> checks=new List<string>();
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add("PASS "+label);}
    public static string Main()
    {
        checks.Clear();string folder="F:/codex/LoglikeCap/Logs/Validation/2026-10-04-town-growth/rules-"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
        try{Run();File.WriteAllText(folder+"/result.txt","PASS supplied rule-state checks only; not normal campaign, UI, server or build proof\n"+string.Join("\n",checks),System.Text.Encoding.UTF8);}
        catch(Exception e){File.WriteAllText(folder+"/result.txt","FAIL "+e+"\n"+string.Join("\n",checks),System.Text.Encoding.UTF8);throw;}
        return folder;
    }
    static void Run()
    {
        Check(TalentCatalog.All.Length==12,"12 behavioral talent IDs");
        Check(TalentLoadout.CanSelect(new[]{"combo_memory","turning_dash","chest_intent"}),"three different branches accepted");
        Check(!TalentLoadout.CanSelect(new[]{"combo_memory","stored_guard"}),"same branch cannot stack");
        Check(!TalentLoadout.CanSelect(new[]{"combo_memory","turning_dash","survey_next","chest_intent"}),"four active talents rejected");
        Check(!TalentLoadout.CanSelect(new[]{"unknown"}),"unknown content ID rejected");
        var selected=new List<string>{"combo_memory","survey_next"};var snapshot=new TalentLoadout(selected);selected.Clear();Check(snapshot.Has(TalentId.ComboMemory),"departure loadout is an independent snapshot");
        var bag=new InventorySystem();bag.AddWeapon(WeaponLootTable.Find("rust_sword"));bag.AddWeapon(WeaponLootTable.Find("rust_sword"));
        var first=bag.QuickSlots[0].Item;var second=bag.QuickSlots[1].Item;
        Check(!bag.AwardMaterials(100,"old")&&!bag.TryUpgrade(first.Identity,0)&&bag.UpgradeMaterials==0,"old material and numerical upgrade actions disabled");
        Check(first.Weapon.AttackPower==first.Definition.AttackPower&&!first.DisplayName.Contains("+"),"base weapon has no mandatory plus-level bonus or label");
        Check(bag.TryMutate(first.Identity,EquipmentMutation.LongFinisher)&&second.Mutation==EquipmentMutation.None,"mutation only affects its owned copy");
        Check(!bag.TryMutate(first.Identity,EquipmentMutation.WideFinisher),"mutually exclusive mutation cannot be reapplied");
        bag.UnequipQuickSlot(0,0);bag.AssignToQuickSlot(0,2);Check(bag.QuickSlots[2].Item==first&&first.Mutation==EquipmentMutation.LongFinisher,"same mutation follows inventory/slot movement");
        Check(bag.TrySwear(first.Identity,EquipmentOath.FinisherHunt)&&!bag.TrySwear(second.Identity,EquipmentOath.CounterDiscipline),"only one incomplete oath active");
        var tracker=new EquipmentOathTracker();
        tracker.Apply(bag,new CombatGrowthEvent{Kind=CombatGrowthKind.FinisherHit,WeaponIdentity=first.Identity,TargetIdentity=100,ActionIdentity=1,IsTraining=true});
        tracker.Apply(bag,new CombatGrowthEvent{Kind=CombatGrowthKind.FinisherHit,WeaponIdentity=first.Identity,TargetIdentity=100,ActionIdentity=1,IsRecursive=true});
        Check(first.OathProgress==0,"training and recursive effects cannot advance vows");
        for(int i=0;i<5;i++)tracker.Apply(bag,new CombatGrowthEvent{Kind=CombatGrowthKind.FinisherHit,WeaponIdentity=first.Identity,TargetIdentity=100+i,ActionIdentity=i+1});
        Check(first.Awakened&&first.OathProgress==5,"finisher vow completes on five different targets");
        tracker.Apply(bag,new CombatGrowthEvent{Kind=CombatGrowthKind.FinisherHit,WeaponIdentity=first.Identity,TargetIdentity=100,ActionIdentity=1});
        Check(first.OathProgress==5,"duplicate impact cannot repeat awakening");
        Check(bag.TrySwear(second.Identity,EquipmentOath.CounterDiscipline),"new vow may begin after completed one");
        tracker.Apply(bag,new CombatGrowthEvent{Kind=CombatGrowthKind.Guard,WeaponIdentity=second.Identity,Time=1});
        tracker.Apply(bag,new CombatGrowthEvent{Kind=CombatGrowthKind.DirectHit,WeaponIdentity=second.Identity,TargetIdentity=1,ActionIdentity=99,Time=1.1f});
        Check(second.OathProgress==0,"ordinary hit after guarding is not a counter");
        for(int i=0;i<3;i++)tracker.Apply(bag,new CombatGrowthEvent{Kind=CombatGrowthKind.DirectHit,WeaponIdentity=second.Identity,TargetIdentity=2+i,ActionIdentity=100+i,Time=1.2f,IsCounter=true});
        Check(second.Awakened,"counter vow completes from direct counters");
        bag=new InventorySystem();bag.AddWeapon(WeaponLootTable.Find("scrap_pistol"));bag.AddWeapon(WeaponLootTable.Find("rust_sword"));first=bag.QuickSlots[0].Item;second=bag.QuickSlots[1].Item;
        first.TryConsumeAmmo();first.StartReload();float reload=first.ReloadRemaining;int ammo=first.Ammo;
        Check(first.LoadSingleRound()&&first.Ammo==ammo+1&&first.ReloadRemaining==reload,"cross-chamber round leaves current reload timing intact");
        var chambered=new OwnedWeapon(WeaponLootTable.Find("scrap_pistol"),0);chambered.StartReload();float chamberTime=chambered.ReloadRemaining;
        Check(chambered.LoadSingleRound()&&chambered.TryConsumeAmmo()&&chambered.Ammo==0&&chambered.ReloadRemaining==chamberTime,"cross-chamber granted round fires once during empty reload without resetting timer");
        Check(!chambered.TryConsumeAmmo()&&chambered.ReloadRemaining==chamberTime,"cross-chamber cannot fire another ungranted round");
        Check(bag.TrySwear(first.Identity,EquipmentOath.CrossWeaponChain),"cross weapon vow requires another copy");tracker=new EquipmentOathTracker();
        for(int i=0;i<3;i++){tracker.Apply(bag,new CombatGrowthEvent{Kind=CombatGrowthKind.DirectHit,WeaponIdentity=second.Identity,TargetIdentity=20+i,ActionIdentity=200+i,Time=1});tracker.Apply(bag,new CombatGrowthEvent{Kind=CombatGrowthKind.DirectHit,WeaponIdentity=first.Identity,TargetIdentity=20+i,ActionIdentity=300+i,Time=2});}
        Check(first.Awakened,"cross weapon vow tracks ordered hits on three distinct enemies");
        bag=new InventorySystem();bag.AddWeapon(WeaponLootTable.Find("scrap_pistol"));first=bag.ActiveSlot.Item;Check(bag.TrySwear(first.Identity,EquipmentOath.SteadyVolley),"volley vow accepted for sufficient magazine");tracker=new EquipmentOathTracker();
        for(int shot=0;shot<3;shot++)tracker.Apply(bag,new CombatGrowthEvent{Kind=CombatGrowthKind.DirectHit,WeaponIdentity=first.Identity,TargetIdentity=33,ActionIdentity=700+shot});
        tracker.Apply(bag,new CombatGrowthEvent{Kind=CombatGrowthKind.ReloadCompleted,WeaponIdentity=first.Identity,ActionIdentity=750,ReloadStartedEmpty=true});
        Check(first.OathProgress==0,"ammo-free special attacks cannot advance magazine vow");
        for(int shot=0;shot<3;shot++)tracker.Apply(bag,new CombatGrowthEvent{Kind=CombatGrowthKind.DirectHit,WeaponIdentity=first.Identity,TargetIdentity=33,ActionIdentity=800+shot,IsMagazineHit=true});
        tracker.Apply(bag,new CombatGrowthEvent{Kind=CombatGrowthKind.ReloadCompleted,WeaponIdentity=first.Identity,ActionIdentity=850,ReloadStartedEmpty=false});
        tracker.Apply(bag,new CombatGrowthEvent{Kind=CombatGrowthKind.ReloadCompleted,WeaponIdentity=first.Identity,ActionIdentity=851,ReloadStartedEmpty=true});
        Check(first.OathProgress==0,"partial reload resets hits and cannot advance volley vow");
        for(int reloadIndex=0;reloadIndex<3;reloadIndex++)
        {for(int shot=0;shot<3;shot++)tracker.Apply(bag,new CombatGrowthEvent{Kind=CombatGrowthKind.DirectHit,WeaponIdentity=first.Identity,TargetIdentity=33,ActionIdentity=reloadIndex*10+shot,IsMagazineHit=true});tracker.Apply(bag,new CombatGrowthEvent{Kind=CombatGrowthKind.ReloadCompleted,WeaponIdentity=first.Identity,ActionIdentity=1000+reloadIndex,ReloadStartedEmpty=true});}
        Check(first.Awakened,"volley vow completes after three qualifying empty reloads");
        bag=new InventorySystem();bag.AddWeapon(WeaponLootTable.Find("rust_sword"));
        Check(bag.TryAddConfirmed("mana_thread",InventoryEntryKind.Accessory,"server-copy-1")&&bag.TryAddConfirmed("mana_thread",InventoryEntryKind.Accessory,"server-copy-1")&&bag.OwnedAccessories.Count==1,"repeated receipt preserves a single server-owned copy");
        for(int i=1;i<InventorySystem.Capacity;i++)bag.AddAccessory(AccessoryLootTable.Find("mana_thread"));
        Check(!bag.HasRoomFor(InventoryEntryKind.Accessory)&&!bag.TryAddConfirmed("mana_thread",InventoryEntryKind.Accessory,"server-overflow"),"full bag rejects grant before changing ownership");
        Check(bag.HasRoomFor(InventoryEntryKind.Weapon),"free quick slot still accepts a weapon with a full bag");
        var item=bag.OwnedAccessories[0];Check(bag.TryRemoveConfirmed(item.Identity)&&bag.FindServerItem("server-copy-1")==null,"removal releases server-copy mapping");
        Check(!bag.TryRemoveConfirmed(bag.ActiveSlot.Item.Identity),"cannot discard last weapon and strand player");
    }
}
