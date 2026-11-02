using System;
using System.IO;
using UnityEngine;
using RuinRunner;
public static class InventorySettingsMigration20261004
{
    public static string Main()
    {
        var folder="F:/codex/LoglikeCap/Logs/Validation/2026-10-04-normal-growth/settings-"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
        var path=folder+"/settings.json";
        File.WriteAllText(path,"{\"version\":6,\"bindings\":[{\"action\":6,\"primary\":120,\"alternate\":0},{\"action\":13,\"primary\":114,\"alternate\":0}]}");
        var settings=new GameSettingsService(path,false);
        if(settings.GetBinding(GameAction.Interact).primary!=KeyCode.X||settings.GetBinding(GameAction.Reload).primary!=KeyCode.R||settings.GetBinding(GameAction.Dismantle).primary!=KeyCode.None)
            throw new Exception("New action stole an existing custom key or changed serialized actions");
        settings.SetBinding(GameAction.Dismantle,KeyCode.F,false);
        var restored=new GameSettingsService(path,false);
        if(restored.GetBinding(GameAction.Interact).primary!=KeyCode.X||restored.GetBinding(GameAction.Reload).primary!=KeyCode.R||restored.GetBinding(GameAction.Dismantle).primary!=KeyCode.F)
            throw new Exception("Isolated save/reload changed original bindings");
        File.WriteAllText(folder+"/result.txt","PASS existing v6 numeric bindings retain X interaction/R reload; new Dismantle starts unbound when X is occupied; remap F persists without stealing keys. Isolated files only.");
        return folder;
    }
}
