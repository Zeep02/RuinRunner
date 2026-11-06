using System;

namespace RuinRunner
{
    /// <summary>
    /// Extension points filled in by the separate RuinRunner.Diagnostics assembly.
    /// Game code never references validation code directly; without that assembly
    /// every hook stays null and the game runs normally.
    /// </summary>
    public static class DiagnosticsHooks
    {
        public const string CombatSandboxArgument = "--combat-sandbox";

        /// <summary>Called before the game is created. Returns true when a standalone validation scene takes over.</summary>
        public static Func<bool> StartStandalone;

        /// <summary>Called at the end of <see cref="GameController"/>.Awake to start an automated or sandbox session.</summary>
        public static Action<GameController> AttachToSession;

        /// <summary>Called once the game controller exists, to attach recorders requested on the command line.</summary>
        public static Action<GameController> AttachToGame;

        /// <summary>Raised after armor for every hit on an enemy: enemy, damage, weapon hit (not an item effect), critical.</summary>
        public static Action<EnemyController, float, bool, bool> EnemyDamaged;
    }
}
