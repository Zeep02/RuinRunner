using UnityEngine;

namespace RuinRunner
{
    [CreateAssetMenu(menuName = "Ruin Runner/Enemy Animation Library", fileName = "EnemyAnimationLibrary")]
    public sealed class EnemyAnimationLibrary : ScriptableObject
    {
        [Min(1f)] public float framesPerSecond = 12f;
        [Tooltip("보스 처치 연출 프레임입니다. 비어 있으면 코드 기반 축소·회전 연출을 사용합니다.")]
        public Sprite[] bossDeath;
    }
}
