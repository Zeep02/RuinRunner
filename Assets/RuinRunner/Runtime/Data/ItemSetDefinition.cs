using System;
using RuinRunner.Backend;

namespace RuinRunner
{
    public enum ItemSetType
    {
        None,
        BloodMoon,
        Storm,
        Guardian,
        Arcanist
    }

    public sealed class SetBonusDefinition
    {
        public ItemSetType SetType { get; }
        public int RequiredPieces { get; }
        public string Name { get; }
        public string Description { get; }
        public AccessoryTuning Tuning { get; }

        public SetBonusDefinition(ItemSetType setType, int requiredPieces, string name, string description, AccessoryTuning tuning = null)
        {
            SetType = setType;
            RequiredPieces = requiredPieces;
            Name = name;
            Tuning = tuning ?? AccessoryTuning.Legacy(ItemSetCatalog.TuningId(setType,requiredPieces));
            Description = Tuning.Format(description);
        }
    }

    public static class ItemSetCatalog
    {
        static readonly SetBonusDefinition[] legacyBonuses =
        {
            new SetBonusDefinition(ItemSetType.BloodMoon, 2, "핏빛 서약", "처치 회복 +1.5. 같은 적에 원본 3적중: 8% 출혈 3회/0.6초, 최대 12표식. 문장과 중복 적용 없음."),
            new SetBonusDefinition(ItemSetType.BloodMoon, 3, "붉은 달", "공격력 +15%. 출혈 적을 원본 공격으로 처치하면 2칸 내 2적에게 75% 출혈 전파. 발동 피해 처치는 전파하지 않음."),
            new SetBonusDefinition(ItemSetType.Storm, 2, "순풍", "속도 +0.5, 치명타 +4%. 대시 경로에 0.45초 바람: 특수 기준 22%, 최대 3적·각 1회. 쿨다운 1.1초 공유."),
            new SetBonusDefinition(ItemSetType.Storm, 3, "폭풍의 눈", "특수공격 +25%. 대시 바람이 적중하면 대시당 마나 3을 1회 회복. 회복은 소비 효과를 발동하지 않음."),
            new SetBonusDefinition(ItemSetType.Guardian, 2, "철벽", "방어 +8. 가드·패링 성공 후 피해 8 흡수 보호막, 5초 유지·쿨다운 8초 공유. 보스 공격에도 적용."),
            new SetBonusDefinition(ItemSetType.Guardian, 3, "불굴", "최대 체력 +30. 방어 성공 시 전방 1.8칸 최대 3적 반격: 특수 기준 40% + 방어력 30%. 쿨다운 3초·벽 차단·재발동 없음."),
            new SetBonusDefinition(ItemSetType.Arcanist, 2, "마력 공명", "초당 마나 +1.5. 마나를 소비한 특수 행동 3회마다 마나 1 회복. 장비 중복과 이동으로 횟수 초기화 없음."),
            new SetBonusDefinition(ItemSetType.Arcanist, 3, "대마도", "최대 마나 +30, 특수공격 +15%. 3회 소비 회복량: 마지막 비용의 30%, 최대 4. 렌즈 메아리 대상 1→3. 회복·발동 피해는 재발동하지 않음.")
        };
        public static SetBonusDefinition[] Bonuses { get; private set; }=(SetBonusDefinition[])legacyBonuses.Clone();
        public static SetBonusDefinition[] Capture() => (SetBonusDefinition[])Bonuses.Clone();
        public static string ContentId(ItemSetType type)
        {
            switch(type)
            {
                case ItemSetType.BloodMoon:return "blood_moon";
                case ItemSetType.Storm:return "storm";
                case ItemSetType.Guardian:return "guardian";
                case ItemSetType.Arcanist:return "arcanist";
                default:throw new ArgumentException("Unknown item set");
            }
        }
        public static string TuningId(ItemSetType type,int pieces)=>"set."+ContentId(type)+"."+pieces;
        internal static SetBonusDefinition[] Prepare(ContentCatalog catalog)
        {
            if(catalog.accessoryTuningVersion==0)return (SetBonusDefinition[])legacyBonuses.Clone();
            if(catalog.itemSets==null||catalog.itemSets.Length!=legacyBonuses.Length)throw new ArgumentException("Pinned item set catalog is incomplete");
            var next=new SetBonusDefinition[legacyBonuses.Length];
            for(int i=0;i<legacyBonuses.Length;i++)
            {
                var old=legacyBonuses[i]; CatalogItemSet selected=null;
                foreach(var row in catalog.itemSets)if(row.id==ContentId(old.SetType)&&row.pieces==old.RequiredPieces)
                {if(selected!=null)throw new ArgumentException("Duplicate item set tuning");selected=row;}
                if(selected==null)throw new ArgumentException("Missing item set tuning");
                var tuning=AccessoryTuning.Parse(TuningId(old.SetType,old.RequiredPieces),selected.stats,catalog.accessoryTuningVersion);
                next[i]=new SetBonusDefinition(old.SetType,old.RequiredPieces,selected.name,selected.description,tuning);
            }
            return next;
        }
        internal static void Commit(SetBonusDefinition[] definitions) { Bonuses=definitions; }

        public static string GetName(ItemSetType setType)
        {
            switch (setType)
            {
                case ItemSetType.BloodMoon: return "핏빛 달";
                case ItemSetType.Storm: return "폭풍";
                case ItemSetType.Guardian: return "수호자";
                case ItemSetType.Arcanist: return "비전술사";
                default: return "세트 없음";
            }
        }
    }
}
