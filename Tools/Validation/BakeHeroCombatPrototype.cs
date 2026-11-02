using System;
using System.IO;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Editor-only first south slash prototype. Runtime combat is unchanged.
public static class BakeHeroCombatPrototype
{
    const string Source="Assets/RuinRunner/Editor/ArtSources/HeroWalkSouth/";
    static Transform root;
    static SpriteRenderer Part(string name,string source,int order)
    {
        var obj=new GameObject(name){layer=31};obj.transform.SetParent(root,false);
        var sr=obj.AddComponent<SpriteRenderer>();sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Source+source+".png");sr.sortingOrder=order;
        if(sr.sprite==null)throw new Exception("Missing source "+source);return sr;
    }
    static Vector3 Point(Vector2 p)=>new Vector3((p.x-24)/32,(38-p.y)/32,0);
    static void Place(SpriteRenderer part,Vector2 p,float angle=0)
    {part.transform.localPosition=Point(p);part.transform.localRotation=Quaternion.Euler(0,0,angle);}
    static void Bone(SpriteRenderer sr,Vector2 start,Vector2 end)
    {var delta=end-start;Place(sr,start,Mathf.Atan2(delta.x,delta.y)*Mathf.Rad2Deg);}
    static Vector2 Elbow(Vector2 shoulder,Vector2 hand,float side)
    {
        // Registered joint-to-glove lengths, not the sprite's transparent rect.
        const float upper=3.2f,lower=3.5f;var delta=hand-shoulder;
        if(delta.magnitude>upper+lower)throw new Exception("Unreachable glove socket: "+delta.magnitude);
        var d=Mathf.Clamp(delta.magnitude,.1f,upper+lower-.001f);var dir=delta.normalized;
        var a=(upper*upper-lower*lower+d*d)/(2*d);
        return shoulder+dir*a+new Vector2(dir.y,-dir.x)*side*Mathf.Sqrt(Mathf.Max(0,upper*upper-a*a));
    }
    static void Leg(SpriteRenderer thigh,SpriteRenderer shin,Vector2 hip,float upper,float lower,float roll)
    {
        Place(thigh,hip);thigh.transform.localRotation=Quaternion.Euler(upper,0,roll);
        shin.transform.localPosition=thigh.transform.localPosition+thigh.transform.localRotation*new Vector3(0,-3.5f/32,0);
        shin.transform.localRotation=Quaternion.Euler(lower,0,-roll);
    }
    static Vector2 Curve(float t,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Vector2 e)
    {
        Vector2 from,to;float x;
        if(t<.26f){from=a;to=b;x=t/.26f;}
        else if(t<MeleeMotionTiming.Contact){from=b;to=c;x=(t-.26f)/(MeleeMotionTiming.Contact-.26f);}
        else if(t<MeleeMotionTiming.Recovery){from=c;to=d;x=(t-MeleeMotionTiming.Contact)/(MeleeMotionTiming.Recovery-MeleeMotionTiming.Contact);}
        else{from=d;to=e;x=(t-MeleeMotionTiming.Recovery)/(1-MeleeMotionTiming.Recovery);}
        return Vector2.Lerp(from,to,Mathf.SmoothStep(0,1,x));
    }
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode");
        var output="Assets/RuinRunner/Editor/ArtSources/HeroCombat/attack_0/04";Directory.CreateDirectory(output);
        var evidence="ArtDirection/Generated/HeroCombat-20260921";Directory.CreateDirectory(evidence+"/south-slash0-preview");
        var holder=new GameObject("TemporaryCombatRig"){hideFlags=HideFlags.HideAndDontSave};root=holder.transform;root.position=new Vector3(10000,10000,0);holder.layer=31;
        var cameraObject=new GameObject("TemporaryCombatCamera"){hideFlags=HideFlags.HideAndDontSave};
        var bodyRT=new RenderTexture(48,48,24,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point,antiAliasing=1};
        var previewRT=new RenderTexture(96,96,24,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point,antiAliasing=1};var previous=RenderTexture.active;
        var csv=new StringBuilder("frame,progress,handX,handY,bodyX,bodyY,bladeAngle\n");
        try
        {
            var scarf=Part("Scarf","rig/scarf",0);var lt=Part("LeftThigh","rig/thigh",1);var ls=Part("LeftShin","rig/shin",2);
            var rt=Part("RightThigh","rig/thigh",1);var rs=Part("RightShin","rig/shin",2);
            var lu=Part("LeftUpper","rig/upperArm",3);var lf=Part("LeftForearm","rig/forearm",4);
            var ru=Part("RightUpper","rig/upperArm",6);var rf=Part("RightForearm","rig/forearm",6);
            var torso=Part("Torso","rig/torso",5);var head=Part("WholeHead","head",7);
            var weaponObject=new GameObject("PreviewWeapon"){layer=31};weaponObject.transform.SetParent(root,false);
            var weapon=weaponObject.AddComponent<SpriteRenderer>();weapon.sprite=PixelArtAtlas.GetHeldWeaponSprite(WeaponLootTable.FindFirst(WeaponFamily.Longsword));weapon.sortingOrder=8;weapon.transform.localScale=Vector3.one*.76f;
            var cam=cameraObject.AddComponent<Camera>();cam.enabled=false;cam.orthographic=true;cam.aspect=1;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;
            cam.cullingMask=1<<31;cam.allowHDR=false;cam.allowMSAA=false;cam.transform.position=root.position+new Vector3(0,14f/32,-10);
            bodyRT.Create();previewRT.Create();
            var phases=new[]{0f,.07f,.14f,.20f,.27f,.35f,.42f,.49f,.59f,.71f,.84f,.96f};
            for(var i=0;i<phases.Length;i++)
            {
                var p=phases[i];var body=Curve(p,Vector2.zero,new Vector2(-.8f,.3f),new Vector2(.8f,.2f),new Vector2(.35f,.3f),Vector2.zero);
                var hand=Curve(p,new Vector2(30.25f,31.0f),new Vector2(32.5f,25.7f),new Vector2(30.5f,31.0f),new Vector2(25.8f,29.0f),new Vector2(30.25f,30.8f))+body;
                var leftHand=Curve(p,new Vector2(17.8f,30.7f),new Vector2(18.4f,29.5f),new Vector2(19.0f,30.5f),new Vector2(17.2f,30.2f),new Vector2(17.8f,30.7f))+body;
                var shoulder=new Vector2(29.5f,24.5f)+body;var leftShoulder=new Vector2(18.5f,24.5f)+body;
                var elbow=Elbow(shoulder,hand,1);var leftElbow=Elbow(leftShoulder,leftHand,-1);
                Bone(ru,shoulder,elbow);Bone(rf,elbow,hand);Bone(lu,leftShoulder,leftElbow);Bone(lf,leftElbow,leftHand);
                var weight=Curve(p,Vector2.zero,new Vector2(1,0),new Vector2(0,1),new Vector2(0,.5f),Vector2.zero);
                Leg(lt,ls,new Vector2(20.8f,29.8f)+body,20+25*weight.x-8*weight.y,-12-20*weight.x,-4-6*weight.y);
                Leg(rt,rs,new Vector2(27.2f,29.8f)+body,25-12*weight.x+20*weight.y,-20-14*weight.y,4+4*weight.x);
                Place(torso,new Vector2(24,21)+body);Place(head,new Vector2(24,38)+body);
                Place(scarf,new Vector2(19,23)+body,Curve(p,Vector2.zero,new Vector2(-5,0),new Vector2(2,0),new Vector2(8,0),Vector2.zero).x);
                var blade=MeleeMotionTiming.BladeAngle(p,false,0);
                weapon.transform.localPosition=Point(hand);weapon.transform.localRotation=Quaternion.Euler(0,0,-180+blade);
                weapon.enabled=false;cam.orthographicSize=.75f;cam.targetTexture=bodyRT;cam.Render();Save(bodyRT,output+"/"+i.ToString("D2")+".png");
                weapon.enabled=true;cam.orthographicSize=1.5f;cam.targetTexture=previewRT;cam.Render();Save(previewRT,evidence+"/south-slash0-preview/"+i.ToString("D2")+".png");
                csv.AppendLine(string.Join(",",i,p.ToString("F3",CultureInfo.InvariantCulture),hand.x.ToString("F3",CultureInfo.InvariantCulture),hand.y.ToString("F3",CultureInfo.InvariantCulture),body.x.ToString("F3",CultureInfo.InvariantCulture),body.y.ToString("F3",CultureInfo.InvariantCulture),blade.ToString("F3",CultureInfo.InvariantCulture)));
            }
        }
        finally{RenderTexture.active=previous;bodyRT.Release();previewRT.Release();UnityEngine.Object.DestroyImmediate(bodyRT);UnityEngine.Object.DestroyImmediate(previewRT);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(holder);}
        File.WriteAllText(evidence+"/south-slash0-poses.csv",csv.ToString());AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        return "12 Editor-only south slash candidates plus weapon preview. Existing .42 contact/.68 recovery, runtime unchanged.";
    }
    static void Save(RenderTexture rt,string file)
    {
        RenderTexture.active=rt;var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();
        File.WriteAllBytes(file,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
    }
}
