using UnityEngine;

public class RicochetBall : MonoBehaviour
{
	[Header("Impact Settings")]
	[SerializeField] private float bounceSpeed = 60f;
	[SerializeField] private float searchRadius = 40f;
	[SerializeField] private int maxBounceCount = 3;

	private Rigidbody _rb;
	private Renderer _renderer;
	private Color _originalColor;

	private int _comboCount = 0;
	private int _currentBounceCount = 0;
	private bool _isReleased = false;
	private bool _isSpecialMode = false;

	private void Awake()
	{
		_rb = GetComponent<Rigidbody>();
		_renderer = GetComponent<Renderer>();
		if (_renderer) _originalColor = _renderer.material.color;
	}

	private void OnEnable()
	{
		_comboCount = 0;
		_currentBounceCount = 0;
		_isReleased = false;
		_isSpecialMode = false;
	}

	public void Activate(bool isSpecial)
	{
		_isReleased = true;
		_isSpecialMode = isSpecial;

		if (_renderer)
		{
			_renderer.material.color = isSpecial ? Color.yellow : _originalColor;
		}
	}

	private void OnCollisionEnter(Collision collision)
	{
		// 1. 적과 충돌
		if (collision.collider.CompareTag("Enemy"))
		{
			// (A) 적 파괴 연출 (무조건 실행)
			EnemyDestruction destruction = collision.collider.GetComponent<EnemyDestruction>();
			if (destruction) destruction.ShatterAndDie();
			else Destroy(collision.collider.gameObject);

			// (B) 공의 운명 결정
			if (!_isReleased)
			{
				// [상태 0] 아직 쏘지 않고 돌고 있는 중
				// -> 공은 파괴되지 않고 계속 돔. 적만 죽임.
			}
			else
			{
				// [상태 1] 발사된 상태
				if (_isSpecialMode)
				{
					// === 특수 모드 (핑핑핑) ===
					_comboCount++;
					_currentBounceCount++;

					// 3번 다 튕겼으면 이제 삭제
					if (_currentBounceCount >= maxBounceCount)
					{
						Destroy(gameObject);
					}
					else
					{
						// 아직 횟수 남음 -> 다음 적 찾기
						float currentSpeed = bounceSpeed + (_comboCount * 5f);
						Transform nextTarget = FindNextTarget(collision.collider.transform.position);

						if (nextTarget != null)
						{
							Vector3 direction = (nextTarget.position - transform.position).normalized;
							_rb.linearVelocity = direction * currentSpeed;
						}
						else
						{
							// 주변에 적 없으면? 
							// 특수 모드라 아깝지만, 더 튕길 곳이 없으니 벽으로 튕기거나 삭제.
							// 여기선 '유도'가 핵심이니 타겟 없으면 그냥 소멸시키거나, 
							// 물리 반사로 남겨둘 수 있음. 
							// 사용자 의도상 "3번 타격"이 목표니 물리 반사로 생명 연장
							ReflectNormal(collision, currentSpeed);
						}
					}
				}
				else
				{
					// === 일반 모드 (타이밍 실패) ===
					// [요청사항 반영] 유도 없음 + 1회 타격 후 즉시 삭제
					Destroy(gameObject);
				}
			}

			// (C) 피드백 (특수 모드일 때만 번쩍임, 일반모드는 죽으니 필요없음)
			if (_isSpecialMode && _renderer)
			{
				_renderer.material.color = Color.white;
				Invoke(nameof(RestoreColor), 0.05f);
			}
		}
		else
		{
			// 벽/바닥 충돌: 발사된 상태면 속도 유지 (안 죽음)
			if (_isReleased && _rb && !_rb.isKinematic)
			{
				_rb.linearVelocity = _rb.linearVelocity * 1.0f;
			}
		}
	}

	private void ReflectNormal(Collision collision, float speed)
	{
		Vector3 randomReflect = Vector3.Reflect(_rb.linearVelocity.normalized, collision.contacts[0].normal);
		randomReflect += Random.insideUnitSphere * 0.1f;
		_rb.linearVelocity = randomReflect.normalized * speed;
	}

	private Transform FindNextTarget(Vector3 impactPos)
	{
		Collider[] hits = Physics.OverlapSphere(impactPos, searchRadius);
		Transform bestTarget = null;
		float closestDist = float.MaxValue;

		foreach (var hit in hits)
		{
			if (hit.CompareTag("Enemy"))
			{
				float d = Vector3.Distance(impactPos, hit.transform.position);
				if (d < 1.0f) continue;

				if (d < closestDist)
				{
					closestDist = d;
					bestTarget = hit.transform;
				}
			}
		}
		return bestTarget;
	}

	private void RestoreColor()
	{
		if (_renderer)
		{
			_renderer.material.color = _isSpecialMode ? Color.yellow : _originalColor;
		}
	}
}