using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RuinRunner.Generation
{
    public static class ProceduralStageGenerator
    {
        public const string Version = "modules-v1.0.0";
        public const int MaximumAttempts = 12;
        public const float HorizontalPitch = 48, VerticalPitch = 36, PassageWidth = 6;
        static readonly int[] DX={1,0,-1,0}, DY={0,1,0,-1};

        // Explicit seeds deliberately do not consult history, wall time, or Unity's global random state.
        public static GeneratedStage Generate(int seed,int stage)
        {
            if(stage<0||stage>2)throw new ArgumentOutOfRangeException(nameof(stage));
            var diagnostics=new GenerationDiagnostics();
            for(int attempt=0;attempt<MaximumAttempts;attempt++)
            {
                diagnostics.Attempts=attempt+1;
                try
                {
                    var candidate=Build(seed,stage,attempt,false); candidate.Diagnostics=diagnostics;
                    var validation=GeneratedStageGeometry.Validate(candidate,false);
                    if(validation.Errors.Count==0){diagnostics.ValidationSummary=validation.Summary;return candidate;}
                    diagnostics.RejectedAttempts.Add("attempt "+attempt+": "+string.Join("; ",validation.Errors));
                }
                catch(InvalidOperationException exception){diagnostics.RejectedAttempts.Add("attempt "+attempt+": "+exception.Message);}
            }
            // A known non-overlapping graph still uses this seed's modules, so failure never quietly selects one global fixed map.
            diagnostics.UsedFallback=true;
            var fallback=Build(seed,stage,MaximumAttempts,true);fallback.Diagnostics=diagnostics;
            var fallbackValidation=GeneratedStageGeometry.Validate(fallback,false);
            if(fallbackValidation.Errors.Count>0)throw new InvalidOperationException("Verified fallback invalid: "+string.Join("; ",fallbackValidation.Errors));
            diagnostics.ValidationSummary=fallbackValidation.Summary;return fallback;
        }

        // Only ordinary new expeditions call this. Persist the returned seed, signature, and version before entering the stage.
        public static GeneratedStage ChooseFreshSeed(Func<int> nextSeed,int stage,ISet<string> recentSignatures,int candidateLimit=8)
        {
            if(nextSeed==null)throw new ArgumentNullException(nameof(nextSeed));
            if(candidateLimit<1||candidateLimit>32)throw new ArgumentOutOfRangeException(nameof(candidateLimit));
            GeneratedStage result=null;
            for(int i=0;i<candidateLimit;i++)
            { result=Generate(nextSeed(),stage);if(recentSignatures==null||!recentSignatures.Contains(result.StructureSignature))return result; }
            result.Diagnostics.RepeatedRecentStructure=true;return result;
        }

        static GeneratedStage Build(int seed,int stage,int attempt,bool fallback)
        {
            var random=new GenerationRandom(seed,stage,GenerationStream.Layout,attempt);
            var regions=new List<GeneratedRegion>();var edges=new List<GeneratedConnection>();var occupied=new HashSet<long>();
            var modules=SpatialModuleCatalog.ForStage(stage);var bag=new List<int>();
            for(int i=0;i<modules.Count;i++)bag.Add(i);random.Shuffle(bag);int moduleCursor=0;
            int count=fallback?6:5+random.Next(2),gx=0,gy=0;
            for(int i=0;i<count;i++)
            {
                if(i>0)
                {
                    int direction=0;
                    if(!fallback)
                    {
                        var available=new List<int>();
                        for(int d=0;d<4;d++)
                        {
                            int nx=gx+DX[d],ny=gy+DY[d];
                            if(Math.Abs(nx)>3||Math.Abs(ny)>2||occupied.Contains(SpatialModuleCatalog.Key(nx,ny)))continue;
                            // Preserve room for at least two side branches around the early mandatory combats.
                            int adjacent=0;for(int side=0;side<4;side++)if(occupied.Contains(SpatialModuleCatalog.Key(nx+DX[side],ny+DY[side])))adjacent++;
                            if(adjacent==1)available.Add(d);
                        }
                        if(available.Count==0)throw new InvalidOperationException("Main path exhausted compatible sockets");
                        direction=available[random.Next(available.Count)];
                    }
                    gx+=DX[direction];gy+=DY[direction];
                }
                var role=i==0?RegionRole.Entrance:i==count-1?RegionRole.Boss:RegionRole.Combat;
                var region=CreateRegion(stage,regions.Count,gx,gy,role,modules[bag[moduleCursor++%bag.Count]],i,null);
                regions.Add(region);occupied.Add(SpatialModuleCatalog.Key(gx,gy));
                if(i>0)Connect(stage,regions[i-1],region,true,edges);
            }
            // Merchant follows the first mandatory combat. Optional services never block the main route.
            AddBranch(1,RegionRole.Merchant,"merchant");
            AddBranch(2,RegionRole.FieldUpgrade,stage==0?"form-anvil":stage==1?"oath-stone":"exchange-altar");
            AddBranch(count-2,RegionRole.Reward,"reward-cache");
            // A second service supplies vows before the next region and allows every expedition to see all three object types.
            AddBranch(1+random.Next(count-2),RegionRole.FieldUpgrade,stage==0?"oath-stone":stage==1?"exchange-altar":"form-anvil");
            var result=new GeneratedStage{GeneratorVersion=Version,Seed=seed,StageIndex=stage,Regions=regions.ToArray(),Connections=edges.ToArray(),EntranceId=regions[0].Id,BossId=regions[count-1].Id};
            result.StructureSignature=Signature(result);return result;

            void AddBranch(int preferred,RegionRole role,string service)
            {
                var parents=new List<int>{preferred};
                for(int p=1;p<count-1;p++)if(p!=preferred)parents.Add(p);
                foreach(int p in parents)
                {
                    var parent=regions[p];var directions=new List<int>{0,1,2,3};random.Shuffle(directions);
                    foreach(int d in directions)
                    {
                        int x=parent.GridX+DX[d],y=parent.GridY+DY[d];
                        if(occupied.Contains(SpatialModuleCatalog.Key(x,y)))continue;
                        // Non-connected neighboring rooms remain separated by at least six units of solid exterior.
                        if(!fallback&&(Math.Abs(x)>3||Math.Abs(y)>3))continue;
                        var branch=CreateRegion(stage,regions.Count,x,y,role,modules[bag[moduleCursor++%bag.Count]],-1,service);
                        regions.Add(branch);occupied.Add(SpatialModuleCatalog.Key(x,y));Connect(stage,parent,branch,false,edges);return;
                    }
                }
                throw new InvalidOperationException("No compatible branch socket for "+service);
            }
        }
        static GeneratedRegion CreateRegion(int stage,int index,int x,int y,RegionRole role,ModuleDefinition module,int order,string service)
            =>new GeneratedRegion{Id="s"+stage+"-r"+index.ToString("D2",CultureInfo.InvariantCulture),Index=index,ModuleId=module.Id,Module=module,GridX=x,GridY=y,
                Center=new MapPoint(x*HorizontalPitch,y*VerticalPitch),Role=role,MainPathOrder=order,Service=service??string.Empty};
        static void Connect(int stage,GeneratedRegion a,GeneratedRegion b,bool main,List<GeneratedConnection> edges)
        {
            int dx=b.GridX-a.GridX,dy=b.GridY-a.GridY;
            if(Math.Abs(dx)+Math.Abs(dy)!=1)throw new InvalidOperationException("Connection is not a neighboring socket");
            var direction=dx>0?CardinalPort.East:dx<0?CardinalPort.West:dy>0?CardinalPort.North:CardinalPort.South;
            var opposite=(CardinalPort)(((int)direction+2)%4);var axis=new MapPoint(dx,dy);
            var from=a.Center+a.Module.Port(direction);var to=b.Center+b.Module.Port(opposite);
            var edge=new GeneratedConnection{Id="s"+stage+"-e"+edges.Count.ToString("D2",CultureInfo.InvariantCulture),From=a.Id,To=b.Id,FromIndex=a.Index,ToIndex=b.Index,
                FromPort=direction,ToPort=opposite,Points=new[]{from-axis*1.5f,to+axis*1.5f},DepartureGate=from+axis*1.5f,ArrivalGate=to-axis*1.5f,Width=PassageWidth,MainPath=main};
            a.Connections.Add(edge.Id);b.Connections.Add(edge.Id);a.Neighbors.Add(b.Id);b.Neighbors.Add(a.Id);edges.Add(edge);
        }
        static string Signature(GeneratedStage stage)
        {
            var text=new StringBuilder(Version).Append('|').Append(stage.StageIndex);
            foreach(var r in stage.Regions)text.Append('|').Append(r.ModuleId).Append(':').Append((int)r.Role).Append(':').Append(r.Service).Append('@').Append(r.GridX).Append(',').Append(r.GridY);
            foreach(var e in stage.Connections)text.Append('|').Append(e.FromIndex).Append('>').Append(e.ToIndex);
            unchecked { ulong hash=14695981039346656037UL;foreach(char c in text.ToString()){hash^=c;hash*=1099511628211UL;}return hash.ToString("x16",CultureInfo.InvariantCulture); }
        }
    }
}
