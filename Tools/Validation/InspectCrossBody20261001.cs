using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using RuinRunner;

public static class InspectCrossBody20261001
{
    public static object Main()
    {
        var samples=new JArray();
        for(int direction=0;direction<8;direction++)for(int clip=0;clip<2;clip++)for(int key=0;key<16;key++)
        {
            Vector2 hand;bool behind;var sprite=SscaryShortBladeFrames.Sample(clip,direction,0,key,out hand,out behind);
            samples.Add(new JObject{{"direction",direction},{"clip",clip},{"key",key},{"hand",new JArray(hand.x,hand.y)},{"behind",behind},{"source",AssetDatabase.GetAssetPath(sprite)}});
        }
        Directory.CreateDirectory("ArtDirection/Review-20261001/cross-cut");
        File.WriteAllText("ArtDirection/Review-20261001/cross-cut/existing-short-body-sockets.json",samples.ToString(),new UTF8Encoding(false));
        return new {nativeSamples=samples.Count,scope="Read-only existing true native forward/reverse body poses and sockets; not newly generated art or accepted cross-cut motion"};
    }
}
