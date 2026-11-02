using UnityEngine;

namespace RuinRunner
{
    public sealed class OneWayPlatform : MonoBehaviour
    {
        public Collider2D PlatformCollider { get; private set; }
        public void Initialize(Collider2D platformCollider) => PlatformCollider = platformCollider;
    }
}
