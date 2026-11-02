using UnityEngine;

namespace RuinRunner
{
    /// <summary>Stone emitters project a temporary, animated encounter seal.</summary>
    public sealed class EncounterBarrier : MonoBehaviour
    {
        private LineRenderer core,glow;
        private Vector2 tangent;
        private Material material;
        private BoxCollider2D blocker;
        private SpriteRenderer[] sparks,bolts;
        private Sprite[] sparkFrames,boltFrames;
        private float width,intensity;
        private bool closed;
        public bool IsClosed => closed;
        public static EncounterBarrier Create(Transform parent, Vector2 position, bool verticalTravel, bool closed, float width = StageTerrainLayout.CorridorWidth, StageTerrainLayout terrain = null)
        {
            var root = new GameObject("EncounterSeal"); root.transform.SetParent(parent, false); root.transform.position = position;
            root.layer = GameLayers.Ground;
            var barrier = root.AddComponent<EncounterBarrier>();
            barrier.width = width;barrier.closed=closed;barrier.intensity=closed?1:0;
            barrier.tangent = verticalTravel ? Vector2.right : Vector2.up;
            barrier.blocker = root.AddComponent<BoxCollider2D>();
            barrier.blocker.size = verticalTravel ? new Vector2(width + .4f, .3f) : new Vector2(.3f, width + .4f);
            barrier.blocker.enabled=closed;
            barrier.material=new Material(Shader.Find("Sprites/Default"));
            barrier.core=barrier.Ribbon("Seal core",.035f);barrier.glow=barrier.Ribbon("Seal halo",.14f);
            barrier.sparkFrames=IndividualArt.Frames("External/NinjaAdventure/Combat/SealSpark",9);
            barrier.boltFrames=IndividualArt.Frames("External/NinjaAdventure/Combat/SealBolt",8);
            barrier.sparks=new SpriteRenderer[2];
            for(var i=0;i<2;i++)
            {
                var foot=barrier.tangent*((i==0?-1:1)*(width*.5f+.13f));
                // A generated passage does not automatically acquire two unsupported lanterns in the void.
                // Leave the existing energy ribbon when no real floor can support an emitter footprint.
                if(terrain!=null&&!SupportsEmitter(terrain,position+foot))continue;
                var post=barrier.MakeSprite("Stone seal emitter",CainosRuinsArt.Sprite("Props/lantern"),foot);
                post.sortingOrder=900-Mathf.RoundToInt((position.y+foot.y)*100);
                barrier.sparks[i]=barrier.MakeSprite("Emitter light",barrier.sparkFrames[0],foot+Vector2.up*.55f);
                barrier.sparks[i].transform.localScale=Vector3.one*.65f;
            }
            var count=Mathf.Max(2,Mathf.CeilToInt(width/1.2f));barrier.bolts=new SpriteRenderer[count];
            for(var i=0;i<count;i++)
            {
                var point=barrier.tangent*((i+.5f)/count-.5f)*width;
                barrier.bolts[i]=barrier.MakeSprite("Seal energy",barrier.boltFrames[0],point);
                barrier.bolts[i].transform.localRotation=Quaternion.Euler(0,0,verticalTravel?-45f:45f);
                barrier.bolts[i].transform.localScale=Vector3.one*.7f;
            }
            barrier.UpdateRibbon();
            return barrier;
        }
        private static bool SupportsEmitter(StageTerrainLayout terrain,Vector2 foot)
        {
            for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                if(!terrain.IsOpen(foot+new Vector2(x*.36f,y*.22f)))return false;
            return true;
        }
        private LineRenderer Ribbon(string name,float thickness)
        {
            var obj=new GameObject(name);obj.transform.SetParent(transform,false);var line=obj.AddComponent<LineRenderer>();
            line.useWorldSpace=false;line.positionCount=17;line.widthMultiplier=thickness;line.sharedMaterial=material;line.sortingOrder=1100;return line;
        }
        private SpriteRenderer MakeSprite(string name,Sprite sprite,Vector2 point)
        {
            var obj=new GameObject(name);obj.transform.SetParent(transform,false);obj.transform.localPosition=point;
            var view=obj.AddComponent<SpriteRenderer>();view.sprite=sprite;view.sortingOrder=1102;return view;
        }
        public void SetClosed(bool value)
        {
            closed=value;blocker.enabled=value;
            // The physical passage opens immediately. The seal briefly fades,
            // leaving its structural emitters attached to the wall ends.
        }
        private void Update()
        {
            if(Time.timeScale<=0f || !closed&&intensity<=0)return;
            intensity=Mathf.MoveTowards(intensity,closed?1:0,Time.deltaTime*7);UpdateRibbon();
        }
        private void UpdateRibbon()
        {
            var normal = new Vector2(-tangent.y, tangent.x);
            for (var i = 0; i < 17; i++)
            {
                var along=(i/16f-.5f)*width;
                var ripple=Mathf.Round(Mathf.Sin(i*.9f+Time.time*7)*Mathf.Sin(i/16f*Mathf.PI)*2)/32;
                var point=tangent*along+normal*ripple;core.SetPosition(i,point);glow.SetPosition(i,point);
            }
            core.startColor=core.endColor=new Color(1,.89f,.58f,intensity*.85f);
            glow.startColor=glow.endColor=new Color(1,.62f,.2f,intensity*.16f);
            core.enabled=glow.enabled=intensity>.001f;
            for(var i=0;i<sparks.Length;i++)
            {if(sparks[i]==null)continue;sparks[i].sprite=sparkFrames[(Mathf.FloorToInt(Time.time*12)+i*3)%sparkFrames.Length];sparks[i].color=new Color(1,1,1,intensity);}
            for(var i=0;i<bolts.Length;i++)
            {bolts[i].sprite=boltFrames[(Mathf.FloorToInt(Time.time*14)+i*3)%boltFrames.Length];bolts[i].color=new Color(1,1,1,intensity*.8f);}
        }
        private void OnDestroy() { if (material != null) { if(Application.isPlaying)Destroy(material);else DestroyImmediate(material); } }
    }
}
