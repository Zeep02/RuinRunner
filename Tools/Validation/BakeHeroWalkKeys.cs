using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BakeHeroWalkKeys
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode");
        var path="Assets/RuinRunner/Editor/ArtSources/HeroWalkSouth/";
        Directory.CreateDirectory(path+"baked");
        var root=new GameObject("TemporaryHeroWalkBake"){hideFlags=HideFlags.HideAndDontSave};
        var cameraObject=new GameObject("TemporaryHeroWalkCamera"){hideFlags=HideFlags.HideAndDontSave};
        var rt=new RenderTexture(48,48,24,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point,antiAliasing=1};
        var previous=RenderTexture.active;
        try
        {
            root.transform.position=new Vector3(10000,10000,0);root.layer=31;
            var body=root.AddComponent<SpriteRenderer>();
            var headObject=new GameObject("WholeHead");headObject.transform.SetParent(root.transform,false);headObject.layer=31;
            var head=headObject.AddComponent<SpriteRenderer>();head.sortingOrder=1;head.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path+"head.png");
            var cam=cameraObject.AddComponent<Camera>();cam.enabled=false;cam.orthographic=true;cam.orthographicSize=.75f;cam.aspect=1;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;cam.cullingMask=1<<31;cam.allowHDR=false;cam.allowMSAA=false;cam.targetTexture=rt;
            cam.transform.position=root.transform.position+new Vector3(0,14f/32,-10);rt.Create();
            for(var i=0;i<4;i++)
            {
                body.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path+"keys/"+i.ToString("D2")+".png");
                if(body.sprite==null||head.sprite==null)throw new Exception("Missing candidate source");
                cam.Render();RenderTexture.active=rt;
                var texture=new Texture2D(48,48,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,48,48),0,0);texture.Apply();
                File.WriteAllBytes(path+"baked/"+i.ToString("D2")+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        finally
        {
            RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(root);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        return "Baked four candidate key poses with one unchanged canonical whole head. Editor-only; no runtime replacement.";
    }
}
