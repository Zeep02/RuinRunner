using System.Runtime.CompilerServices;
using System.Threading;
namespace RuinRunner
{
    /// <summary>Process-unique target IDs. Weak keys do not retain dead scene targets.</summary>
    public static class CombatTargetIdentity
    {
        sealed class Identity { public long Value; public Identity(){Value=Interlocked.Increment(ref next);} }
        static long next;
        static readonly ConditionalWeakTable<EnemyController,Identity> identities=new ConditionalWeakTable<EnemyController,Identity>();
        public static long For(EnemyController enemy)=>enemy==null?0:identities.GetOrCreateValue(enemy).Value;
    }
}
