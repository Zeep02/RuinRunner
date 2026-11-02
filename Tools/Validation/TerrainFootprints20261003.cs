using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using RuinRunner;
using Newtonsoft.Json.Linq;

// Inspect actual drawn paver bounds at native pixel centers against the production
// walk mask. No collision or gameplay progression is altered by this fixture.
public static class TerrainFootprints20261003
{
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        var random=UnityEngine.Random.state;
        var rows=new JArray();
        try
        {
            foreach(var seed in new[]{9100,314159,271828})
            {
                UnityEngine.Random.InitState(seed);
                var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},seed,0);
                try
                {
                    var pavers=map.Root.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.name=="Worn paving").ToArray();
                    var checks=0;var outside=0;var offGrid=0;
                    foreach(var sprite in pavers)
                    {
                        var b=sprite.bounds;
                        if(Mathf.Abs(b.size.x-.5f)>.00001f || Mathf.Abs(b.size.y-.5f)>.00001f)throw new Exception("Resampled native paving");
                        for(int y=0;y<16;y++)for(int x=0;x<16;x++)
                        {
                            var p=new Vector2(b.min.x+(x+.5f)/32,b.min.y+(y+.5f)/32);
                            if(!map.Terrain.IsOpen(p))outside++;checks++;
                        }
                        if(Mathf.Abs(b.min.x*32-Mathf.Round(b.min.x*32))>.001f || Mathf.Abs(b.min.y*32-Mathf.Round(b.min.y*32))>.001f)offGrid++;
                    }
                    if(pavers.Length==0 || outside!=0 || offGrid!=0)throw new Exception("Paver boundary/grid failure: "+seed+" outside="+outside+" offGrid="+offGrid);
                    rows.Add(new JObject{{"seed",seed},{"pavers",pavers.Length},{"variants",pavers.Select(p=>p.sprite).Distinct().Count()},{"nativePixelCentersInsideFloor",checks},{"outsideFloor",outside},{"offNativeGrid",offGrid}});
                }
                finally {map.Root.SetActive(false);UnityEngine.Object.Destroy(map.Root);}
            }
        }
        finally {UnityEngine.Random.state=random;}
        var report=new JObject{{"result","PASS"},{"scope","Actual native paver pixel-center footprints against three seeded production maps; not movement, normal campaign or performance"},{"cases",rows}};
        File.WriteAllText("F:/codex/LoglikeCap/ArtDirection/Review-20261003/terrain-composition/paver-footprints.json",report.ToString(),new UTF8Encoding(false));
        return report.ToString();
    }
}
