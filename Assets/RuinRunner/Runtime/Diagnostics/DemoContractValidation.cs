using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    // Entry point retained for existing build automation.
    public static class DemoContractValidation
    {
        public static bool Validate(DungeonMap map)
        {
            try
            {
                Require(RunContentValidation.Validate(),"full deterministic equipment catalog");
                Require(map != null && map.IsProcedural,"normal run uses generated terrain");
                var bosses=new HashSet<EnemyType>();
                for(var stage=0;stage<CampaignStage.Count;stage++)
                {
                    var graph=RuinRunner.Generation.ProceduralStageGenerator.Generate(9100,stage);
                    Require(RuinRunner.Generation.GeneratedStageGeometry.Validate(graph,false).Errors.Count==0,"connected generated stage "+stage);
                    Require(bosses.Add(CampaignStage.Get(stage).BossType),"different guardian "+stage);
                    foreach(var region in graph.Regions)
                    {
                        bool boss=region.Role==RuinRunner.Generation.RegionRole.Boss;
                        if(!boss&&region.Role!=RuinRunner.Generation.RegionRole.Combat)continue;
                        var encounter=RunContent.Encounters(9100,region.Index,boss,stage);
                        Require(encounter.Length>0,"nonempty encounter");
                        foreach(var placement in encounter)
                            Require(EnemyRoles.IsBoss(placement.type)==boss,"boss restricted to boss role");
                        if(boss)Require(encounter.Length==1 && encounter[0].type==CampaignStage.Get(stage).BossType,"correct guardian");
                    }
                }
                foreach(var weapon in WeaponLootTable.All)
                {
                    Require(ValidIcon(InventoryIconFactory.Get(weapon.Id,weapon.Color,InventoryIconKind.Weapon)),"icon "+weapon.Id);
                    var held=PixelArtAtlas.GetHeldWeaponSprite(weapon);
                    Require(held!=null && held.texture.filterMode==FilterMode.Point && held.rect.size==new Vector2(held.texture.width,held.texture.height),"individual held weapon "+weapon.Id);
                }
                foreach(var accessory in AccessoryLootTable.All)
                    Require(ValidIcon(InventoryIconFactory.Get(accessory.Id,accessory.Color,InventoryIconKind.Accessory)),"icon "+accessory.Id);
                var checkedFrames=0;
                foreach(var direction in new[]{"up","right_up","right_down","down","left_down","left_up"})
                foreach(var action in new[]{"idle","walk","dash","death"})
                {
                    for(var frame=0;frame<8;frame++)
                    {
                        var path=PlayerAnimationController.ArtRoot+action+"/"+direction+"/"+frame.ToString("D2");
                        Require(ValidHero(IndividualArt.Sprite(path),IndividualArt.Texture(path)),"native 48x64/32PPU/fixed foot "+path);
                        checkedFrames++;
                    }
                }
                Require(checkedFrames==192,"all selected native source frames");
                foreach(var retired in new[]{"HeroMotion/idle/04/00","HeroWalk/move/04/00","Player/Legacy/idle/00"})
                    Require(!IndividualArt.Exists(retired),"retired hero excluded from runtime "+retired);
                var first=new PlayerStats();var second=new PlayerStats();
                for(var hit=0;hit<100;hit++)Require(first.RollCritical()==second.RollCritical(),"critical sequence");
                Debug.Log("[Content/Art Contract] PASS three generated stage graphs, distinct guardians, active equipment icons and 192 native base hero frames. Data contract, not normal completion or full animation review.");
                return true;
            }
            catch(Exception error) { Debug.LogError("[Content/Art Contract] FAIL "+error); return false; }
        }
        private static void Require(bool passed,string name) { if(!passed)throw new InvalidOperationException(name); }
        private static bool ValidIcon(Texture2D texture)
            => texture!=null && texture.filterMode==FilterMode.Point && texture.mipmapCount==1 && texture.width>0 && texture.height>0;
        private static bool ValidHero(Sprite sprite,Texture2D texture)
        {
            if(sprite==null || sprite.pivot!=new Vector2(24,22) || sprite.pixelsPerUnit!=32) return false;
            if(!texture.isReadable || texture.width!=48 || texture.height!=64 || texture.filterMode!=FilterMode.Point || texture.mipmapCount!=1) return false;
            var pixels=texture.GetPixels32();var visible=0;
            for(var y=0;y<64;y++)for(var x=0;x<48;x++)
            {
                var alpha=pixels[y*48+x].a;
                if(alpha!=0 && alpha!=255)return false;
                if((x==0 || y==0 || x==47 || y==63) && alpha!=0)return false;
                if(alpha!=0)visible++;
            }
            return visible>0;
        }
    }
}

