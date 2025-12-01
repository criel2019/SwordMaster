using UnityEngine;

public abstract class EnemyBase : MonoBehaviour, IEnemy
{
	[Header("Base Stats")]
	[SerializeField] protected float moveSpeed = 5f;
	[SerializeField] protected float stopDistance = 2f;

	protected Transform _player;
	protected Rigidbody _rb;
	protected bool _isAlive = true;
	protected Transform _cachedTransform;

	// 공통 기능
	public bool IsAlive => _isAlive;
	public Transform Transform => _cachedTransform;

	protected virtual void Awake()
	{
		_cachedTransform = transform;
	}

	public virtual void Initialize(Transform player)
	{
		_player = player;
		_rb = GetComponent<Rigidbody>();

		if (_cachedTransform == null)
		{
			_cachedTransform = transform;
		}

		SetupRigidbody();
	}

	public virtual void Die()
	{
		_isAlive = false;

		// 스포너에 즉시 통보 (다음 프레임에 새 적 스폰)
		if (EnemyRuntimeSpawner.Instance != null)
		{
			EnemyRuntimeSpawner.Instance.NotifyEnemyDied(this);
		}

		// 기본: EnemyDestruction 있으면 실행
		var destruction = GetComponent<EnemyDestruction>();
		if (destruction) destruction.ShatterAndDie();
		else Destroy(gameObject);
	}

	// 자식이 구현
	public abstract void UpdateBehavior();

	protected virtual void SetupRigidbody()
	{
		if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
		_rb.useGravity = false;
		_rb.constraints = RigidbodyConstraints.FreezeRotationX |
						 RigidbodyConstraints.FreezeRotationZ |
						 RigidbodyConstraints.FreezePositionY;
	}
}