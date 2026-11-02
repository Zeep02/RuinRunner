from pathlib import Path
root=Path(__file__).resolve().parents[2]
original=(root/'Tools/Validation/ItemEffectsPresentation20261001.cs').read_text(encoding='utf-8-sig')
head=original.split('    static IEnumerator Run()')[0]
head=head.replace('ItemEffectsPresentation20261001','WeaponIdentityPresentation20261001').replace('2026-10-01-item-effects','2026-10-01-weapon-identity').replace('[Item effects presentation]','[Weapon identity presentation]')
head=head.replace('"PASS staged supplied-item/target visual review; not normal campaign, Windows build or 60fps performance evidence','"PASS staged supplied-weapon/target motion review; not normal campaign, Windows build or 60fps performance evidence')
head=head.replace('220f','3000f')
head=head.replace('game.Player.ItemEffects.CancelTransient();','game.Player.ItemEffects.CancelTransient();\n        foreach(var fx in UnityEngine.Object.FindObjectsByType<NativeCombatEffect>())UnityEngine.Object.Destroy(fx.gameObject);\n        foreach(var fx in UnityEngine.Object.FindObjectsByType<WeaponSecondaryParticles>())UnityEngine.Object.Destroy(fx.gameObject);')
tail=r'''    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        Require(Screen.width==1280&&Screen.height==720,"recording is actual native 1280x720 screen, no camera rescale or interpolation");
        var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;
        foreach(var id in new[]{"worn_dagger","rust_sword","training_katana","iron_greatsword","iron_mace","hunter_bow","hand_crossbow","scrap_pistol","scrap_shotgun","storm_rifle","dragon_cannon","void_revolver","flintlock"})
        foreach(var special in new[]{false,true})
        {
            Equip(id);game.Player.Stats.RestoreMana(game.Player.MaxMana);yield return new WaitForSeconds(.65f);
            var family=game.Player.EquippedWeapon.Family;bool ranged=game.Player.EquippedWeapon.AttackType==WeaponAttackType.Ranged;
            var enemy=Enemy(new Vector2(ranged?2.9f:1.4f,0));
            if(family==WeaponFamily.Shotgun){Enemy(new Vector2(2.9f,.7f));Enemy(new Vector2(2.9f,-.7f));}
            if(special&&(family==WeaponFamily.Crossbow||family==WeaponFamily.Rifle))Enemy(new Vector2(4.4f,0));
            yield return null;Physics2D.SyncTransforms();
            var hp=enemy.Health;StageValidationInput.BeginFrame();StageValidationInput.ReleaseAll();StageValidationInput.SetAim(Vector2.right);
            StageValidationInput.SetHeld(special?GameAction.Guard:GameAction.Attack,true);
            yield return null;StageValidationInput.BeginFrame();
            if(special&&family==WeaponFamily.Greatsword)
            {
                var charge=Record(1.22f,id+"-charge",.70f);while(charge.MoveNext()){StageValidationInput.BeginFrame();yield return charge.Current;}Flush();
                Require(game.Player.IsGreatswordCharging&&game.Player.GreatswordChargeProgress>.98f,"actual held RMB reaches full greatsword charge with a blade effect");
                StageValidationInput.SetHeld(GameAction.Guard,false);
                yield return null;StageValidationInput.BeginFrame();
            }
            else if(special&&(family==WeaponFamily.Katana||family==WeaponFamily.Longsword))
                game.Player.TakeDamage(10,Vector2.left);
            else if(special||ranged)StageValidationInput.SetHeld(special?GameAction.Guard:GameAction.Attack,false);
            var phase=id+(special?"-action":"-combo");
            var motion=Record(ranged?.78f:1.35f,phase,ranged?.14f:.16f);while(motion.MoveNext()){StageValidationInput.BeginFrame();yield return motion.Current;}
            StageValidationInput.BeginFrame();StageValidationInput.ReleaseAll();
            Require(special&&family==WeaponFamily.Longsword?game.Player.Health>0:enemy.Health<hp,"real "+phase+" input resolves actual gameplay contact");
            Flush();File.WriteAllText(folder+"/progress.txt",phase,new UTF8Encoding(false));
        }
        ClearEffects();yield return new WaitForSeconds(.8f);
        Require(WeaponSecondaryParticles.ActiveCount==0,"all secondary fragments expire after attacks");
        Require(NativeCombatEffect.ActiveCount==0,"all attack/projectile native effects expire after attacks");
        StageValidationInput.ReleaseAll();game.ReturnToMainMenu();game.enabled=true;
    }
}
'''
(root/'Tools/Validation/WeaponIdentityPresentation20261001.cs').write_text(head+tail,encoding='utf-8')
print('Generated native motion review fixture; keeps async readback/orientation and Game View restore checks')
