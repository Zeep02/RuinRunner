using System;
using UnityEngine;
using RuinRunner.Generation;

namespace RuinRunner
{
    /// <summary>Walk-through connection to the next encounter; no teleport or permanent brush wall.</summary>
    public sealed class RoomExit : MonoBehaviour
    {
        public bool IsUnlocked => unlocked;
        public bool IsConsumed => consumed;
        public bool IsGraphConnection { get; private set; }
        public string ConnectionId { get; private set; }
        public int SourceRoom { get; private set; } = -1;
        public int TargetRoom => targetRoom;
        public bool IsGateVisible => departureSeal != null && departureSeal.IsClosed || IsGraphConnection && arrivalSeal != null && arrivalSeal.IsClosed;
        public Vector2 DeparturePosition => departureSeal.transform.position;
        public Vector2 ArrivalPosition => arrivalSeal.transform.position;
        private int targetRoom;
        private Action<int> onEntered;
        private bool unlocked;
        private bool consumed;
        private EncounterBarrier departureSeal;
        private EncounterBarrier arrivalSeal;
        private Vector2 departureEntry,arrivalEntry;

        public static RoomExit CreateGraphConnection(Transform parent,GeneratedConnection edge,string theme,Action<int> enteredCallback)
        {
            var departure=GeneratedStageUnityAdapter.Vector(edge.DepartureGate);var arrival=GeneratedStageUnityAdapter.Vector(edge.ArrivalGate);
            var axis=(arrival-departure).normalized;bool vertical=Mathf.Abs(axis.y)>.5f;
            var root=new GameObject("Connection_"+edge.Id);root.transform.SetParent(parent,false);
            root.transform.position=arrival+axis*3.25f;
            var exit=root.AddComponent<RoomExit>();exit.IsGraphConnection=true;exit.ConnectionId=edge.Id;exit.SourceRoom=edge.FromIndex;exit.targetRoom=edge.ToIndex;exit.onEntered=enteredCallback;
            exit.departureEntry=departure-axis*3.25f;exit.arrivalEntry=arrival+axis*3.25f;
            exit.departureSeal=EncounterBarrier.Create(parent,departure,vertical,false,edge.Width,theme);
            exit.arrivalSeal=EncounterBarrier.Create(parent,arrival,vertical,false,edge.Width,theme);
            exit.CreateEntryTrigger(root.transform,exit.departureEntry,vertical,edge.Width,edge.FromIndex);
            exit.CreateEntryTrigger(root.transform,exit.arrivalEntry,vertical,edge.Width,edge.ToIndex);
            exit.unlocked=true;return exit;
        }
        private void CreateEntryTrigger(Transform parent,Vector2 point,bool vertical,float width,int destination)
        {
            var obj=new GameObject("Region entry "+destination);obj.transform.SetParent(parent,false);obj.transform.position=point;
            var trigger=obj.AddComponent<BoxCollider2D>();trigger.isTrigger=true;trigger.size=vertical?new Vector2(width,1):new Vector2(1,width);
            obj.AddComponent<RegionEntryTrigger>().Initialize(this,destination);
        }
        internal void TryEnterGraph(Collider2D other,int destination)
        {
            if(!unlocked||other.GetComponent<PlayerController>()==null)return;
            // The controller owns adjacency/current-state checks. Revisiting a cleared region never consumes this connection.
            onEntered?.Invoke(destination);
        }
        public bool Connects(int first,int second)=>IsGraphConnection&&(SourceRoom==first&&targetRoom==second||SourceRoom==second&&targetRoom==first);
        public Vector2 GetEntryPoint(int destination)=>IsGraphConnection?(destination==SourceRoom?departureEntry:arrivalEntry):(Vector2)transform.position;
        public void SetGraphClosed(bool value)
        {if(!IsGraphConnection)return;departureSeal.SetClosed(value);arrivalSeal.SetClosed(value);unlocked=!value;consumed=false;}

        public static RoomExit Create(Transform parent, Vector2 position, RoomTravelDirection direction, int nextRoom, Action<int> enteredCallback)
            => CreateConnection(parent, position, position, direction, nextRoom, enteredCallback);

        public static RoomExit CreateConnection(Transform parent, Vector2 departure, Vector2 arrival, RoomTravelDirection direction, int nextRoom, Action<int> enteredCallback,
            float width = StageTerrainLayout.CorridorWidth, bool inset = true, Vector2 arrivalAxis = default)
        {
            var axis = direction == RoomTravelDirection.Right ? Vector2.right : direction == RoomTravelDirection.Left ? Vector2.left :
                direction == RoomTravelDirection.Up ? Vector2.up : Vector2.down;
            var vertical = Mathf.Abs(axis.y) > .5f;
            if (arrivalAxis.sqrMagnitude < .5f) arrivalAxis = axis;
            arrivalAxis.Normalize();
            var arrivalVertical = Mathf.Abs(arrivalAxis.y) > .5f;
            var root = new GameObject("EncounterEntrance" + nextRoom); root.transform.SetParent(parent, false);
            root.transform.position = arrival + arrivalAxis * 1.6f;
            var trigger = root.AddComponent<BoxCollider2D>(); trigger.isTrigger = true;
            trigger.size = arrivalVertical ? new Vector2(width, .6f) : new Vector2(.6f, width);
            var exit = root.AddComponent<RoomExit>(); exit.targetRoom = nextRoom; exit.onEntered = enteredCallback;
            exit.SourceRoom=nextRoom-1;
            // Seal the narrow passage beyond the region contour, not inside
            // the wider clearing where actors could walk around its ends.
            exit.departureSeal = EncounterBarrier.Create(parent, departure + axis * (inset ? .8f : 0), vertical, true, width);
            exit.arrivalSeal = EncounterBarrier.Create(parent, arrival - arrivalAxis * (inset ? .8f : 0), arrivalVertical, false, width);
            return exit;
        }

        public void Unlock() { unlocked = true; departureSeal.SetClosed(false); }
        public void SealEntrance() { arrivalSeal.SetClosed(true); }
        public void ReleaseEntrance() { arrivalSeal.SetClosed(false); }
        public void ResetExit()
        {
            if(IsGraphConnection){SetGraphClosed(false);return;}
            unlocked = false; consumed = false;
            departureSeal.SetClosed(true); arrivalSeal.SetClosed(false);
        }
        private void OnTriggerEnter2D(Collider2D other) => TryEnter(other);
        private void OnTriggerStay2D(Collider2D other) => TryEnter(other);
        private void TryEnter(Collider2D other)
        {
            if (!unlocked || consumed || other.GetComponent<PlayerController>() == null) return;
            consumed = true;
            if (StageValidationInput.Enabled) Debug.Log("[Portal] Continuous encounter entry target=" + targetRoom);
            onEntered?.Invoke(targetRoom);
        }
    }
    public sealed class RegionEntryTrigger : MonoBehaviour
    {
        private RoomExit owner;private int destination;
        public void Initialize(RoomExit connection,int region){owner=connection;destination=region;}
        private void OnTriggerEnter2D(Collider2D other)=>owner.TryEnterGraph(other,destination);
        private void OnTriggerStay2D(Collider2D other)=>owner.TryEnterGraph(other,destination);
    }
}
