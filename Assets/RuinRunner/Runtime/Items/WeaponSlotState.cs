namespace RuinRunner
{
    /// <summary>A position holding an owned weapon, not another copy of its mutable state.</summary>
    public sealed class WeaponSlotState
    {
        public OwnedWeapon Item { get; private set; }
        public WeaponDefinition Weapon => Item?.Weapon;
        public int Ammo => Item?.Ammo ?? 0;
        public float ReloadRemaining => Item?.ReloadRemaining ?? 0;
        public bool IsReloading => Item != null && Item.IsReloading;
        public float ReloadProgress => !IsReloading ? 0 : 1 - ReloadRemaining / Weapon.ReloadSeconds;
        public void Assign(WeaponDefinition weapon)
        {
            Item = weapon == null ? null : new OwnedWeapon(weapon);
        }
        public void Assign(WeaponDefinition weapon, int ammo, float reloadRemaining)
        {
            Item = weapon == null ? null : new OwnedWeapon(weapon, ammo, reloadRemaining);
        }
        internal void AssignItem(OwnedWeapon item)
        {
            Item = item;
        }
        public void SwapWith(WeaponSlotState other)
        {
            if (other == null || ReferenceEquals(this, other))
                return;
            var item = Item;
            Item = other.Item;
            other.Item = item;
        }
        public bool TryConsumeAmmo() => Item == null || Item.TryConsumeAmmo();
        public bool StartReload() => Item != null && Item.StartReload();
        public void Tick(float deltaTime)
        {
            Item?.Tick(deltaTime);
        }
        public void RefreshBalance()
        {
            Item?.RefreshBalance();
        }
    }
}
