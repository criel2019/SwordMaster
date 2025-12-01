

using UnityEngine;

public interface IEnemy
{
    // 상태
    bool IsAlive { get; }
    Transform Transform { get; }
    
    // 생명주기
    void Initialize(Transform player);
    void Die();
    
    // AI (각자 구현)
    void UpdateBehavior();
}