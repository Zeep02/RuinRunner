using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using RuinRunner;

// Disk-only fixture. Constructor path confines all writes to a new evidence folder.
public static class SaveCompatibilityFixture
{
    static int checks;
    static void Require(bool value,string message){if(!value)throw new Exception("[Save Compatibility] FAIL "+message);checks++;Debug.Log("[Save Compatibility] OK "+message);}
    public static string Main()
    {
        var root="F:/codex/LoglikeCap/Logs/Validation/2026-09-16-save-compatibility";
        if(Directory.Exists(root))throw new Exception("Keep previous evidence; choose a new folder");
        Directory.CreateDirectory(root);var saves=Path.Combine(root,"Saves");Directory.CreateDirectory(saves);
        File.WriteAllText(Path.Combine(saves,"save1.json"),"{\"version\":1,\"slotNumber\":1,\"totalRuns\":4,\"victories\":2,\"totalKills\":54,\"legacyShards\":27,\"upgrades\":[{\"type\":"+(int)MetaUpgradeType.VitalLegacy+",\"rank\":1}]}");
        var slots=new SaveSlotService(saves);Require(slots.IsValidationProfile,"explicit isolated directory");
        Require(slots.OpenSlot(1),"legacy slot opens");
        Require(slots.ActiveData.victories==2 && slots.ActiveData.campaignVictories==0 && slots.ActiveData.bestCampaignStage==0,"old one-stage victories preserved, not claimed as campaign clears");
        var meta=new MetaProgressionService(slots);Require(meta.LegacyShards==27 && meta.GetRank(MetaUpgradeType.VitalLegacy)==1,"legacy shards and rank preserved");
        var ranks=new Dictionary<MetaUpgradeType,int>{{MetaUpgradeType.VitalLegacy,1}};
        slots.SaveRunResult(35,ranks,60,12,true,3);
        Require(slots.ActiveData.totalRuns==5 && slots.ActiveData.victories==3 && slots.ActiveData.campaignVictories==1 && slots.ActiveData.bestCampaignStage==3,"new campaign result extends old counters");
        var before=JsonUtility.FromJson<SaveSlotData>(File.ReadAllText(Path.Combine(saves,"save1.json.bak")));
        Require(before.totalRuns==4 && before.legacyShards==27,"atomic backup contains whole previous snapshot");
        slots.OpenSlot(2);slots.SaveProgression(11,ranks);slots.OpenSlot(3);slots.SaveProgression(19,ranks);slots.CloseActiveSlot();
        slots=new SaveSlotService(saves);
        Require(slots.GetSummary(1).LegacyShards==35 && slots.GetSummary(2).LegacyShards==11 && slots.GetSummary(3).LegacyShards==19,"three independent slots reload");
        slots.OpenSlot(1);slots.CloseActiveSlot();File.WriteAllText(Path.Combine(saves,"save1.json"),"{ truncated");
        slots=new SaveSlotService(saves);slots.OpenSlot(1);
        Require(slots.ActiveData.legacyShards==35 && slots.ActiveData.campaignVictories==1,"malformed primary recovers backup");
        File.WriteAllText(Path.Combine(saves,"save2.json"),"{\"version\":1,\"slotNumber\":2,\"legacyShards\":-9,\"totalRuns\":-1,\"upgrades\":null}");
        slots.OpenSlot(2);slots.SaveProgression(7,ranks);
        Require(slots.ActiveData.totalRuns==0 && slots.ActiveData.upgrades!=null && slots.ActiveData.legacyShards==7,"null/negative fields normalize without progression exception");
        File.WriteAllText(Path.Combine(saves,"save3.json"),"broken");File.WriteAllText(Path.Combine(saves,"save3.json.bak"),"broken");
        slots.OpenSlot(3);Require(slots.ActiveData.legacyShards==3 && slots.ActiveData.totalRuns==0,"both corrupt files fall back to fresh isolated defaults");
        Require(!File.Exists(Path.Combine(saves,"save1.json.tmp")) && !File.Exists(Path.Combine(saves,"save2.json.tmp")),"no temporary save remains");

        var settingsPath=Path.Combine(root,"settings.json");
        File.WriteAllText(settingsPath,"{\"version\":1,\"masterVolume\":0.3,\"bindings\":[{\"action\":"+(int)GameAction.Inventory+",\"primary\":"+(int)KeyCode.K+",\"alternate\":0}]}");
        var settings=new GameSettingsService(settingsPath,false);
        Require(Mathf.Approximately(settings.MasterVolume,.3f) && settings.GetBinding(GameAction.Inventory).primary==KeyCode.K,"legacy settings preserve custom key and volume");
        Require(settings.GetBinding(GameAction.Dash).primary==KeyCode.Space && settings.GetBinding(GameAction.Guard).primary==KeyCode.Mouse1 && settings.GetBinding(GameAction.Jump).primary==KeyCode.None,"legacy combat keys migrate to current controls");
        settings.SetBinding(GameAction.Attack,KeyCode.J,false);settings.ChangeVolume(AudioChannel.Music,.2f);
        var reloaded=new GameSettingsService(settingsPath,false);
        Require(reloaded.GetBinding(GameAction.Attack).primary==KeyCode.J && Mathf.Approximately(reloaded.GetVolume(AudioChannel.Music),settings.GetVolume(AudioChannel.Music)),"rebind and channel settings persist");
        File.WriteAllText(settingsPath,"corrupt");var defaults=new GameSettingsService(settingsPath,false);
        Require(defaults.GetBinding(GameAction.Dash).primary==KeyCode.Space,"corrupt settings retain usable default controls");
        var result="[Save Compatibility] PASS "+checks+" checks. Isolated disk fixture; relaunch in a Windows build still required.";
        File.WriteAllText(Path.Combine(root,"result.txt"),result);Debug.Log(result);return result;
    }
}
