using System;

namespace RuinRunner
{
    /// <summary>Content rolls are independent of particle, audio and camera random calls.</summary>
    public static class RunRandom
    {
        public static int NewSeed()
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var flag in new[] { "--run-seed", "--validation-seed" })
            {
                var index = Array.IndexOf(args, flag);
                if (index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var seed))
                    return seed;
            }
            return BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0) & int.MaxValue;
        }

        public static T Sample<T>(int seed, int stream, int index, Func<T> roll)
        {
            var previous = UnityEngine.Random.state;
            try
            {
                // Explicit integer arithmetic stays stable across process launches and runtimes.
                uint mixed;
                unchecked
                {
                    mixed = (uint)seed ^ ((uint)stream * 0x9e3779b9u) ^ ((uint)index * 0x85ebca6bu);
                    mixed ^= mixed >> 16;
                    mixed *= 0x7feb352du;
                    mixed ^= mixed >> 15;
                    mixed *= 0x846ca68bu;
                    mixed ^= mixed >> 16;
                }
                UnityEngine.Random.InitState(unchecked((int)mixed));
                return roll();
            }
            finally
            {
                UnityEngine.Random.state = previous;
            }
        }
    }
}
