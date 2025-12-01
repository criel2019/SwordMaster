using UnityEngine;

/// <summary>
/// 보스 몬스터 베이스 클래스
/// 체력 시스템(히트 카운트), 공격 패턴 관리
/// </summary>
public abstract class BossBase : MonoBehaviour, IBossEnemy
{
    [Header("Boss Stats")]
    [SerializeField] protected int maxHealth = 50;
    [SerializeField] protected float moveSpeed = 2.5f; // BasicEnemy의 절반
    [SerializeField] protected float stopDistance = 5f; // 보스는 좀 더 먼 거리 유지

    [Header("Attack Settings")]
    [SerializeField] protected float attackCooldown = 3f;
    [SerializeField] protected float attackPreparationTime = 1.5f;

    protected Transform _player;
    protected Rigidbody _rb;
    protected bool _isAlive = true;
    protected Transform _cachedTransform;

    // 체력 시스템
    protected int _currentHealth;
    public int CurrentHealth => _currentHealth;
    public int MaxHealth => maxHealth;

    // 공격 상태
    protected bool _isInAttackPattern = false;
    public bool IsInAttackPattern => _isInAttackPattern;
    protected float _attackCooldownTimer = 0f;

    // IEnemy 구현
    public bool IsAlive => _isAlive;
    public Transform Transform => _cachedTransform;

    protected virtual void Awake()
    {
        _cachedTransform = transform;
        _currentHealth = maxHealth;
    }

    public virtual void Initialize(Transform player)
    {
        _player = player;
        _rb = GetComponent<Rigidbody>();

        if (_cachedTransform == null)
        {
            _cachedTransform = transform;
        }

        _currentHealth = maxHealth;
        SetupRigidbody();
    }

    public virtual void TakeDamage(int damage = 1)
    {
        if (!_isAlive) return;

        _currentHealth -= damage;

        Debug.Log($"[Boss] 피격! 남은 체력: {_currentHealth}/{maxHealth}");

        if (_currentHealth <= 0)
        {
            Die();
        }
        else
        {
            OnDamaged();
        }
    }

    public virtual void Die()
    {
        if (!_isAlive) return;

        _isAlive = false;

        Debug.Log($"<color=red>[Boss] {gameObject.name} 처치!</color>");

        // 스포너에 보스 사망 통보
        if (EnemyRuntimeSpawner.Instance != null)
        {
            EnemyRuntimeSpawner.Instance.NotifyBossDied(this);
        }

        // 파괴 처리
        var destruction = GetComponent<EnemyDestruction>();
        if (destruction)
            destruction.ShatterAndDie();
        else
            Destroy(gameObject, 0.5f);
    }

    public abstract void UpdateBehavior();
    public abstract void ExecuteBasicAttack();
    public abstract void ExecuteSpecialAttack();

    /// <summary>
    /// 피격 시 호출 (서브클래스에서 선택적으로 오버라이드)
    /// </summary>
    protected virtual void OnDamaged()
    {
        // 피격 이펙트, 사운드 등 추가 가능
    }

    protected virtual void SetupRigidbody()
    {
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ |
                         RigidbodyConstraints.FreezePositionY;
    }

    /// <summary>
    /// 공격 쿨다운 관리 유틸리티
    /// </summary>
    protected bool CanAttack()
    {
        if (_attackCooldownTimer > 0f)
        {
            _attackCooldownTimer -= Time.deltaTime;
            return false;
        }
        return true;
    }

    protected void StartAttackCooldown()
    {
        _attackCooldownTimer = attackCooldown;
    }
}
