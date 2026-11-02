using System;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEditor;
using UnityEngine;

// Authoring-only joint animation. Unity exports full individual frames. No
// runtime bone rig, silhouette inference, per-frame scale, or eye overlay.
public static class BakeHeroWalkRig
{
    const string PathRoot="Assets/RuinRunner/Editor/ArtSources/HeroWalkSouth/";
    static Transform root,partsRoot;
    static SpriteRenderer Part(string name,string source,int order)
    {
        var obj=new GameObject(name){layer=31};obj.transform.SetParent(partsRoot,false);
        var sr=obj.AddComponent<SpriteRenderer>();sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(PathRoot+source+".png");
        sr.sortingOrder=order;if(sr.sprite==null)throw new Exception("Missing part: "+source);return sr;
    }
    static Vector2 World(Vector2 p)=>new Vector2((p.x-24)/32,(38-p.y)/32);
    static void Place(SpriteRenderer sr,Vector2 p,float rotation=0)
    {sr.transform.localPosition=World(p);sr.transform.localRotation=Quaternion.Euler(0,0,rotation);}
    static void Bone(SpriteRenderer sr,Vector2 start,Vector2 end)
    {
        var d=end-start;Place(sr,start,Mathf.Atan2(d.x,d.y)*Mathf.Rad2Deg);
    }
    static Vector2 Joint(Vector2 start,Vector2 end,float a,float b,float side)
    {
        var delta=end-start;var distance=Mathf.Clamp(delta.magnitude,.01f,a+b-.001f);var dir=delta.normalized;
        var along=(a*a-b*b+distance*distance)/(2*distance);
        var outward=new Vector2(dir.y,-dir.x)*side;
        return start+dir*along+outward*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
    }
    static Vector2 Leg(SpriteRenderer thigh,SpriteRenderer shin,Vector2 hip,float upperAngle,float lowerAngle,float roll,bool sideView=false)
    {
        // Sagittal joint rotations project a lifting knee into a shorter leg;
        // a flat 2D IK solution instead splays both knees sideways unnaturally.
        Place(thigh,hip);thigh.transform.localRotation=Quaternion.Euler(upperAngle,0,roll);
        var knee=thigh.transform.localPosition+thigh.transform.localRotation*new Vector3(0,-3.5f/32,0);
        shin.transform.localPosition=knee;shin.transform.localRotation=Quaternion.Euler(lowerAngle,0,sideView?roll:-roll);
        var foot=knee+shin.transform.localRotation*new Vector3(0,-4.5f/32,0);
        return new Vector2(24+foot.x*32,38-foot.y*32);
    }
    public static string Main()
    {
        var result=new StringBuilder();
        foreach(var direction in new[]{1,3,5,6,7})result.AppendLine(BakeDirection(direction));
        return result.ToString();
    }
    public static string BakeDirection(int direction)
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode");
        if(direction<0||direction>7)throw new Exception("Invalid direction");
        var mirrored=direction>4;var basis=mirrored?8-direction:direction;
        var side=basis==2?1f:(basis==1||basis==3?.6f:0);
        var output=PathRoot+(direction==4?"rigBaked":"bakedDirections/"+direction.ToString("D2"));Directory.CreateDirectory(output);
        var rootObject=new GameObject("TemporaryHeroRigBake"){hideFlags=HideFlags.HideAndDontSave};root=rootObject.transform;
        var cameraObject=new GameObject("TemporaryHeroRigCamera"){hideFlags=HideFlags.HideAndDontSave};
        var rt=new RenderTexture(48,48,24,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point,antiAliasing=1};var previous=RenderTexture.active;
        var metadata=new StringBuilder("frame,handX,handY,headX,headY,leftFootX,leftFootY,rightFootX,rightFootY\n");
        try
        {
            root.position=new Vector3(10000,10000,0);rootObject.layer=31;
            var partsObject=new GameObject("AuthoredBodyParts"){layer=31};partsRoot=partsObject.transform;partsRoot.SetParent(root,false);
            partsRoot.localScale=new Vector3(mirrored?-1:1,1,1);
            var scarf=Part("Scarf","rig/scarf",0);
            var lt=Part("LeftThigh","rig/thigh",1);var ls=Part("LeftShin","rig/shin",2);
            var rtg=Part("RightThigh","rig/thigh",1);var rs=Part("RightShin","rig/shin",2);
            var lu=Part("LeftUpperArm","rig/upperArm",3);var lf=Part("LeftForearm","rig/forearm",4);
            var ru=Part("RightUpperArm","rig/upperArm",3);var rf=Part("RightForearm","rig/forearm",4);
            var torso=Part("Torso",basis==4?"rig/torso":"directions/torso-"+basis.ToString("D2"),5);
            var head=Part("WholeHead",direction==4?"head":"directions/head-"+direction.ToString("D2"),7);
            head.transform.SetParent(root,false); // Directional master is never mirrored.
            scarf.flipX=basis==0;
            var cam=cameraObject.AddComponent<Camera>();cam.enabled=false;cam.orthographic=true;cam.orthographicSize=.75f;cam.aspect=1;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;cam.cullingMask=1<<31;cam.allowHDR=false;cam.allowMSAA=false;cam.targetTexture=rt;
            cam.transform.position=root.position+new Vector3(0,14f/32,-10);rt.Create();
            // Contact -> weight transfer -> toe-off -> raised knee -> reach.
            // The opposite leg is half a cycle out of phase. Anchored poses at
            // 0/3/6/9 define the gait; these are articulation, not image shifts.
            var upperAngles=new[]{15f,18,22,28,34,40,45,60,70,62,44,25};
            var lowerAngles=new[]{-8f,-15,-25,-34,-45,-53,-59,-58,-45,-25,-10,-5};
            for(var i=0;i<12;i++)
            {
                var gait=basis<2?(12-i)%12:i;
                var phase=gait*Mathf.PI/6;var bob=Mathf.Lerp(-.5f,-.25f,side)*(1-Mathf.Cos(phase*2));var sway=.25f*Mathf.Sin(phase);
                var offset=new Vector2(sway,bob);var opposite=(gait+6)%12;
                var lh=new Vector2(Mathf.Lerp(21,23,side),29.8f)+offset;var rh=new Vector2(Mathf.Lerp(27,25,side),29.8f)+offset;
                var leftFoot=Leg(lt,ls,lh,upperAngles[gait],lowerAngles[gait],Mathf.Lerp(-3,30*Mathf.Cos(phase),side),side>0);
                var rightFoot=Leg(rtg,rs,rh,upperAngles[opposite],lowerAngles[opposite],Mathf.Lerp(3,-30*Mathf.Cos(phase),side),side>0);
                Place(torso,new Vector2(24,21)+offset);Place(head,new Vector2(24+(mirrored?-sway:sway),38+bob));
                Place(scarf,new Vector2(basis==0?29:19,23)+offset,(basis==0?-1:1)*5*Mathf.Sin(phase-.6f));
                var lShoulder=new Vector2(Mathf.Lerp(18.5f,22.5f,side),24.5f)+offset;var rShoulder=new Vector2(Mathf.Lerp(29.5f,25.5f,side),24.5f)+offset;
                var le=new Vector2(Mathf.Lerp(17.9f,22.7f-1.4f*Mathf.Cos(phase),side),27.2f-.7f*Mathf.Cos(phase))+offset;
                var re=new Vector2(Mathf.Lerp(30.1f,25.8f+1.4f*Mathf.Cos(phase),side),27.2f+.7f*Mathf.Cos(phase))+offset;
                var lHand=le+new Vector2(-.15f-.5f*Mathf.Sin(phase),3.65f);
                var rHand=re+new Vector2(.15f+.5f*Mathf.Sin(phase),3.65f);
                Bone(lu,lShoulder,le);Bone(lf,le,lHand);Bone(ru,rShoulder,re);Bone(rf,re,rHand);
                lf.sortingOrder=Mathf.Cos(phase)<0?6:4;rf.sortingOrder=Mathf.Cos(phase)>0?6:4;
                if(side>0){lu.sortingOrder=0;lf.sortingOrder=0;ru.sortingOrder=6;rf.sortingOrder=6;lt.sortingOrder=1;ls.sortingOrder=1;rtg.sortingOrder=3;rs.sortingOrder=3;}
                cam.Render();RenderTexture.active=rt;var tex=new Texture2D(48,48,TextureFormat.RGBA32,false);
                tex.ReadPixels(new Rect(0,0,48,48),0,0);tex.Apply();File.WriteAllBytes(output+"/"+i.ToString("D2")+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
                metadata.AppendLine(string.Join(",",i,(mirrored?48-rHand.x:rHand.x).ToString("F3",CultureInfo.InvariantCulture),rHand.y.ToString("F3",CultureInfo.InvariantCulture),(mirrored?-sway:sway).ToString("F3",CultureInfo.InvariantCulture),bob.ToString("F3",CultureInfo.InvariantCulture),mirrored?48-leftFoot.x:leftFoot.x,leftFoot.y,mirrored?48-rightFoot.x:rightFoot.x,rightFoot.y));
            }
        }
        finally{RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(rootObject);}
        var label=new[]{"north","northeast","east","southeast","south","southwest","west","northwest"}[direction];
        File.WriteAllText("ArtDirection/Generated/HeroWalk-20260921/"+label+"-rig-poses.csv",metadata.ToString());
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        return "12 Editor-only direction "+direction+" walk frames rendered from authored joint trajectories; runtime remains unchanged.";
    }
}
