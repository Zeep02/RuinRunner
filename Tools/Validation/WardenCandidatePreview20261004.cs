using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using RuinRunner;

// Side-by-side scale/art review only. Nothing is imported or connected to runtime.
public static class WardenCandidatePreview20261004
{
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        var folder="F:/codex/LoglikeCap/ArtDirection/Review-20261004/warden-native";
        Directory.CreateDirectory(folder);
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        var center=map.GetRoomCenter(3);
        var inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
        var follow=CameraFollow.Create();
        var player=PlayerController.Create(center+Vector2.down*2,()=>true,()=>false,()=>{},follow,inventory,new GameSettingsService());player.EquipWeapon(inventory.EquippedWeapon);
        var old=EnemyController.Create(EnemyType.Boss,center+Vector2.right*3,player,()=>true,_=>{},follow);old.enabled=false;
        var obj=new GameObject("Native warden candidate");obj.transform.position=center+Vector2.left*3;
        var renderer=obj.AddComponent<SpriteRenderer>();renderer.sortingOrder=8;
        var sheet=new Texture2D(2,2,TextureFormat.RGBA32,false);sheet.LoadImage(File.ReadAllBytes("ArtDirection/ExternalCandidates/20261003/foozle-king/original/Foozle_2DC0021_Lucifer_Skeleton_King_Pixel_Art/Down/Png/SkeletonKingDownIdle.png"));sheet.filterMode=FilterMode.Point;
        var candidate=Sprite.Create(sheet,new Rect(0,0,48,48),new Vector2(24f/48,8f/48),32,0,SpriteMeshType.FullRect);renderer.sprite=candidate;
        var camObj=new GameObject("Candidate camera");var cam=camObj.AddComponent<Camera>();cam.enabled=false;cam.orthographic=true;cam.orthographicSize=5.625f;cam.aspect=16f/9;cam.cullingMask=1<<31;cam.transform.position=(Vector3)center+Vector3.back*10;
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(18/255f,15/255f,31/255f);cam.allowHDR=false;cam.allowMSAA=false;
        var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point,antiAliasing=1};target.Create();cam.targetTexture=target;
        foreach(var root in new[]{map.Root,player.gameObject,old.gameObject,obj})foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
        var previous=RenderTexture.active;
        try
        {
            cam.Render();RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(folder+"/native-left-current-right.png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);
            File.WriteAllText(folder+"/candidate-scope.txt","Native candidate left at source 32 PPU, original 48x48px canvas and pivot (24,8); current Warden right, approved hero below. Production first-region ground, same camera. Static scale review only; no runtime adoption, animation or gameplay claim.",new UTF8Encoding(false));
        }
        finally
        {
            cam.targetTexture=null;RenderTexture.active=previous;target.Release();
            foreach(var value in new UnityEngine.Object[]{map.Root,player.gameObject,follow.gameObject,old.gameObject,obj,camObj,target,candidate,sheet})UnityEngine.Object.Destroy(value);
        }
        return folder+"/native-left-current-right.png";
    }
}
