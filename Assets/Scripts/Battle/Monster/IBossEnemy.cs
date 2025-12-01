using UnityEngine;

/// <summary>
/// 보스 몬스터 인터페이스
/// 일반 적과 달리 특수 공격 패턴을 가질 수 있음
/// </summary>
public interface IBossEnemy : IEnemy
{
    // 보스 전용 기능
    int CurrentHealth { get; }
    int MaxHealth { get; }
    bool IsInAttackPattern { get; }

    /// <summary>
    /// 피격 처리 (히트 카운트)
    /// </summary>
    void TakeDamage(int damage = 1);

    /// <summary>
    /// 특수 공격 패턴 실행 (구현은 선택적)
    /// 예: 범위 공격, 투사체, 소환 등
    /// </summary>
    void ExecuteSpecialAttack();

    /// <summary>
    /// 기본 공격 (돌진, 근접 등)
    /// </summary>
    void ExecuteBasicAttack();
}
