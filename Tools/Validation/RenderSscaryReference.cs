using System;
using System.IO;
using UnityEngine;
using UnityEditor;
public static class RenderSscaryReference
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode");
        const string folder="ArtDirection/Generated/SscaryMotion-20260922";
        Directory.CreateDirectory(folder);
        var root=new GameObject("Temporary source reference render");root.hideFlags=HideFlags.HideAndDontSave;
        var camera=root.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=4;
        camera.aspect=.75f;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
        camera.cullingMask=1<<30;camera.allowHDR=false;camera.allowMSAA=false;
        var target=new RenderTexture(768,1024,24,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point};target.Create();camera.targetTexture=target;
        var previous=RenderTexture.active;
        try
        {
            for(var i=0;i<16;i++)
            {
                var obj=new GameObject("Original idle reference "+i);obj.hideFlags=HideFlags.HideAndDontSave;obj.layer=30;obj.transform.SetParent(root.transform,true);
                obj.transform.position=new Vector3(-3+(i%4+.5f)*1.5f,4-(i/4)*2-42f/32,0);
                obj.AddComponent<SpriteRenderer>().sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/RuinRunner/ArtReview/SscaryMale/idle/right_down/00.png");
            }
            camera.Render();RenderTexture.active=target;
            var image=new Texture2D(768,1024,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,768,1024),0,0);image.Apply();
            var path=folder+"/original-idle-grid-4x.png";File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);return Path.GetFullPath(path);
        }
        finally{RenderTexture.active=previous;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(root);}
    }
}
