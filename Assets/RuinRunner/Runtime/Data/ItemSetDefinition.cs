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
            new SetBonusDefinition(ItemSetType.BloodMoon, 2, "핏빛 서약", "처치 시 체력 +1.5. 같은 적을 3번 때리면 출혈: 0.6초마다 그 공격 피해의 8%를 3번 줍니다. 광전사의 문장과 겹치지 않습니다."),
            new SetBonusDefinition(ItemSetType.BloodMoon, 3, "붉은 달", "공격력 +15%. 출혈 중인 적을 공격으로 처치하면 2칸 안의 적 2마리에게 출혈이 옮겨 갑니다(피해 75%)."),
            new SetBonusDefinition(ItemSetType.Storm, 2, "순풍", "이동 속도 +0.5, 치명타 확률 +4%. 대시한 길에 바람이 0.45초 남아 적 3마리까지 무기 스킬의 22% 피해를 줍니다. 재사용 1.1초"),
            new SetBonusDefinition(ItemSetType.Storm, 3, "폭풍의 눈", "무기 스킬 피해 +25%. 대시 바람이 적을 맞히면 대시마다 한 번 마나 3을 회복합니다."),
            new SetBonusDefinition(ItemSetType.Guardian, 2, "철벽", "방어력 +8. 가드나 패링에 성공하면 5초 동안 피해를 8 막는 보호막이 생깁니다. 재사용 8초"),
            new SetBonusDefinition(ItemSetType.Guardian, 3, "불굴", "최대 체력 +30. 가드나 패링에 성공하면 앞쪽 1.8칸의 적 3마리까지 반격합니다(무기 스킬의 40% + 방어력의 30%). 재사용 3초"),
            new SetBonusDefinition(ItemSetType.Arcanist, 2, "마력 공명", "초당 마나 +1.5. 마나를 쓴 무기 스킬 3번마다 마나 1을 돌려받습니다."),
            new SetBonusDefinition(ItemSetType.Arcanist, 3, "대마도", "최대 마나 +30, 무기 스킬 피해 +15%. 마나를 쓴 무기 스킬 3번마다 마지막 비용의 30%(최대 4)를 돌려받고, 비전 렌즈의 빛이 적 3마리까지 맞힙니다.")
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
                case ItemSetType.Arcanist: return "대마법사";
                default: return "세트 없음";
            }
        }
    }
}
