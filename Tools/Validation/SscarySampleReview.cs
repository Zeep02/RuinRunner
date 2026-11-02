using System;
using System.IO;
using System.Collections;
using System.Text;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Source-pose review in the production map. Does not replace PlayerController.
public static class SscarySampleReview
{
    static string folder;
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-22-sscary/"+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder+"/frames");
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        Time.timeScale=1;Application.runInBackground=true;game.StartCoroutine(Run());return folder;
    }
    static Sprite Pose(string path)=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/RuinRunner/ArtReview/SscaryMale/"+path+".png");
    static IEnumerator Run()
    {
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        foreach(var t in map.Root.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
        var actor=new GameObject("Selected Sscary sample - review only");actor.layer=31;
        var view=actor.AddComponent<SpriteRenderer>();view.sortingOrder=1000;
        var shadow=new GameObject("Source shadow - review only");shadow.layer=31;
        var shadowView=shadow.AddComponent<SpriteRenderer>();shadowView.sprite=Pose("shadow");shadowView.sortingLayerName="WorldFloor";shadowView.sortingOrder=5;
        var cameraRoot=new GameObject("Source sample review camera");var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;
        camera.orthographic=true;camera.orthographicSize=5.625f;camera.aspect=16f/9;camera.cullingMask=1<<31;
        camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;
        camera.allowHDR=false;camera.allowMSAA=false;
        var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point,antiAliasing=1};target.Create();camera.targetTexture=target;
        var rows=new StringBuilder("frame,realtime,phase,pose\n");
        var dirs=new[]{"down","right_down","right_up","up","left_up","left_down"};
        var vectors=new[]{Vector2.down,new Vector2(1,-1).normalized,new Vector2(1,1).normalized,Vector2.up,new Vector2(-1,1).normalized,new Vector2(-1,-1).normalized};
        var started=Time.realtimeSinceStartupAsDouble;var frame=0;var next=started;var missing=false;
        foreach(var direction in dirs)
        foreach(var action in new[]{"idle","walk","dash","death"})
        {
            var sprites=new Sprite[8];for(var f=0;f<8;f++){sprites[f]=Pose(action+"/"+direction+"/"+f.ToString("00"));missing|=sprites[f]==null;}
            if(missing)break;
            var duration=action=="idle"?1.6f:action=="walk"?1.6f:action=="dash"?.64f:.8f;
            var start=Time.realtimeSinceStartupAsDouble;var elapsed=0f;
            while(elapsed<duration)
            {
                elapsed=(float)(Time.realtimeSinceStartupAsDouble-start);
                var rate=action=="idle"?5:action=="walk"?10:8/duration;
                var index=action=="idle"||action=="walk"?Mathf.FloorToInt(elapsed*rate)%8:Mathf.Min(7,Mathf.FloorToInt(elapsed*rate));
                view.sprite=sprites[index];
                var distance=action=="walk"?Mathf.Sin(elapsed/duration*Mathf.PI)*1.5f:action=="dash"?Mathf.Clamp01(elapsed/duration)*2:0;
                actor.transform.position=vectors[Array.IndexOf(dirs,direction)]*distance;shadow.transform.position=actor.transform.position;
                // Free review uses the supplied idle shadow; death/dash shadow
                // variants are not claimed to have been integrated here.
                shadowView.enabled=action=="idle"||action=="walk";
                if(Time.realtimeSinceStartupAsDouble>=next)
                {
                    var now=Time.realtimeSinceStartupAsDouble;var previous=RenderTexture.active;camera.Render();RenderTexture.active=target;
                    var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
                    File.WriteAllBytes(folder+"/frames/frame-"+frame.ToString("000000")+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);RenderTexture.active=previous;
                    rows.AppendLine(frame+","+(now-started).ToString("F6",CultureInfo.InvariantCulture)+","+action+"-"+direction+","+index);frame++;next=now+1.0/12;
                }
                yield return null;
            }
        }
        File.WriteAllText(folder+"/recording.csv",rows.ToString());
        File.WriteAllText(folder+"/result.txt",(missing?"FAIL missing source sprite":"PASS source playback")+"\nEditor-only original poses on production stage0, 32PPU, fixed pivot (24,22), six authored directions. Scripted display motion, not player input/combat integration or performance. No attack clips in free pack. No paid purchase.\nFrames="+frame);
        camera.targetTexture=null;target.Release();UnityEngine.Object.Destroy(target);
        foreach(var obj in new[]{map.Root,actor,shadow,cameraRoot})UnityEngine.Object.Destroy(obj);
        Debug.Log("[Sscary source review] "+folder);
    }
}
