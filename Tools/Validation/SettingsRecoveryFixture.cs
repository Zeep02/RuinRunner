using System;
using System.IO;
using UnityEngine;
using RuinRunner;

public static class SettingsRecoveryFixture
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception("[Settings Recovery] FAIL "+name);checks++;Debug.Log("[Settings Recovery] CHECK "+name);}
    public static string Main()
    {
        var root="F:/codex/LoglikeCap/Logs/Validation/2026-09-16-settings/"+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(root);var path=root+"/settings.json";
        var settings=new GameSettingsService(path,false);
        settings.SetBinding(GameAction.Inventory,KeyCode.K,false);
        settings.ChangeVolume(AudioChannel.Music,-.2f);
        var savedMusic=settings.GetVolume(AudioChannel.Music);
        settings.SetBinding(GameAction.Reward6,KeyCode.L,false);
        var reloaded=new GameSettingsService(path,false);
        Check(reloaded.GetBinding(GameAction.Inventory).primary==KeyCode.K && reloaded.GetBinding(GameAction.Reward6).primary==KeyCode.L,"custom old and appended keys survive reload");
        Check(savedMusic<.65f && Mathf.Approximately(reloaded.GetVolume(AudioChannel.Music),savedMusic),"channel value survives reload");
        Check(File.Exists(path+".bak") && !File.Exists(path+".tmp"),"atomic replacement leaves backup and no temporary");
        File.WriteAllText(path,"{ truncated");
        var backup=File.ReadAllText(path+".bak");
        var recovered=new GameSettingsService(path,false);
        Check(recovered.GetBinding(GameAction.Inventory).primary==KeyCode.K && Mathf.Approximately(recovered.GetVolume(AudioChannel.Music),savedMusic),"corrupt primary recovers last complete backup");
        recovered.SetBinding(GameAction.Reward6,KeyCode.L,false);
        Check(File.ReadAllText(path+".bak")==backup,"repair does not overwrite good backup with corrupt primary");
        var again=new GameSettingsService(path,false);
        Check(again.GetBinding(GameAction.Reward6).primary==KeyCode.L,"repaired primary reloads");
        File.WriteAllText(path,"broken");File.WriteAllText(path+".bak","broken");
        var defaults=new GameSettingsService(path,false);
        Check(defaults.GetBinding(GameAction.Dash).primary==KeyCode.Space && defaults.MasterVolume==.8f,"both corrupt files recover usable defaults");
        File.WriteAllText(path,"{\"version\":6,\"masterVolume\":9,\"musicVolume\":-3,\"bindings\":[null,{\"action\":7,\"primary\":107},{\"action\":7,\"primary\":108},{\"action\":999,\"primary\":999},{\"action\":0,\"primary\":52}]}");
        var normalized=new GameSettingsService(path,false);
        Check(normalized.MasterVolume==1 && normalized.GetVolume(AudioChannel.Music)==0,"out of range volumes clamp");
        Check(normalized.GetBinding(GameAction.Inventory).primary==KeyCode.K,"null unknown and duplicate bindings normalize preserving first custom key");
        Check(normalized.GetBinding(GameAction.MoveLeft).primary==KeyCode.Alpha4 && normalized.GetBinding(GameAction.Reward4).primary==KeyCode.None,"new reward default does not steal an existing custom key");
        for(var index=0;index<6;index++)Check(normalized.GetBinding(GameSettingsService.AccessoryChoiceAction(index))!=null,"six choices have bindable actions "+index);
        normalized.ResetBindings();
        Check(normalized.GetBinding(GameAction.Reward4).primary==KeyCode.Alpha4 && normalized.GetBinding(GameAction.Reward6).primary==KeyCode.Alpha6,"reset restores six numbered choice keys");
        var result="[Settings Recovery] PASS "+checks+" isolated persistence checks: "+root;
        File.WriteAllText(root+"/result.txt",result);Debug.Log(result);return result;
    }
}
