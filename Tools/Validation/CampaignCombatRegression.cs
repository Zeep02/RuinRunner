using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using RuinRunner;

// Isolated pose/shape regression. This deliberately stages actors, never a run completion.
public static class CampaignCombatRegression
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool pass,string message){if(!pass)throw new Exception(message);checks++;Debug.Log("[Campaign Combat] CHECK "+message);}
    public static string Main()
    {
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        game.ReturnToMainMenu();game.SaveSlots.OpenTransientForTests();
        typeof(GameController).GetMethod("BeginRunForActiveSlot",Private).Invoke(game,null);
        var input=typeof(GameController).Assembly.GetType("RuinRunner.StageValidationInput");
        input.GetField("enabled",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        input.GetField("initialized",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        game.CameraFollow.StartCoroutine(Run(game,input));
        return "Isolated combat regression started";
    }
    static IEnumerator Run(GameController game,Type input)
    {
        yield return new WaitForSecondsRealtime(.2f);
        game.StopAllCoroutines();
        typeof(GameController).Assembly.GetType("RuinRunner.RuntimeSceneObjects").GetMethod("ClearSession").Invoke(null,null);
        game.Player.Teleport(Vector2.zero);Time.timeScale=1;Time.captureFramerate=0;
        yield return null;
        checks=0;
        var player=game.Player;
        var right=EnemyController.Create(EnemyType.Boss,new Vector2(1.1f,.15f),player,()=>true,_=>{},null);
        var left=EnemyController.Create(EnemyType.Boss,new Vector2(-1.1f,.15f),player,()=>true,_=>{},null);
        var initialRight=right.Health;var initialLeft=left.Health;
        input.GetMethod("SetAim").Invoke(null,new object[]{Vector2.right});
        input.GetMethod("SetHeld").Invoke(null,new object[]{GameAction.Attack,true});
        yield return null;
        input.GetMethod("BeginFrame").Invoke(null,null);
        input.GetMethod("SetHeld").Invoke(null,new object[]{GameAction.Attack,false});
        input.GetMethod("SetAim").Invoke(null,new object[]{Vector2.left});
        Check(Vector2.Dot(player.MeleeAimDirection,Vector2.right)>.99f,"active swing keeps committed direction after cursor reversal");
        yield return new WaitForSeconds(.38f);
        Check(right.Health<initialRight && left.Health==initialLeft,"contact hits committed side only");
        UnityEngine.Object.Destroy(right.gameObject);UnityEngine.Object.Destroy(left.gameObject);
        foreach(EnemyType type in Enum.GetValues(typeof(EnemyType)))
        {
            var enemy=EnemyController.Create(type,new Vector2(7,5),player,()=>true,_=>{},null);
            var collider=(BoxCollider2D)enemy.DamageCollider;
            var size=collider.size;var offset=collider.offset;
            var bar=(Transform)typeof(EnemyController).GetField("healthBarRoot",Private).GetValue(enemy);
            var barPosition=bar.localPosition;
            var view=(SpriteRenderer)typeof(EnemyController).GetField("spriteRenderer",Private).GetValue(enemy);
            foreach(var sprite in new[]{Resources.Load<Sprite>("Art/Pixel/Separated/HeroMotion/attack/00/00"),Resources.Load<Sprite>("Art/Pixel/Separated/Campaign/Hazards/Rectangle")})
            {
                if(sprite==null)throw new Exception("Regression reference sprite is missing");
                view.sprite=sprite;enemy.UpdateVisualPresentation();
                Check(collider.size==size && collider.offset==offset && bar.localPosition==barPosition,type+" authored body ignores radically different sprite bounds");
            }
            UnityEngine.Object.Destroy(enemy.gameObject);
        }
        var owner=EnemyController.Create(EnemyType.Tidekeeper,new Vector2(8,8),player,()=>false,_=>{},null);owner.enabled=false;
        foreach(var circle in new[]{false,true})foreach(var angle in new[]{0f,45f,90f})
        {
            var hazard=ArenaHazard.Create(owner,player,new Vector2(4,4),new Vector2(6,2),angle,circle,10,.1f,7,Color.cyan);
            var rotation=Quaternion.Euler(0,0,angle);
            Check(hazard.Contains(new Vector2(4,4)+(Vector2)(rotation*new Vector3(2.99f,0))) && !hazard.Contains(new Vector2(4,4)+(Vector2)(rotation*new Vector3(3.01f,0))),"hazard long edge "+circle+" angle="+angle);
            Check(hazard.Contains(new Vector2(4,4)+(Vector2)(rotation*new Vector3(0,.99f))) && !hazard.Contains(new Vector2(4,4)+(Vector2)(rotation*new Vector3(0,1.01f))),"hazard short edge "+circle+" angle="+angle);
            Check(hazard.Contains(new Vector2(4,4)+(Vector2)(rotation*new Vector3(2.4f,.8f)))==!circle,"ellipse/rectangle corner differs "+circle+" angle="+angle);
            var renderer=hazard.GetComponentInChildren<SpriteRenderer>();
            Check(renderer.sortingLayerName=="WorldFloor" && (circle || renderer.drawMode==SpriteDrawMode.Sliced && renderer.size==new Vector2(6,2)),"authored warning rendered with matching dimensions "+circle+" angle="+angle);
            UnityEngine.Object.Destroy(hazard.gameObject);
        }
        player.SetStageClearSafety(false);
        var hp=player.Health;
        var damaging=ArenaHazard.Create(owner,player,player.transform.position,Vector2.one*4,0,true,.15f,2.2f,7,Color.cyan);
        yield return new WaitForSeconds(.1f);
        Check(player.Health==hp,"warning inflicts no damage");
        yield return new WaitForSeconds(1.8f);
        Check(player.Health==hp-7,"active hazard inflicts one damage event across invulnerability expiry");
        UnityEngine.Object.Destroy(damaging.gameObject);UnityEngine.Object.Destroy(owner.gameObject);
        input.GetMethod("ReleaseAll").Invoke(null,null);
        Debug.Log("[Campaign Combat] PASS "+checks+" isolated assertions; not normal campaign completion.");
    }
}
