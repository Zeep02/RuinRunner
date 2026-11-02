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
        Sentinel
    }

    public static class EnemyRoles
    {
        public static bool IsBoss(EnemyType type) => type == EnemyType.Boss || type == EnemyType.Tidekeeper || type == EnemyType.SealHeart;
    }
}
