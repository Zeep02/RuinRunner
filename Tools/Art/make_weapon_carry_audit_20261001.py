"""Capture every equipped weapon in eight directions through actual runtime renderers."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/CrossCutPresentation20261001.cs').read_text(encoding='utf-8')
t=t[:t.index('    static IEnumerator Ready(')]
t=t.replace('CrossCutPresentation20261001','WeaponCarryAudit20261001').replace('cross-cut/presentation-','weapon-carry/audit-')
t=t.replace('using RuinRunner;','using RuinRunner;\nusing Newtonsoft.Json.Linq;')
t=t.replace('static string folder;', 'static readonly JArray poses=new JArray(); static string folder;')
t=t.replace('checks.Clear(); customSizes.Clear();','poses.Clear(); checks.Clear(); customSizes.Clear();')
t+=r'''    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();Require(Screen.width==1280&&Screen.height==720,"native eight-direction carry composition");
        foreach(var weapon in WeaponLootTable.All)
        {
            Equip(weapon.Id);yield return null;
            for(int d=0;d<8;d++)
            {
                StageValidationInput.BeginFrame();StageValidationInput.SetAim(new Vector2(Mathf.Sin(d*Mathf.PI/4),Mathf.Cos(d*Mathf.PI/4)));
                yield return new WaitForSeconds(.38f);yield return new WaitForEndOfFrame();
                var p=game.Player;var held=p.GetComponent<HeldWeaponView>();var a=p.GetComponent<PlayerAnimationController>();var pivot=p.transform.Find("HeldWeaponPivot");var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
                var phase=weapon.Id+"-view-"+d;Track(phase);Capture(phase);File.Copy(folder+"/"+phase+".png",folder+"/frames/frame-"+sample.ToString("D6")+".png");recording.AppendLine(sample+++","+Time.realtimeSinceStartupAsDouble.ToString("F6",CultureInfo.InvariantCulture)+","+phase);
                Require(held.IsVisible&&held.CurrentWeaponId==weapon.Id,"equipped native weapon "+phase);
                Require(Vector2.Distance(pivot.localPosition,a.HandLocalPosition)<.001f,"actual authored hand/grip registration "+phase);
                poses.Add(new JObject{{"phase",phase},{"weapon",weapon.Id},{"family",weapon.Family.ToString()},{"direction",d},{"bodyDirection",a.FacingDirection},{"pose",a.PoseName},
                    {"bodySource",AssetDatabase.GetAssetPath(body.sprite)},{"weaponSource",AssetDatabase.GetAssetPath(held.RendererSprite)},
                    {"bodyOrder",body.sortingOrder},{"weaponOrder",pivot.GetComponentInChildren<SpriteRenderer>().sortingOrder},
                    {"handNativePixels",new JArray(a.HandLocalPosition.x*32,a.HandLocalPosition.y*32)},{"weaponRotation",pivot.eulerAngles.z},
                    {"grip",new JArray(held.BladePoint(0).x,held.BladePoint(0).y)},{"actor",new JArray(p.transform.position.x,p.transform.position.y)}});
                File.WriteAllText(folder+"/carry-poses.json",poses.ToString(),new UTF8Encoding(false));File.WriteAllText(folder+"/body-telemetry.csv",telemetry.ToString(),new UTF8Encoding(false));File.WriteAllText(folder+"/recording.csv",recording.ToString(),new UTF8Encoding(false));
            }
        }
        Require(poses.Count==WeaponLootTable.All.Count*8,"all31 equipped weapons/eight directions captured; poses="+poses.Count);
        game.ReturnToMainMenu();yield return null;
    }
}
'''
(root/'Tools/Validation/WeaponCarryAudit20261001.cs').write_text(t,encoding='utf-8')
print('Generated actual equipped31 weapon/eight direction native carry capture; no normal campaign claim.')
