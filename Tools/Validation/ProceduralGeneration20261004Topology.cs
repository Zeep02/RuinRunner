using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RuinRunner;
using RuinRunner.Generation;
using UnityEngine;

// Supplied-state topology test. This does not award a normal campaign PASS.
public static class ProceduralGeneration20261004Topology
{
    public static object Main()
    {
        DungeonMap map=null;var checks=new List<string>();
        try
        {
            map=DungeonMap.BuildStage(_=>{},2026100400,0);
            Require(map.IsProcedural&&map.BossRoomIndex!=map.RoomCount-1,"explicit boss index",checks);
            Require(!map.BossPrerequisitesCleared,"boss prerequisites initially closed",checks);
            foreach(int neighbor in map.GetNeighbors(map.BossRoomIndex))
                Require(!map.CanEnter(neighbor,map.BossRoomIndex)&&map.GetConnectionBetween(neighbor,map.BossRoomIndex).IsGateVisible,"boss entry sealed until mandatory combats clear",checks);
            int combat=-1;
            for(int i=0;i<map.RoomCount;i++)if(map.RequiresCombat(i)&&i!=map.BossRoomIndex){combat=i;break;}
            map.MarkVisited(map.EntranceRoomIndex);map.UnlockExit(map.EntranceRoomIndex);map.MarkVisited(combat);map.BeginEncounter(combat);
            int gates=0;
            foreach(int neighbor in map.GetNeighbors(combat))
            {Require(map.GetConnectionBetween(combat,neighbor).IsGateVisible,"all current encounter entrances seal",checks);gates++;}
            Require(gates>=2,"sample tests multiple entrances",checks);
            map.UnlockExit(combat);int visited=map.VisitedRegionCount;map.MarkVisited(combat);
            Require(map.VisitedRegionCount==visited&&map.Regions[combat].Phase==EncounterRegionPhase.Cleared,"revisit preserves clear and counts visit once",checks);
            int branch=-1;foreach(int neighbor in map.GetNeighbors(combat))if(!map.RequiresCombat(neighbor)&&neighbor!=map.EntranceRoomIndex){branch=neighbor;break;}
            Require(branch>=0&&map.CanEnter(combat,branch),"optional branch accessible",checks);
            map.MarkVisited(branch);map.UnlockExit(branch);Require(map.CanEnter(branch,combat),"optional branch returns to cleared combat",checks);
            for(int i=0;i<map.RoomCount;i++)if(map.RequiresCombat(i)&&i!=map.BossRoomIndex)map.UnlockExit(i);
            Require(map.BossPrerequisitesCleared,"all mandatory combats unlock boss",checks);
            foreach(int neighbor in map.GetNeighbors(map.BossRoomIndex))Require(map.CanEnter(neighbor,map.BossRoomIndex)&&!map.GetConnectionBetween(neighbor,map.BossRoomIndex).IsGateVisible,"boss passage opens",checks);
            map.BeginEncounter(map.BossRoomIndex);foreach(int neighbor in map.GetNeighbors(map.BossRoomIndex))Require(map.GetConnectionBetween(neighbor,map.BossRoomIndex).IsGateVisible,"boss entrance recloses during encounter",checks);
            map.UnlockExit(map.BossRoomIndex);Require(StageNavigation.IsClear(map.Terrain,map.StageExitPosition),"exit belongs to boss region",checks);
            Physics2D.SyncTransforms();
            for(int i=0;i<map.ConnectionCount;i++)
            {
                var gate=map.GetConnection(i);Require(gate.IsGraphConnection&&!gate.IsConsumed,"graph gates remain revisitable",checks);
                Require(StageNavigation.IsClear(map.Terrain,gate.GetEntryPoint(gate.SourceRoom))&&StageNavigation.IsClear(map.Terrain,gate.GetEntryPoint(gate.TargetRoom)),"entry triggers land inside floor",checks);
            }
            string output=Path.GetFullPath("Logs/Validation/2026-10-04-procedural-generation/runtime-topology.txt");Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output,"PASS supplied-state topology fixture; not normal play.\n"+string.Join("\n",checks),new UTF8Encoding(false));
            return new{passed=true,checks=checks.Count,output};
        }
        finally{if(map?.Root!=null)UnityEngine.Object.DestroyImmediate(map.Root);}
    }
    static void Require(bool condition,string message,List<string> checks){if(!condition)throw new InvalidOperationException(message);checks.Add(message);}
}
