using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using RuinRunner;

// Isolated geometry regression, not campaign or input responsiveness evidence.
public static class LargeAttackGeometryFixture
{
    static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static StringBuilder report=new StringBuilder();
    static string folder;
    public static string Main()
    {
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-21-large-geometry/"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();StageValidationInput.SetAim(Vector2.right);Time.timeScale=1;
        var follow=CameraFollow.Create();var inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
        var player=PlayerController.Create(new Vector2(500,500),()=>false,()=>false,()=>{},follow,inventory,new GameSettingsService());player.EquipWeapon(inventory.EquippedWeapon);
        follow.Target=player.transform;follow.StartCoroutine(Run(player));return folder;
    }
    static IEnumerator Run(PlayerController player)
    {
        var origin=(Vector2)player.transform.position;
        var near=EnemyController.Create(EnemyType.Boss,origin+Vector2.right*3,player,()=>true,_=>{},null);
        var back=EnemyController.Create(EnemyType.Boss,origin+Vector2.left*3,player,()=>true,_=>{},null);
        yield return null;Physics2D.SyncTransforms();
        // Put the nearest damage surface outside the previous 1.635-unit reach,
        // inside the enlarged 2.41-unit reach (use angle zero for this fixture).
        near.transform.position+=(Vector3)Vector2.right*(origin.x+2.1f-near.DamageCollider.bounds.min.x);
        Physics2D.SyncTransforms();
        var profile=MeleeComboCatalog.Get(player.EquippedWeapon);
        var source=profile.LightSteps[0];var step=new MeleeAttackStep(source.DamageMultiplier,source.DelayMultiplier,source.Reach,source.HitboxSize,0,0);
        var hp=near.Health;var backHp=back.Health;var contacts=player.MeleeContactsResolved;
        typeof(PlayerController).GetMethod("PerformMeleeAttack",Private).Invoke(player,new object[]{step,1f,false});
        yield return new WaitForSeconds(.4f);
        if(near.Health>=hp||back.Health!=backHp||player.MeleeContactsResolved!=contacts+1){Finish("FAIL expanded leading edge / rear exclusion / single contact");yield break;}
        report.AppendLine("PASS target outside old range is hit; rear target untouched; one contact");
        var after=near.Health;yield return new WaitForSeconds(.4f);
        if(near.Health!=after){Finish("FAIL lingering VFX caused another hit");yield break;}
        report.AppendLine("PASS enlarged lingering VFX causes no extra damage");
        var wall=new GameObject("Test occluder");wall.layer=GameLayers.Ground;wall.transform.position=origin+Vector2.right*1.6f;
        wall.AddComponent<BoxCollider2D>().size=new Vector2(.2f,5);Physics2D.SyncTransforms();
        typeof(PlayerController).GetMethod("PerformMeleeAttack",Private).Invoke(player,new object[]{step,1f,false});
        yield return new WaitForSeconds(.4f);
        if(near.Health!=after){Finish("FAIL enlarged attack hit through wall");yield break;}
        report.AppendLine("PASS enlarged attack remains occluded by wall");
        UnityEngine.Object.Destroy(wall);UnityEngine.Object.Destroy(near.gameObject);UnityEngine.Object.Destroy(back.gameObject);
        Finish("PASS");
    }
    static void Finish(string status){File.WriteAllText(folder+"/result.txt",status+"\n"+report+"\nIsolated staged damage geometry; no normal campaign claim.");Debug.Log("[Large attack geometry] "+status);}
}
