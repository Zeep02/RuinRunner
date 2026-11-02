using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using RuinRunner;
using Newtonsoft.Json.Linq;
public static class PreviewArmedGrip20261001
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop play for temporary native asset preview");
        var data=JObject.Parse(File.ReadAllText("ArtDirection/Generated/SscaryMotion-20261001/armed-grip-v2/manifest.json",System.Text.Encoding.UTF8));
        var root=new GameObject("Temporary armed grip preview");root.hideFlags=HideFlags.HideAndDontSave;
        var camera=root.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=11*1.375f;
        camera.aspect=8f/11;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.34f,.39f,.3f);
        camera.cullingMask=1<<30;camera.allowHDR=false;camera.allowMSAA=false;
        var target=new RenderTexture(1408,1936,24,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point};target.Create();camera.targetTexture=target;
        var old=RenderTexture.active;var textures=new System.Collections.Generic.List<Texture2D>();var sprites=new System.Collections.Generic.List<Sprite>();
        try
        {
            var ids=new[]{"worn_dagger","knight_sword","iron_mace","training_katana","iron_greatsword","hunter_bow","hand_crossbow","scrap_pistol","storm_rifle","scrap_shotgun","dragon_cannon"};
            for(int row=0;row<ids.Length;row++)for(int d=0;d<8;d++)
            {
                var weapon=WeaponLootTable.Find(ids[row]);bool ranged=weapon.AttackType==WeaponAttackType.Ranged;
                int clip=ranged?PlayerAnimationController.RangedClip(weapon.Family):weapon.Family==WeaponFamily.Greatsword||weapon.Family==WeaponFamily.Mace?1:0;
                JObject pose=null;foreach(JObject entry in data["frames"])if(entry.Value<int>("kind")== (ranged?5:0)&&entry.Value<int>("clip")==clip&&entry.Value<int>("direction")==new[]{0,1,2,2,3,4,4,5}[d]&&entry.Value<int>("variant")==0&&entry.Value<int>("key")== (ranged?15:0)){pose=entry;break;}
                if(pose==null)throw new Exception(ids[row]+"/"+d);
                var body=new GameObject(ids[row]+" body "+d);body.hideFlags=HideFlags.HideAndDontSave;body.layer=30;body.transform.SetParent(root.transform,false);
                body.transform.position=new Vector3(-11+(d+.5f)*2.75f,15.125f-(row+.5f)*2.75f-.28f,0);
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};texture.LoadImage(File.ReadAllBytes(pose.Value<string>("path")));textures.Add(texture);
                var sprite=Sprite.Create(texture,new Rect(0,0,48,64),new Vector2(.5f,22/64f),32,0,SpriteMeshType.FullRect);sprites.Add(sprite);var br=body.AddComponent<SpriteRenderer>();br.sprite=sprite;br.sortingOrder=2;
                var held=new GameObject("Original native held weapon");held.hideFlags=HideFlags.HideAndDontSave;held.layer=30;held.transform.SetParent(body.transform,false);
                var grip=new Vector2(pose["grip"][0].Value<float>(),pose["grip"][1].Value<float>());held.transform.localPosition=new Vector3((grip.x-24)/32,(42-grip.y)/32,0);
                var aim=new Vector2(Mathf.Sin(d*Mathf.PI/4),Mathf.Cos(d*Mathf.PI/4));NativeWeaponArt.TryGet(weapon.Id,out var landmarks);
                held.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg-(aim.x<0?-landmarks.ForwardAngle:landmarks.ForwardAngle));
                held.transform.localScale=Vector3.one*(weapon.Family==WeaponFamily.Bow?.9f:weapon.Family==WeaponFamily.Greatsword?1.25f:1);
                var wr=held.AddComponent<SpriteRenderer>();wr.sprite=PixelArtAtlas.GetHeldWeaponSprite(weapon);wr.flipY=aim.x<0;wr.sortingOrder=1;
            }
            camera.Render();RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
            var path="ArtDirection/Review-20261001/weapon-carry/native-armed-grip-v2-preview.png";File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);return Path.GetFullPath(path);
        }
        finally{RenderTexture.active=old;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(root);foreach(var sprite in sprites)UnityEngine.Object.DestroyImmediate(sprite);foreach(var texture in textures)UnityEngine.Object.DestroyImmediate(texture);}
    }
}
