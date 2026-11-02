using System;
using System.IO;
using UnityEngine;
using RuinRunner;

public static class StageRasterRegression
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Use Edit Mode");
        var definition=StageLayoutDefinition.Load(1);
        var rooms=definition.CreateEncounterRooms(RoomTemplateCatalog.CreateRandomRun(),9100,1);
        var centers=Array.ConvertAll(definition.regions,r=>r.center);
        var terrain=new StageTerrainLayout(rooms,centers,definition.connections,definition.trails,definition.exploration);
        terrain.SnapToDungeonTiles();
        var center=centers[1]+new Vector2(3,1);var checks=0;var blocked=0;
        try
        {
            // Independently evaluate the authored 3x5 rounded rectangle at symmetric half-unit centers.
            for(var y=-3.5f;y<=3.5f;y++)for(var x=-2.5f;x<=2.5f;x++)
            {
                var shouldBeOpen=Mathf.Pow(Mathf.Abs(x)/1.5f,4)+Mathf.Pow(Mathf.Abs(y)/2.5f,4)>=1;
                var point=center+new Vector2(x,y);
                if(terrain.IsOpen(point)!=shouldBeOpen)throw new Exception("Raster mismatch at "+point);
                if(!terrain.IsOpen(point))blocked++;
                if(terrain.IsOpen(point)!=terrain.IsOpen(center+new Vector2(-x,-y)))throw new Exception("Asymmetric obstacle at "+point);
                checks+=2;
                foreach(var offset in new[]{new Vector2(-.4f,-.4f),new Vector2(.4f,-.4f),new Vector2(-.4f,.4f),new Vector2(.4f,.4f)})
                {if(terrain.IsOpen(point+offset)!=shouldBeOpen)throw new Exception("Nonuniform tile at "+point);checks++;}
            }
            if(blocked!=8)throw new Exception("3x5 obstacle expected eight blocked tile centers, found "+blocked);
            var result="[Stage Raster] PASS "+checks+" exact-center, symmetry and within-tile checks; flooded 3x5 obstacle occupies eight cells. No gameplay completion claim.";
            File.WriteAllText("Logs/Validation/2026-09-20-environment/raster-regression.txt",result);return result;
        }
        finally {foreach(var room in rooms)UnityEngine.Object.DestroyImmediate(room);}
    }
}
