"""Reuse the existing native Game View recorder; replace only the real-input review scenarios."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
source=(root/'Tools/Validation/WeaponIdentityPresentation20261001.cs').read_text(encoding='utf-8')
source=source.replace('WeaponIdentityPresentation20261001','SscaryThrustPresentation20261001').replace('2026-10-01-weapon-identity/presentation-','2026-10-01-hero-thrust/presentation-').replace('[Weapon identity presentation]','[Sscary thrust presentation]')
source=source.replace('static int pending; static bool readbackError, flipReadback;', '''static int pending; static bool readbackError, flipReadback;
    static readonly StringBuilder telemetry=new StringBuilder();
    static readonly HashSet<Sprite> phaseSprites=new HashSet<Sprite>();
    static bool sawThrust,sawMobileThrust;
    static void Track(string phase)
    {
        var p=game.Player;var a=p.GetComponent<PlayerAnimationController>();var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
        foreach(var fx in UnityEngine.Object.FindObjectsByType<MeleeThrustEffect>())
            foreach(var streak in fx.GetComponentsInChildren<SpriteRenderer>())
                if(streak.sortingOrder>=body.sortingOrder)throw new Exception("moving thrust trail paints over player body "+phase);
        if(p.IsThrustActive)
        {
            if(!a.PoseName.StartsWith("thrust"))throw new Exception("real thrust still uses fallback "+phase);
            if(Vector2.Distance(p.transform.Find("HeldWeaponPivot").localPosition,a.HandLocalPosition)>.002f)throw new Exception("held grip detached from native hand "+phase);
            if(body.sprite.rect.width!=48||body.sprite.rect.height!=64||body.sprite.pivot!=new Vector2(24,22)||body.sprite.pixelsPerUnit!=32)throw new Exception("native body geometry "+phase);
            sawThrust=true;sawMobileThrust|=a.PoseName=="thrust-move";phaseSprites.Add(body.sprite);
        }
        telemetry.AppendLine(string.Join(",",sample,phase,p.AttackAnimationSequence,p.IsThrustActive,a.PoseName,a.FacingDirection,a.FrameIndex,body.sprite.name,
            p.AttackAnimationProgress.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.x.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.y.ToString("F4",CultureInfo.InvariantCulture),
            a.HandLocalPosition.x.ToString("F4",CultureInfo.InvariantCulture),a.HandLocalPosition.y.ToString("F4",CultureInfo.InvariantCulture)));
    }''')
source=source.replace('checks.Clear(); customSizes.Clear(); buffered.Clear(); sample = 0; recording.Clear(); recording.AppendLine("frame,realtime,phase");','checks.Clear(); customSizes.Clear(); buffered.Clear(); sample = 0; recording.Clear(); recording.AppendLine("frame,realtime,phase"); telemetry.Clear();telemetry.AppendLine("frame,phase,sequence,thrust,pose,direction,key,sprite,progress,x,y,handX,handY");')
source=source.replace('yield return new WaitForEndOfFrame(); double time = Time.realtimeSinceStartupAsDouble;','yield return new WaitForEndOfFrame(); double time = Time.realtimeSinceStartupAsDouble; Track(phase);')
source=source.replace('buffered.Clear(); File.WriteAllText(folder + "/recording.csv", recording.ToString(), new UTF8Encoding(false));','buffered.Clear(); File.WriteAllText(folder + "/recording.csv", recording.ToString(), new UTF8Encoding(false));File.WriteAllText(folder+"/body-telemetry.csv",telemetry.ToString(),new UTF8Encoding(false));')
source=source.replace('catch (Exception error) { RestoreView();','catch (Exception error) { try{Flush();}catch{} RestoreView();')
start=source.index('        foreach(var id in new[]{"worn_dagger"')
end=source.index('        ClearEffects();yield return new WaitForSeconds(.8f);',start)
source=source[:start]+'''
        foreach(var direction in new[]{0,1,2,4,5,7})
        {
            Equip("worn_dagger");game.Player.Stats.RestoreMana(game.Player.MaxMana);
            Vector2 aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);
            yield return new WaitForSeconds(.8f);var enemy=Enemy(aim*1.25f);yield return null;Physics2D.SyncTransforms();float hp=enemy.Health;
            sawThrust=false;sawMobileThrust=false;phaseSprites.Clear();
            StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,false);
            var motion=Record(.55f,"dagger-thrust-"+direction,.09f);
            while(motion.MoveNext()){yield return motion.Current;StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,false);}
            Require(sawThrust&&phaseSprites.Count>=5,"real RMB uses multiple native thrust poses in view "+direction+" samples="+phaseSprites.Count);
            Require(enemy.Health<hp,"real RMB contact damages target in view "+direction);
            Flush();
        }
        foreach(var id in new[]{"worn_dagger","rust_sword"})
        foreach(var moving in new[]{false,true})
        {
            Equip(id);game.Player.Stats.RestoreMana(game.Player.MaxMana);yield return new WaitForSeconds(.85f);
            // A frozen target directly in the movement lane is a real collision obstacle.
            // Keep movement reviews clear; stationary contact reviews retain a target in front.
            var enemy=Enemy(new Vector2(1.35f,moving?1.8f:0));yield return null;Physics2D.SyncTransforms();float hp=enemy.Health;
            sawThrust=false;sawMobileThrust=false;phaseSprites.Clear();
            StageValidationInput.BeginFrame();StageValidationInput.ReleaseAll();StageValidationInput.SetAim(Vector2.right);
            if(moving)StageValidationInput.SetHeld(GameAction.MoveRight,true);
            StageValidationInput.SetHeld(id=="worn_dagger"?GameAction.Guard:GameAction.Attack,true);
            yield return null;StageValidationInput.BeginFrame();if(id=="worn_dagger")StageValidationInput.SetHeld(GameAction.Guard,false);
            Vector2 before=game.Player.transform.position;
            var phase=id+(moving?"-moving":"-standing");var motion=Record(id=="worn_dagger"?.62f:1.55f,phase,.09f);
            while(motion.MoveNext())
            {
                yield return motion.Current;StageValidationInput.BeginFrame();
                if(id=="worn_dagger")StageValidationInput.SetHeld(GameAction.Guard,false);
            }
            StageValidationInput.ReleaseAll();Require(sawThrust&&phaseSprites.Count>=5,"actual "+phase+" contains keyed thrust/contact/recovery poses");
            if(moving){Require(sawMobileThrust,"moving thrust combines independent original gait "+id);Require(Vector2.Distance(before,game.Player.transform.position)>.9f,"thrust input keeps actual movement "+id);}
            else Require(enemy.Health<hp,"normal combo/RMB contact still damages target "+id);
            Flush();
        }
        // Dash cancels the body and blade together through the real GameAction route.
        Equip("worn_dagger");game.Player.Stats.RestoreMana(game.Player.MaxMana);yield return new WaitForSeconds(.9f);
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,false);
        double readyBy=Time.realtimeSinceStartupAsDouble+.35;
        while(!game.Player.IsThrustActive&&Time.realtimeSinceStartupAsDouble<readyBy){yield return null;StageValidationInput.BeginFrame();}
        Require(game.Player.IsThrustActive,"dash scenario starts an actual thrust");
        Pulse(GameAction.Dash);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();
        Require(!game.Player.IsThrustActive&&game.Player.GetComponent<PlayerAnimationController>().PoseName=="dash","dash immediately leaves thrust body/blade together");
        var dashRecord=Record(.45f,"thrust-dash-return",.07f);while(dashRecord.MoveNext()){yield return dashRecord.Current;StageValidationInput.BeginFrame();}Flush();
        Require(!game.Player.GetComponent<PlayerAnimationController>().PoseName.StartsWith("thrust"),"cancelled thrust does not replay after dash");
        // Pause through the existing menu method, not a custom animation-only pause.
        Equip("worn_dagger");game.Player.Stats.RestoreMana(game.Player.MaxMana);yield return new WaitForSeconds(.9f);
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,false);
        readyBy=Time.realtimeSinceStartupAsDouble+.35;
        while(!game.Player.IsThrustActive&&Time.realtimeSinceStartupAsDouble<readyBy){yield return null;StageValidationInput.BeginFrame();}
        yield return new WaitForSeconds(.035f);yield return new WaitForEndOfFrame();
        Require(game.Player.IsThrustActive,"pause scenario reaches the actual contact/recovery clock after dagger lunge windup");
        var anim=game.Player.GetComponent<PlayerAnimationController>();var frozen=game.Player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite;var hand=anim.HandLocalPosition;int key=anim.FrameIndex;
        typeof(GameController).GetMethod("SetManualPause",Private).Invoke(game,new object[]{true});Time.timeScale=0;
        yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();
        Require(anim.FrameIndex==key&&anim.HandLocalPosition==hand&&game.Player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite==frozen,"menu pause freezes authored pose/held grip without phase drift");
        typeof(GameController).GetMethod("SetManualPause",Private).Invoke(game,new object[]{false});Time.timeScale=1;yield return new WaitForSeconds(.4f);
        Require(!game.Player.IsThrustActive&&!anim.PoseName.StartsWith("thrust"),"resuming finishes recovery and restores native idle");
''' + source[end:]
target=root/'Tools/Validation/SscaryThrustPresentation20261001.cs';target.write_text(source,encoding='utf-8');print(target)
