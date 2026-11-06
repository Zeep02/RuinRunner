namespace RuinRunner
{
    public enum EnemyType
    {
        Grunt,
        Shooter,
        Charger,
        Leaper,
        Boss,
        // Append only: the first five values are present in shipped room assets.
        Tidekeeper,
        SealHeart,
        Tidecaller,
        Sentinel,
        Sporeling,
        RuneTotem,
        TideCrab,
        Wisp,
        SealKnight,
        Hexer,
        // Stage 1
        BoneShield,
        VineLasher,
        RuinRat,
        MossArcher,
        PowderBeetle,
        // Stage 2
        Drowned,
        EelSpitter,
        CoralGuard,
        JellyLamp,
        TideSiren,
        // Stage 3
        InkShade,
        SealGolem,
        GildedBolter,
        ChainWarden,
        SealPriest
    }

    public static class EnemyRoles
    {
        public static bool IsBoss(EnemyType type) => type == EnemyType.Boss || type == EnemyType.Tidekeeper || type == EnemyType.SealHeart;
    }
}
