"""Small actual-input physics/proc safety regression for the revised two-stroke combo."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/CrossCutPresentation20261001.cs').read_text(encoding='utf-8')
t=t[:t.index('    static IEnumerator Case(')]
t=t.replace('CrossCutPresentation20261001','CrossCutRules20261001').replace('cross-cut/presentation-','cross-cut/rules-').replace('PASS staged supplied-weapon/target motion review; not normal campaign, Windows build or 60fps performance evidence','PASS staged supplied two-stroke physics/proc rules; not normal campaign, visual, Windows or performance evidence')
t+=r'''    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);
        foreach(var blocked in new[]{false,true})
        {
            Equip("training_katana",AccessoryType.BerserkerEmblem);yield return null;StageValidationInput.SetAim(Vector2.right);
            game.Inventory.AwardMaterials(1000,"cross-rules-"+blocked);long identity=game.Inventory.ActiveSlot.Item.Identity;
            for(int level=0;level<3;level++)Require(game.Inventory.TryUpgrade(identity,level),"actual owned +"+(level+1)+" upgrade "+blocked);
            var p=game.Player;typeof(PlayerStats).GetProperty("ManaRegeneration").SetValue(p.Stats,0f);
            var prepare=Prepare();while(prepare.MoveNext())yield return prepare.Current;
            var enemy=Enemy(Vector2.right*1.05f);var copy=new GameObject("DuplicateEnemyHurtCollider");copy.transform.SetParent(enemy.transform,false);copy.layer=GameLayers.Enemy;copy.AddComponent<BoxCollider2D>().size=new Vector2(.55f,.55f);
            GameObject wall=null;if(blocked){wall=new GameObject("CrossRulesGroundBarrier");wall.layer=GameLayers.Ground;wall.transform.position=(Vector2)p.transform.position+Vector2.right*.45f;wall.AddComponent<BoxCollider2D>().size=new Vector2(.08f,2f);}
            yield return null;Physics2D.SyncTransforms();p.Stats.TrySpendMana(20);float mana=p.Mana,hp=enemy.Health;int seq=p.AttackAnimationSequence,contacts=p.MeleeContactsResolved;
            Pulse(GameAction.Attack);yield return null;StageValidationInput.BeginFrame();
            float total=(float)typeof(PlayerController).GetField("pendingMeleeDamage",Private).GetValue(p);
            var ready=Ready();while(ready.MoveNext())yield return ready.Current;
            Require(p.AttackAnimationSequence==seq+1&&p.MeleeContactsResolved==contacts+2,"one input/two actual physical contacts "+blocked);
            float expected=blocked?0:2*Mathf.Max(1,total*.5f*100/(100+enemy.Armor));
            Require(Mathf.Abs(hp-enemy.Health-expected)<.003f,"wall blocks/double collider does not duplicate either hit "+blocked);
            Require(Mathf.Abs(p.Mana-mana-(blocked?0:2))<.003f,"+3 mana restores at most once for two strokes "+blocked);
            var effectContacts=(System.Collections.IDictionary)typeof(ItemCombatEffects).GetField("contacts",Private).GetValue(p.ItemEffects);
            if(blocked)Require(!effectContacts.Contains(enemy),"wall-blocked strokes create no item contact");
            else
            {
                Require(effectContacts.Contains(enemy)&&((HashSet<int>)effectContacts[enemy]).Count==1,"both strokes share one original item action/target");
                var marks=(System.Collections.IDictionary)typeof(ItemCombatEffects).GetField("marks",Private).GetValue(p.ItemEffects);
                var mark=marks[enemy];Require(mark!=null&&(int)mark.GetType().GetField("Contacts",All).GetValue(mark)==1,"bleed counter advances once, not twice");
            }
            if(wall!=null)UnityEngine.Object.Destroy(wall);StageValidationInput.ReleaseAll();
        }
        game.ReturnToMainMenu();yield return null;Require(UnityEngine.Object.FindObjectsByType<MeleeCrossCutEffect>().Length==0,"menu removes owned source arcs");
    }
}
'''
(root/'Tools/Validation/CrossCutRules20261001.cs').write_text(t,encoding='utf-8')
print('Generated two actual input cases: duplicate enemy colliders, wall, owned +3 mana once and shared proc contacts.')

