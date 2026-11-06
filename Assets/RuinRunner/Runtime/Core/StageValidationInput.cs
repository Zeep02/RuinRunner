using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>
    /// Command-line-only input source used by the graphical stage validation run.
    /// It deliberately feeds the same GameAction path as keyboard input; it does not
    /// alter health, damage, positions, rewards, or progression state.
    /// </summary>
    public static class StageValidationInput
    {
        public const string CommandLineArgument = "--automated-stage-validation";

        /// <summary>Command-line flags whose runs are driven by this input source instead of the keyboard.</summary>
        private static readonly string[] ScriptedArguments =
        {
            CommandLineArgument, "--sandbox-tour", "--sandbox-passives", "--sandbox-skills", "--melee-trail-tour", "--boss-move-tour",
            "--motion-validation", "--continuous-world-validation", "--map-validation", "--portal-validation", "--room-traversal-validation",
        };

        private static readonly HashSet<GameAction> Held = new HashSet<GameAction>();
        private static readonly HashSet<GameAction> Pressed = new HashSet<GameAction>();
        private static readonly HashSet<GameAction> Released = new HashSet<GameAction>();
        private static bool initialized;
        private static bool enabled;

        public static bool Enabled
        {
            get
            {
                EnsureInitialized();
                return enabled;
            }
        }

        public static Vector2 AimDirection { get; private set; } = Vector2.right;
        public static int? RequestedSeed
        {
            get
            {
                if (!Enabled)
                    return null;
                var args = Environment.GetCommandLineArgs();
                var index = Array.IndexOf(args, "--validation-seed");
                return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out int seed) ? seed : (int?)null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Held.Clear();
            Pressed.Clear();
            Released.Clear();
            AimDirection = Vector2.right;
            initialized = false;
            enabled = false;
        }

        public static void BeginFrame()
        {
            if (!Enabled)
                return;
            Pressed.Clear();
            Released.Clear();
        }

        public static void ConfigureRandomSeed()
        {
            if (!Enabled)
                return;
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "--validation-seed");
            int seed;
            if (index < 0 || index + 1 >= args.Length || !int.TryParse(args[index + 1], out seed))
                return;
            UnityEngine.Random.InitState(seed);
            Debug.Log("[Stage E2E] Deterministic initial seed " + seed);
        }

        public static bool GetKey(GameAction action) => Enabled && Held.Contains(action);
        public static bool GetKeyDown(GameAction action) => Enabled && Pressed.Contains(action);
        public static bool GetKeyUp(GameAction action) => Enabled && Released.Contains(action);

        public static void SetHeld(GameAction action, bool value)
        {
            if (!Enabled)
                return;
            var wasHeld = Held.Contains(action);
            if (value == wasHeld)
                return;

            if (value)
            {
                Held.Add(action);
                Pressed.Add(action);
            }
            else
            {
                Held.Remove(action);
                Released.Add(action);
            }
        }

        public static void SetAim(Vector2 direction)
        {
            if (!Enabled || direction.sqrMagnitude < .0001f)
                return;
            AimDirection = direction.normalized;
        }

        public static void ReleaseAll()
        {
            if (!Enabled)
                return;
            foreach (var action in Held)
                Released.Add(action);
            Held.Clear();
        }

        private static void EnsureInitialized()
        {
            if (initialized)
                return;
            initialized = true;
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (Array.FindIndex(ScriptedArguments, flag => string.Equals(flag, argument, StringComparison.OrdinalIgnoreCase)) < 0)
                    continue;
                enabled = true;
                break;
            }
        }
    }
}
