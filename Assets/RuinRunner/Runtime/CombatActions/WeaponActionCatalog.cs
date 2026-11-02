using UnityEngine;

namespace RuinRunner
{
    public enum UniqueWeaponAction { None, ShadowReturn, BloodRecall, FractureLink, VoidCharge, DragonBeacon, SunBank, StarConvergence, ReturningEdge, GravityWell, Tether, Bastion }

    /// <summary>One source for gameplay cost, recovery and the concise player description.</summary>
    public readonly struct WeaponActionSpec
    {
        public readonly UniqueWeaponAction Kind;
        public readonly float Mana, Cooldown;
        public readonly string Description;
        public WeaponActionSpec(UniqueWeaponAction kind,float mana,float cooldown,string description)
        { Kind=kind;Mana=mana;Cooldown=cooldown;Description=description; }
    }

    public static class WeaponActionCatalog
    {
        public static WeaponActionSpec Get(WeaponDefinition weapon)
        {
            var authored=GetAuthored(weapon);if(weapon==null)return authored;
            float mana=weapon.CatalogSpecialMana>=0?weapon.CatalogSpecialMana:authored.Mana;
            float cooldown=weapon.CatalogSpecialCooldown>=0?weapon.CatalogSpecialCooldown:authored.Cooldown;
            int costStart=authored.Description.LastIndexOf("마나",System.StringComparison.Ordinal);
            var description=(costStart>=0?authored.Description.Substring(0,costStart):authored.Description+"\n")+"마나 "+mana.ToString("0.#")+" · 재사용 "+cooldown.ToString("0.##")+"초";
            return new WeaponActionSpec(authored.Kind,mana,cooldown,description);
        }
        private static WeaponActionSpec GetAuthored(WeaponDefinition weapon)
        {
            if(weapon==null)return default;
            switch(weapon.Id)
            {
                case "shadow_fang":return new WeaponActionSpec(UniqueWeaponAction.ShadowReturn,16,2.5f,"그림자 돌파 후 재입력하면 출발점으로 돌아가며 벤다.\n회수 1.4초 · 마나 16 · 재사용 2.5초");
                case "blood_moon_katana":return new WeaponActionSpec(UniqueWeaponAction.BloodRecall,18,3,"발도에 맞은 적 3명까지 낙인. 재입력으로 낙인을 회수한다.\n회수 2초 · 마나 18 · 재사용 3초");
                case "blood_reaver":return new WeaponActionSpec(UniqueWeaponAction.FractureLink,22,3.5f,"내려찍은 자리에 균열 셋. 다음 기본 공격 명중으로 잇는다.\n유지 3초 · 마나 22 · 재사용 3.5초");
                case "void_revolver":return new WeaponActionSpec(UniqueWeaponAction.VoidCharge,16,2.4f,"적에게 공허 탄두를 박는다. 재입력하면 그 자리에서 폭발한다.\n회수 3초 · 마나 16 · 재사용 2.4초");
                case "dragon_cannon":return new WeaponActionSpec(UniqueWeaponAction.DragonBeacon,24,4,"조준 앞에 화약 봉화를 설치. 재입력으로 폭파하며 뒤로 물러난다.\n유지 3초 · 마나 24 · 재사용 4초");
                case "sun_oath":return new WeaponActionSpec(UniqueWeaponAction.SunBank,14,2,"받아내고 반격한다. 받아낸 힘은 다음 우클릭에 검기로 방출.\n저장 4초 · 마나 14 · 재사용 2초");
                case "starbreaker":return new WeaponActionSpec(UniqueWeaponAction.StarConvergence,22,3.5f,"별의 닻을 설치. 재입력하면 닻을 거쳐 조준 방향으로 관통한다.\n유지 3초 · 마나 22 · 재사용 3.5초");
                case "returning_edge":return new WeaponActionSpec(UniqueWeaponAction.ReturningEdge,14,2.6f,"칼날을 던져 회수한다. 돌아오는 경로를 이동으로 바꿀 수 있다.\n왕복 각각 1회 적중 · 마나 14 · 재사용 2.6초");
                case "gravity_maul":return new WeaponActionSpec(UniqueWeaponAction.GravityWell,22,4,"중력 우물을 설치. 기본 마무리를 맞히면 우물이 적을 끌어모은다.\n보스는 이동 대신 충격 · 마나 22 · 재사용 4초");
                case "tether_carbine":return new WeaponActionSpec(UniqueWeaponAction.Tether,18,3,"맞힌 적에게 작살 연결. 기본 사격 3회가 적을 끌어당긴다.\n벽에서 연결 해제 · 마나 18 · 재사용 3초");
                case "bastion_shotgun":return new WeaponActionSpec(UniqueWeaponAction.Bastion,20,4,"전방 방패가 적 탄환 3개까지 포획. 재입력하면 산탄으로 되쏜다.\n유지 3초 · 마나 20 · 재사용 4초");
            }
            switch(weapon.Family)
            {
                case WeaponFamily.Dagger:return new WeaponActionSpec(0,7,1,"짧게 파고들며 찌른다. 이동 방향을 잡고 사용.\n마나 7 · 재사용 1초");
                case WeaponFamily.Longsword:return new WeaponActionSpec(0,LongswordActionRules.ManaCost,LongswordActionRules.CooldownSeconds,"짧게 받아낸 뒤 반격. 빗나가도 전방을 벤다.\n마나 10 · 재사용 0.82초");
                case WeaponFamily.Katana:return new WeaponActionSpec(0,9,1.1f,"짧게 받아내고 발도. 성공하면 강한 교차 베기.\n마나 9 · 재사용 1.1초");
                case WeaponFamily.Mace:return new WeaponActionSpec(0,15,1.8f,"앞으로 내리쳐 적을 밀고 다음 공격 공간을 만든다.\n마나 15 · 재사용 1.8초");
                case WeaponFamily.Greatsword:return new WeaponActionSpec(0,16,1.7f,"누르면 준비, 놓으면 전진 내려찍기. 최대 충전 0.6초.\n마나 16 · 재사용 1.7초");
                case WeaponFamily.Bow:return new WeaponActionSpec(0,20,2.2f,"조준 방향으로 관통 화살을 부채꼴로 쏜다.\n마나 20 · 재사용 2.2초");
                case WeaponFamily.Crossbow:return new WeaponActionSpec(0,14,1.5f,"일렬로 선 적을 꿰뚫는 공성 볼트. 벽 앞에서 멈춘다.\n마나 14 · 재사용 1.5초");
                case WeaponFamily.Pistol when weapon.SpecialType==WeaponSpecialType.Nova:return new WeaponActionSpec(0,12,1.25f,
                    (weapon.Id=="flintlock"?"맞은 곳에서 폭발하는 탄환 한 발을 쏜다.":"위력 높은 탄환 한 발을 쏜다.")+"\n마나 12 · 재사용 1.25초");
                case WeaponFamily.Pistol:return new WeaponActionSpec(0,12,1.25f,"세 발을 짧은 간격으로 쏜다. 각 발 사이 조준 가능.\n마나 12 · 재사용 1.25초");
                case WeaponFamily.Rifle:return new WeaponActionSpec(0,16,1.6f,"한 호흡 정조준 뒤 직선 관통 사격. 이동 가능.\n마나 16 · 재사용 1.6초");
                case WeaponFamily.Shotgun:return new WeaponActionSpec(0,16,1.65f,"넓은 산탄으로 밀어내며 안전하게 뒤로 물러난다.\n마나 16 · 재사용 1.65초");
                default:return new WeaponActionSpec(0,20,2.2f,"조준 앞 3.4칸에 곡사 포탄. 착탄까지 0.38초.\n마나 20 · 재사용 2.2초");
            }
        }
    }
}
