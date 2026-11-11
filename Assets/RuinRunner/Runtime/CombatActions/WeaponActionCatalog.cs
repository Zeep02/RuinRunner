namespace RuinRunner
{
    /// <summary>One source for a skill's cost and the concise player description.</summary>
    public readonly struct WeaponActionSpec
    {
        public readonly float Mana, Cooldown;
        public readonly string Description;
        public WeaponActionSpec(float mana, float cooldown, string description)
        {
            Mana = mana;
            Cooldown = cooldown;
            Description = description;
        }
    }

    public static class WeaponActionCatalog
    {
        // Mana is the only price of a skill. This only stops one click from registering twice.
        const float PressRecovery = .15f;
        public static WeaponActionSpec Get(WeaponDefinition weapon)
        {
            if (weapon == null || !SignatureWeaponCatalog.TryGet(weapon.ArtId, out var signature))
                return default;
            float mana = weapon.CatalogSpecialMana >= 0 ? weapon.CatalogSpecialMana : signature.Mana;
            return new WeaponActionSpec(mana, PressRecovery, signature.Description + "\n마나 " + mana.ToString("0.#"));
        }
    }
}
