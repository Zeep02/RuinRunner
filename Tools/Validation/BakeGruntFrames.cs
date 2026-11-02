using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Authoring bake of generated sprite parts. No painting, interpolation, or
// per-frame resizing. The game consumes complete individual PNG frames.
public static class BakeGruntFrames
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before baking");
        var rootPath="Assets/RuinRunner/Resources/Art/Pixel/Separated/CreatureMotion/Grunt/";
        var output=rootPath+"frames";Directory.CreateDirectory(output);
        var offsets=new[]{new Vector2(0,8),new Vector2(0,8),new Vector2(-1,8),new Vector2(0,9),new Vector2(1,8),new Vector2(1,7)};
        var root=new GameObject("TemporaryGruntBake"){hideFlags=HideFlags.HideAndDontSave};
        var cameraObject=new GameObject("TemporaryGruntCamera"){hideFlags=HideFlags.HideAndDontSave};
        var rt=new RenderTexture(64,64,24,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point,antiAliasing=1};
        var previous=RenderTexture.active;
        try
        {
            root.transform.position=new Vector3(10000,10000,0);root.layer=31;
            var body=root.AddComponent<SpriteRenderer>();body.sortingOrder=0;
            var headObject=new GameObject("WholeHead");headObject.transform.SetParent(root.transform,false);headObject.layer=31;
            var head=headObject.AddComponent<SpriteRenderer>();head.sortingOrder=1;head.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(rootPath+"head.png");
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1;camera.aspect=1;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<31;
            camera.allowHDR=false;camera.allowMSAA=false;camera.targetTexture=rt;
            camera.transform.position=root.transform.position+new Vector3(0,24f/32f,-10);
            rt.Create();
            for(var i=0;i<6;i++)
            {
                body.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(rootPath+"body/"+i.ToString("D2")+".png");
                if(body.sprite==null||head.sprite==null)throw new Exception("Missing rig source");
                head.transform.localPosition=offsets[i]/32f;
                camera.Render();RenderTexture.active=rt;
                var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,64,64),0,0);texture.Apply();
                File.WriteAllBytes(output+"/"+i.ToString("D2")+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        finally
        {
            RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(root);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        return "Baked six complete 64x64 Grunt poses from authored body PNGs and one unchanged whole-head Sprite. Uniform 32 PPU and fixed 32/8 pivot.";
    }
}


