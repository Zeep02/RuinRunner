using System.Collections.Generic;

namespace RuinRunner
{
    public enum EquipmentMutation { None = 0, LongFinisher = 1, WideFinisher = 2, Ricochet = 3, AnchorRound = 4 }
    public enum EquipmentOath { None = 0, CounterDiscipline = 1, CrossWeaponChain = 2, FinisherHunt = 3, SteadyVolley = 4 }
    public enum CombatGrowthKind { DirectHit = 1, FinisherHit = 2, Guard = 3, Defense = 3, ReloadCompleted = 4, DamageTaken = 5, WeaponSwap = 6 }
    public struct CombatGrowthEvent
    {
        public CombatGrowthKind Kind;
        public long WeaponIdentity, TargetIdentity, ActionIdentity;
        public bool IsTraining, IsRecursive, IsBoss, IsCounter, ReloadStartedEmpty, IsMagazineHit;
        public float Time;
    }
    public static class EquipmentMutationRules
    {
        public static string Name(EquipmentMutation mutation)
        {
            switch (mutation)
            {
                case EquipmentMutation.LongFinisher:
                    return "뻗는 마무리";
                case EquipmentMutation.WideFinisher:
                    return "휘도는 마무리";
                case EquipmentMutation.Ricochet:
                    return "도약탄";
                case EquipmentMutation.AnchorRound:
                    return "머무는 탄두";
                default:
                    return "원형";
            }
        }
        public static string Summary(EquipmentMutation mutation)
        {
            switch (mutation)
            {
                case EquipmentMutation.LongFinisher:
                    return "콤보 마지막 베기가 좁고 길어집니다.";
                case EquipmentMutation.WideFinisher:
                    return "콤보 마지막 베기가 짧고 넓어집니다.";
                case EquipmentMutation.Ricochet:
                    return "기본 탄환이 벽에서 한 번 반사됩니다.";
                case EquipmentMutation.AnchorRound:
                    return "기본 탄환이 맞은 자리에 탄두가 남고, 다음 공격이 탄두를 터뜨립니다.";
                default:
                    return "변형 없음";
            }
        }
        public static bool Compatible(OwnedWeapon item, EquipmentMutation mutation)
        {
            if (item == null || item.Mutation != EquipmentMutation.None || mutation == EquipmentMutation.None)
                return false;
            // Orb strikes and cannon lobs fire no straight round for these to change.
            if (item.Definition.Family == WeaponFamily.Orb || item.Definition.Family == WeaponFamily.Cannon)
                return false;
            bool melee = item.Definition.AttackType == WeaponAttackType.Melee;
            return melee ? (mutation == EquipmentMutation.LongFinisher || mutation == EquipmentMutation.WideFinisher) : (mutation == EquipmentMutation.Ricochet || mutation == EquipmentMutation.AnchorRound);
        }
        public static string OathName(EquipmentOath oath)
        {
            switch (oath)
            {
                case EquipmentOath.CounterDiscipline:
                    return "받아내는 약속";
                case EquipmentOath.CrossWeaponChain:
                    return "이어지는 무장";
                case EquipmentOath.FinisherHunt:
                    return "마무리의 자취";
                case EquipmentOath.SteadyVolley:
                    return "한 탄창의 약속";
                default:
                    return "서약 없음";
            }
        }
        public static string OathCondition(EquipmentOath oath)
        {
            switch (oath)
            {
                case EquipmentOath.CounterDiscipline:
                    return "막은 뒤 2초 안에 서로 다른 적 3마리 맞히기";
                case EquipmentOath.CrossWeaponChain:
                    return "무기를 바꿔 3초 안에 서로 다른 적 3마리 맞히기";
                case EquipmentOath.FinisherHunt:
                    return "콤보 마지막 공격으로 서로 다른 적 5마리 맞히기";
                case EquipmentOath.SteadyVolley:
                    return "맞지 않고 한 탄창으로 3번 맞힌 뒤 빈 탄창 장전하기, 3번";
                default:
                    return "";
            }
        }
        public static string OathResult(EquipmentOath oath)
        {
            switch (oath)
            {
                case EquipmentOath.CounterDiscipline:
                    return "각성 · 반격에 가느다란 관통 검격이 이어집니다.";
                case EquipmentOath.CrossWeaponChain:
                    return "각성 · 무기를 바꾸고 처음 쓰는 무기 스킬이 이전 무기의 흔적을 잇습니다.";
                case EquipmentOath.FinisherHunt:
                    return "각성 · 콤보 마지막 공격의 궤적이 되돌아옵니다.";
                case EquipmentOath.SteadyVolley:
                    return "각성 · 빈 탄창을 장전한 뒤 첫 탄이 표식을 남기고, 다음 공격이 표식을 터뜨립니다.";
                default:
                    return "";
            }
        }
        public static int Required(EquipmentOath oath) => oath == EquipmentOath.FinisherHunt ? 5 : 3;
        public static bool Compatible(OwnedWeapon item, EquipmentOath oath, InventorySystem inventory)
        {
            if (item == null || item.Oath != EquipmentOath.None || oath == EquipmentOath.None)
                return false;
            if (oath == EquipmentOath.CounterDiscipline)
                return item.Definition.Family == WeaponFamily.Longsword || item.Definition.Family == WeaponFamily.Katana;
            if (oath == EquipmentOath.FinisherHunt)
                return item.Definition.AttackType == WeaponAttackType.Melee;
            if (oath == EquipmentOath.SteadyVolley)
                return item.Definition.AttackType == WeaponAttackType.Ranged && item.Definition.MagazineSize >= 3;
            foreach (var other in inventory.AllWeapons())
                if (other.Identity != item.Identity)
                    return true;
            return false;
        }
    }
    // The active run owns progression. Training targets and proc damage never contribute.
    public sealed class EquipmentOathTracker
    {
        readonly HashSet<string> counted = new HashSet<string>();
        readonly Dictionary<long, float> guards = new Dictionary<long, float>();
        readonly Dictionary<long, CombatGrowthEvent> lastHits = new Dictionary<long, CombatGrowthEvent>();
        readonly Dictionary<long, HashSet<long>> magazineHits = new Dictionary<long, HashSet<long>>();
        public bool Apply(InventorySystem inventory, CombatGrowthEvent e)
        {
            if (e.IsTraining || e.IsRecursive)
                return false;
            var weapon = inventory.FindOwned(e.WeaponIdentity) as OwnedWeapon;
            if (e.Kind == CombatGrowthKind.Guard)
            {
                guards[e.WeaponIdentity] = e.Time;
                return false;
            }
            if (e.Kind == CombatGrowthKind.DamageTaken)
            {
                magazineHits.Clear();
                return false;
            }
            var key = e.WeaponIdentity + ":" + e.Kind + ":" + e.ActionIdentity + ":" + e.TargetIdentity;
            if (!counted.Add("event:" + key))
                return false;
            bool direct = e.Kind == CombatGrowthKind.DirectHit || e.Kind == CombatGrowthKind.FinisherHit;
            if (direct && e.IsMagazineHit)
            {
                if (!magazineHits.TryGetValue(e.WeaponIdentity, out var shots))
                {
                    shots = new HashSet<long>();
                    magazineHits[e.WeaponIdentity] = shots;
                }
                shots.Add(e.ActionIdentity);
            }
            bool complete = false;
            if (weapon != null && !weapon.Awakened && weapon.Oath != EquipmentOath.None)
            {
                bool qualifies = false;
                switch (weapon.Oath)
                {
                    case EquipmentOath.CounterDiscipline:
                        qualifies = direct && e.IsCounter && guards.TryGetValue(e.WeaponIdentity, out var at) && e.Time - at <= 2f;
                        break;
                    case EquipmentOath.CrossWeaponChain:
                        qualifies = direct && lastHits.TryGetValue(e.TargetIdentity, out var previous) && previous.WeaponIdentity != e.WeaponIdentity && e.Time - previous.Time <= 3f;
                        break;
                    case EquipmentOath.FinisherHunt:
                        qualifies = e.Kind == CombatGrowthKind.FinisherHit;
                        break;
                    case EquipmentOath.SteadyVolley:
                        qualifies = e.Kind == CombatGrowthKind.ReloadCompleted && e.ReloadStartedEmpty && magazineHits.TryGetValue(e.WeaponIdentity, out var hits) && hits.Count >= 3;
                        break;
                }
                string progressKey = "progress:" + weapon.Identity + ":" + (weapon.Oath == EquipmentOath.SteadyVolley ? e.ActionIdentity : e.TargetIdentity);
                if (qualifies && counted.Add(progressKey))
                {
                    weapon.OathProgress++;
                    weapon.Awakened = weapon.OathProgress >= EquipmentMutationRules.Required(weapon.Oath);
                    complete = weapon.Awakened;
                }
            }
            if (direct)
                lastHits[e.TargetIdentity] = e;
            if (e.Kind == CombatGrowthKind.ReloadCompleted)
                magazineHits.Remove(e.WeaponIdentity);
            return complete;
        }
    }
}
