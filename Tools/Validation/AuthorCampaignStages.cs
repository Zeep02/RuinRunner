using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Author text assets through the connected Editor; original dungeon_demo is retained.
public static class AuthorCampaignStages
{
    public static string Main()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring geography.");
        var outer = JsonUtility.FromJson<StageLayoutDefinition>(Resources.Load<TextAsset>("Stages/dungeon_demo").text);
        outer.regions[0].name = "무너진 성문";
        outer.regions[1].name = "이끼 낀 중정";
        outer.regions[2].name = "감시자의 서고";
        outer.exploration = new[] { Path(4, -8,4, -16,4, -16,13, -9,13) };
        outer.explorationRewards = new[] { Reward("성벽의 숨은 보급함", 0, -9,13) };
        Save("outer_ruins", outer);

        var halls = new StageLayoutDefinition
        {
            regions = new[]
            {
                Region("잠긴 나루", 0,0, 24,16, Points(-12,-8, 5,-8, 5,-5, 12,-5, 12,5, 5,5, 5,8, -5,8, -5,5, -12,5), Points(-4,-2, 5,1, 1,4)),
                Region("갈라진 수로", 10,29, 26,22, Points(-13,-6, -6,-6, -6,-11, 6,-11, 6,-7, 13,-7, 13,7, 7,7, 7,11, -9,11, -9,6, -13,6), Points(-6,-3, 7,-4, -5,5, 7,5), Pool(3,1, 3,5)),
                Region("수문 제어실", 50,43, 22,22, Points(-11,-5, -5,-5, -5,-11, 5,-11, 5,-7, 11,-7, 11,7, 6,7, 6,11, -6,11, -6,5, -11,5), Points(-4,-3, 5,2, -3,6, 4,-6)),
                Region("조수의 원형 제단", 60,3, 28,24, Points(-14,-6, -10,-6, -10,-10, -6,-10, -6,-12, 6,-12, 6,-10, 10,-10, 10,-6, 14,-6, 14,6, 10,6, 10,10, 6,10, 6,12, -6,12, -6,10, -10,10, -10,6, -14,6), Points(0,0))
            },
            connections = new[] { Path(4, 0,8, 0,13, 10,13, 10,18), Path(4, 23,29, 31,29, 31,43, 39,43), Path(4, 50,32, 50,24, 60,24, 60,15) },
            exploration = new[] { Path(4, -3,29, -10,29, -10,39) },
            explorationRewards = new[] { Reward("잊힌 등대지기의 함", 1, -10,39) }, trails = Array.Empty<StagePathData>()
        };
        Save("flooded_halls", halls);
        var core = new StageLayoutDefinition
        {
            regions = new[]
            {
                Region("봉인의 문턱", 0,0, 24,18, Points(-12,-6, -6,-6, -6,-9, 8,-9, 8,-5, 12,-5, 12,5, 6,5, 6,9, -8,9, -8,6, -12,6), Points(-4,-3, 4,3, 7,-3), Pool(0,5, 3,3)),
                Region("수호자의 교차로", 38,13, 24,20, Points(-12,-5, -7,-5, -7,-10, 7,-10, 7,-6, 12,-6, 12,6, 5,6, 5,10, -5,10, -5,6, -12,6), Points(-5,-3, 5,3, 0,6, 4,-5), Pool(0,0, 3,3)),
                Region("무너진 별의 회랑", 24,47, 24,22, Points(-12,-5, -6,-5, -6,-11, 6,-11, 6,-7, 12,-7, 12,6, 6,6, 6,11, -6,11, -6,5, -12,5), Points(-4,-3, 5,2, 0,6, 5,-5)),
                Region("마지막 봉인", -18,61, 28,26, Points(-14,-7, -9,-7, -9,-13, 9,-13, 9,-7, 14,-7, 14,7, 9,7, 9,13, -9,13, -9,7, -14,7), Points(0,0))
            },
            connections = new[] { Path(4, 12,0, 18,0, 18,13, 26,13), Path(4, 38,23, 38,29, 24,29, 24,36), Path(4, 12,47, 4,47, 4,61, -4,61) },
            exploration = new[] { Path(4, 24,56, 24,64, 33,64) },
            explorationRewards = new[] { Reward("봉인술사의 유품", 2, 33,64) }, trails = Array.Empty<StagePathData>()
        };
        Save("seal_core", core);
        AssetDatabase.Refresh();
        return "Authored 3 campaign maps with curved connections and one optional branch each; originals retained. Run geography validation next.";
    }
    private static Vector2[] Points(params float[] xy)
    {
        var p = new Vector2[xy.Length / 2]; for (var i=0;i<p.Length;i++) p[i]=new Vector2(xy[i*2],xy[i*2+1]); return p;
    }
    private static StagePathData Path(float width, params float[] xy) => new StagePathData { width=width, points=Points(xy) };
    private static ExplorationRewardData Reward(string name, int region, float x,float y) => new ExplorationRewardData {name=name, region=region, position=new Vector2(x,y)};
    private static RoomPlatformData Pool(float x,float y,float w,float h) => new RoomPlatformData { position=new Vector2(x,y), size=new Vector2(w,h) };
    private static StageRegionData Region(string name,float x,float y,float w,float h,Vector2[] outline,Vector2[] spawns,params RoomPlatformData[] pools)
        => new StageRegionData {name=name,center=new Vector2(x,y),size=new Vector2(w,h),outline=outline,spawns=spawns,land=pools};
    private static void Save(string id,StageLayoutDefinition map)
    {
        var path="Assets/RuinRunner/Resources/Stages/"+id+".json";
        File.WriteAllText(path,JsonUtility.ToJson(map,true)); AssetDatabase.ImportAsset(path);
    }
}
