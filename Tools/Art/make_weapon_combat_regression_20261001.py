from pathlib import Path
root=Path(__file__).resolve().parents[2]
s=(root/'Tools/Validation/EquipmentCombatFixture.cs').read_text(encoding='utf-8-sig')
s=s.replace('public static class EquipmentCombatFixture','public static class WeaponCombatRegression20261001')
s=s.replace('    static int checks;','    static int checks;\n    static string folder;\n    static readonly List<string> assertions=new List<string>();')
s=s.replace('checks++;Debug.Log(', 'checks++;assertions.Add("PASS "+name);Debug.Log(')
s=s.replace('        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();','        folder="F:/codex/LoglikeCap/Logs/Validation/2026-10-01-weapon-identity/catalog-"+DateTime.Now.ToString("HHmmss");System.IO.Directory.CreateDirectory(folder);assertions.Clear();\n        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();')
s=s.replace('        game.CameraFollow.StartCoroutine(Run(game));','        game.enabled=false;game.CameraFollow.StartCoroutine(Guard(Run(game),game));')
s=s.replace('        return "Isolated catalog combat checks started; no campaign evidence";','        return folder;')
s=s.replace('    static IEnumerator Run(GameController game)',r'''    static IEnumerator Guard(IEnumerator run,GameController game)
    {
        string error=null;
        while(true){object value=null;bool next;try{next=run.MoveNext();if(next)value=run.Current;}catch(Exception e){error=e.ToString();break;}if(!next)break;yield return value;}
        input.GetMethod("ReleaseAll").Invoke(null,null);game.ReturnToMainMenu();game.enabled=true;
        System.IO.File.WriteAllText(folder+"/result.txt",(error==null?"PASS supplied full equipment catalog / actual GameAction combat":"FAIL "+error)+"\n"+string.Join("\n",assertions));
    }
    static IEnumerator Run(GameController game)''')
(root/'Tools/Validation/WeaponCombatRegression20261001.cs').write_text(s,encoding='utf-8')
print('Full catalog combat regression with preserved assertion results')
