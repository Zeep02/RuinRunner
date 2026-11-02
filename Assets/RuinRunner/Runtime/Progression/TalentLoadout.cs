using System;
using System.Collections.Generic;

namespace RuinRunner
{
    // Published IDs are persistence contracts; enum ordinals are never recycled.
    public enum TalentId { ComboMemory=1, StoredGuard=2, CrossChamber=3, TurningDash=4, BrakeDash=5, ReturnDash=6, SurveyNext=7, SupplyChoice=8, ReturnTrail=9, ChestIntent=10, MerchantBarter=11, ReserveOffer=12 }
    public enum TalentFamily { Combat, Mobility, Exploration, Reward }
    public sealed class TalentDefinition
    {
        public readonly TalentId Type; public readonly string Id, Name, Summary; public readonly TalentFamily Family;
        public TalentDefinition(TalentId type,string id,string name,TalentFamily family,string summary)
        { Type=type;Id=id;Name=name;Family=family;Summary=summary; }
    }
    public static class TalentCatalog
    {
        public static readonly TalentDefinition[] All={
            new TalentDefinition(TalentId.ComboMemory,"combo_memory","이어지는 검무",TalentFamily.Combat,"직접 명중 뒤 대시하면 다음 콤보를 잠시 보존합니다."),
            new TalentDefinition(TalentId.StoredGuard,"stored_guard","되돌릴 탄환",TalentFamily.Combat,"막은 탄환 하나를 저장해 다음 특수행동으로 돌려보냅니다."),
            new TalentDefinition(TalentId.CrossChamber,"cross_chamber","엇갈린 탄창",TalentFamily.Combat,"근접 마무리를 맞히면 비어 있는 원거리 무기에 한 발을 채웁니다."),
            new TalentDefinition(TalentId.TurningDash,"turning_dash","꺾이는 발걸음",TalentFamily.Mobility,"대시 도중 한 번 방향을 꺾습니다. 거리와 무적 시간은 그대로입니다."),
            new TalentDefinition(TalentId.BrakeDash,"brake_dash","멈추는 발끝",TalentFamily.Mobility,"대시 키를 놓으면 즉시 멈춥니다. 남은 무적도 끝납니다."),
            new TalentDefinition(TalentId.ReturnDash,"return_dash","되짚는 발걸음",TalentFamily.Mobility,"대시 뒤 빠르게 다시 대시하면 첫 출발점으로 돌아갑니다."),
            new TalentDefinition(TalentId.SurveyNext,"survey_next","길잡이의 눈",TalentFamily.Exploration,"인접한 미발견 구역의 종류를 지도에 표시합니다."),
            new TalentDefinition(TalentId.SupplyChoice,"supply_choice","준비된 보급",TalentFamily.Exploration,"보급함의 회복을 체력 또는 마나로 선택합니다."),
            new TalentDefinition(TalentId.ReturnTrail,"return_trail","귀로의 기록",TalentFamily.Exploration,"지도에 현재 위치에서 입구까지 방문한 귀로를 표시합니다."),
            new TalentDefinition(TalentId.ChestIntent,"chest_intent","보물 감별",TalentFamily.Reward,"전투 상자를 열기 전 무기 또는 장신구 중심 보상을 고릅니다."),
            new TalentDefinition(TalentId.MerchantBarter,"merchant_barter","현물 제안",TalentFamily.Reward,"상인마다 장비 하나로 비슷한 값의 상품을 한 번 교환합니다."),
            new TalentDefinition(TalentId.ReserveOffer,"reserve_offer","미뤄 둔 선택",TalentFamily.Reward,"레벨 보상 하나를 보관하고 안전한 구역에서 선택합니다.")
        };
        public static TalentDefinition Find(string id) { foreach(var t in All)if(t.Id==id)return t;return null; }
        public static TalentDefinition Find(TalentId id) { foreach(var t in All)if(t.Type==id)return t;return null; }
    }
    public sealed class TalentLoadout
    {
        readonly HashSet<TalentId> active=new HashSet<TalentId>();
        public const int MaximumActive=3;
        public bool Has(TalentId id)=>active.Contains(id);
        public string[] Ids { get { var result=new List<string>();foreach(var t in TalentCatalog.All)if(Has(t.Type))result.Add(t.Id);return result.ToArray(); } }
        public TalentLoadout(IEnumerable<string> ids=null)
        {
            if(ids==null)return;var families=new HashSet<TalentFamily>();
            foreach(var id in ids){var t=TalentCatalog.Find(id);if(t==null||active.Count>=MaximumActive||!families.Add(t.Family))continue;active.Add(t.Type);}
        }
        public static bool CanSelect(IEnumerable<string> ids)
        { var seen=new HashSet<string>();var families=new HashSet<TalentFamily>();foreach(var id in ids){var t=TalentCatalog.Find(id);if(t==null||!seen.Add(id)||!families.Add(t.Family)||seen.Count>MaximumActive)return false;}return true; }
    }
}
