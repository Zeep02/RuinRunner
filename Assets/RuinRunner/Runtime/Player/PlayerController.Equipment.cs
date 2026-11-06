namespace RuinRunner
{
    /// <summary>Balance, weapons, accessories and set bonuses.</summary>
    public sealed partial class PlayerController
    {
        public void ApplyBalance(PlayerBalanceData balance)
        {
            if (Stats == null)
                return;
            Stats.ApplyBalance(balance);
            inventory?.RefreshWeaponBalance();
            if (EquippedWeapon == null)
                return;
            weaponAttackDelayMultiplier = EquippedWeapon.AttackDelayMultiplier;
        }

        public void EquipWeapon(WeaponDefinition weapon)
        {
            var swappedFrom = EquippedWeapon;
            previousWeaponFamily = EquippedWeapon == null ? WeaponFamily.Katana : EquippedWeapon.Family;
            crossAwakeReady = true;
            ClearPlantedRound();
            actionCancelSequence++;
            CancelMeleeAttack();
            EquippedWeapon = weapon;
            SignatureMeleeFx.Preload(weapon);
            weaponAttackDelayMultiplier = weapon == null ? 1f : weapon.AttackDelayMultiplier;
            comboIndex = 0;
            activeComboAnimationStep = -1;
            comboResetTimer = 0f;
            attackPoseDuration = 0f;
            attackPoseTimer = 0f;
            heavyAttackPose = false;
            CancelWeaponAction();
            SigOnEquip(weapon);
            Grow(CombatGrowthKind.WeaponSwap);
            if (swappedFrom != null && weapon != null && swappedFrom != weapon && ItemEffects != null)
                ItemEffects.OnWeaponSwapped();
        }

        private void CancelWeaponAction()
        {
            katanaAutoCutPending = false;
            katanaCounterSucceeded = false;
            specialInputBuffer = 0;
            IsGuarding = false;
            parryTimer = 0f;
            parryPresentation = false;
            ParryPresentationSucceeded = false;
            greatswordCharging = false;
            greatswordChargeTimer = 0f;
            weaponActionTimer = 0f;
            rangedPoseTimer = 0f;
        }

        private void CancelMeleeAttack()
        {
            pendingMeleeStep = null;
            pendingMeleeStroke = 0;
            activeMeleeMotion = MeleeAttackMotion.Sweep;
            attackPoseTimer = 0f;
            attackBuffer = 0f;
            chargedReleasePose = false;
            // The attack cooldown is intentionally retained when cancelling.
        }

        public void ApplyAccessory(AccessoryDefinition accessory)
        {
            if (accessory != null)
                Stats.ApplyAccessory(accessory.Type);
        }
        public void ApplySetBonus(SetBonusDefinition bonus)
        {
            if (bonus != null)
                Stats.ApplySetBonus(bonus.SetType, bonus.RequiredPieces);
        }
        public void OnEnemyDefeated(EnemyController enemy = null)
        {
            Stats.RestoreAfterKill();
            ItemEffects.OnEnemyDefeated(enemy);
            TalentOnKill(enemy);
        }
    }
}
