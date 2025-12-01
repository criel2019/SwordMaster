using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 썬더버드 신수 - 번개를 다루는 비행형 신수
/// 패시브: 정전기장 (주기적 CC)
/// 탭: 체인 스파크 (연쇄 번개)
/// 홀드: 과부하 충전 (범위 확장)
/// 릴리즈: 썬더 폴 (광역 낙뢰)
/// </summary>
public class ThunderBird : DivineBeastBase
{
	[Header("Passive - Static Field")]
	[SerializeField] private float passiveCooldown = 3f;
	[SerializeField] private float passiveRadius = 3f;
	[SerializeField] private int passiveMaxTargets = 3;
	[SerializeField] private int passiveDamage = 10;
	[SerializeField] private float passiveStunDuration = 0.1f;

	[Header("Tap - Chain Spark")]
	[SerializeField] private float chainSearchRadius = 4f;
	[SerializeField] private int chainMaxBounces = 2;
	[SerializeField] private float chainDamageReduction = 0.1f; // 10%씩 감소
	[SerializeField] private float tapCooldown = 0.1f;

	[Header("Hold - Overload Charge")]
	[SerializeField] private float holdThreshold = 0.25f; // 0.25초 이상이면 홀드
	[SerializeField] private float chargeHeight = 5f; // 상승 높이
	[SerializeField] private float chargeMinRadius = 2f;
	[SerializeField] private float chargeMaxRadius = 8f;
	[SerializeField] private float chargeMaxTime = 3f; // 최대 충전 시간

	[Header("Release - Thunder Fall")]
	[SerializeField] private float fallSpeed = 50f;
	[SerializeField] private float knockbackForce = 2f;
	[SerializeField] private float returnDelay = 0.5f;

	[Header("Visual Effects")]
	[SerializeField] private Color lightningColor = Color.cyan;
	[SerializeField] private float lightningWidth = 0.2f;
	[SerializeField] private float lightningDuration = 0.1f;

	// 내부 상태
	private float _passiveTimer;
	private float _tapCooldownTimer;
	private bool _isHolding;
	private float _holdTime;
	private float _currentChargeRadius;
	private Vector3 _originalPosition; // 플레이어 주변 기본 위치
	private Vector3 _chargePosition; // 충전 중 위치
	private Vector3 _targetGroundPosition; // 낙뢰 목표 위치

	// 상태
	private enum State { Idle, Charging, Falling, Returning }
	private State _currentState = State.Idle;

	// 시각 효과
	private List<LineRenderer> _activeLightnings = new List<LineRenderer>();
	private GameObject _rangeIndicator;

	private void Start()
	{
		// Owner 자동 찾기
		if (owner == null)
		{
			var player = GameObject.FindGameObjectWithTag("Player");
			if (player) Initialize(player.transform);
		}

		_originalPosition = transform.localPosition;
		_passiveTimer = passiveCooldown; // 시작 시 바로 발동하지 않도록

		CreateRangeIndicator();
		StartCoroutine(Routine_PassiveStaticField());
	}

	protected override void PassiveUpdate()
	{
		// 타이머 감소
		if (_tapCooldownTimer > 0f)
			_tapCooldownTimer -= Time.deltaTime;

		// 상태별 업데이트
		switch (_currentState)
		{
			case State.Idle:
				UpdateIdleState();
				break;
			case State.Charging:
				UpdateChargingState();
				break;
			case State.Falling:
				UpdateFallingState();
				break;
			case State.Returning:
				UpdateReturningState();
				break;
		}
	}

	#region State Updates

	private void UpdateIdleState()
	{
		// 플레이어 주변 따라다니기 (우측 어깨)
		Vector3 targetPos = owner.position + owner.right * 1.5f + Vector3.up * 1.5f;
		transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 5f);
	}

	private void UpdateChargingState()
	{
		// 충전 중에는 플레이어 머리 위에 고정
		_chargePosition = owner.position + Vector3.up * chargeHeight;
		transform.position = Vector3.Lerp(transform.position, _chargePosition, Time.deltaTime * 8f);

		// 범위 표시 업데이트
		float chargeProgress = Mathf.Clamp01(_holdTime / chargeMaxTime);
		_currentChargeRadius = Mathf.Lerp(chargeMinRadius, chargeMaxRadius, chargeProgress);

		UpdateRangeIndicator(_targetGroundPosition, _currentChargeRadius);
	}

	private void UpdateFallingState()
	{
		// 급강하
		transform.position = Vector3.MoveTowards(transform.position, _targetGroundPosition, fallSpeed * Time.deltaTime);

		// 지면 도착 체크
		if (Vector3.Distance(transform.position, _targetGroundPosition) < 0.5f)
		{
			ExecuteThunderFall();
			_currentState = State.Returning;
			StartCoroutine(ReturnDelayRoutine());
		}
	}

	private void UpdateReturningState()
	{
		// 복귀
		Vector3 targetPos = owner.position + owner.right * 1.5f + Vector3.up * 1.5f;
		transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 10f);
	}

	#endregion

	#region Input Handling

	public override void OnInputDown()
	{
		if (_currentState != State.Idle) return;

		_isHolding = true;
		_holdTime = 0f;

		// 마우스 위치를 지면에 투영
		_targetGroundPosition = GetGroundPosition();
	}

	public override void OnInputHold(Vector3 target)
	{
		if (!_isHolding) return;

		_holdTime += Time.deltaTime;

		// 홀드 임계값 초과 시 충전 상태로 전환
		if (_holdTime >= holdThreshold && _currentState == State.Idle)
		{
			_currentState = State.Charging;
			ShowRangeIndicator();
			Debug.Log("[ThunderBird] 충전 시작!");
		}

		// 충전 중이면 타겟 위치 업데이트
		if (_currentState == State.Charging)
		{
			_targetGroundPosition = GetGroundPosition();
		}
	}

	public override void OnInputUp(Vector3 target)
	{
		if (!_isHolding) return;

		_isHolding = false;

		// 탭인지 릴리즈인지 판단
		if (_holdTime < holdThreshold)
		{
			// 탭: 체인 스파크
			ExecuteChainSpark();
		}
		else
		{
			// 릴리즈: 썬더 폴
			if (_currentState == State.Charging)
			{
				_currentState = State.Falling;
				HideRangeIndicator();
				Debug.Log("[ThunderBird] 썬더 폴 발동!");
			}
		}

		_holdTime = 0f;
	}

	#endregion

	#region Passive - Static Field

	private IEnumerator Routine_PassiveStaticField()
	{
		while (true)
		{
			yield return new WaitForSeconds(passiveCooldown);

			if (_currentState == State.Idle || _currentState == State.Charging)
			{
				ExecuteStaticField();
			}
		}
	}

	private void ExecuteStaticField()
	{
		// 반경 내 적 탐색
		Collider[] hits = Physics.OverlapSphere(transform.position, passiveRadius);
		List<Transform> enemies = new List<Transform>();

		foreach (var hit in hits)
		{
			if (hit.CompareTag("Enemy"))
			{
				enemies.Add(hit.transform);
			}
		}

		if (enemies.Count == 0) return;

		// 거리순 정렬
		enemies.Sort((a, b) =>
		{
			float distA = Vector3.Distance(transform.position, a.position);
			float distB = Vector3.Distance(transform.position, b.position);
			return distA.CompareTo(distB);
		});

		// 최대 3명까지 타겟팅
		int targetCount = Mathf.Min(enemies.Count, passiveMaxTargets);

		for (int i = 0; i < targetCount; i++)
		{
			Transform enemy = enemies[i];
			if (enemy == null) continue;

			// 번개 이펙트
			CreateLightningEffect(transform.position, enemy.position);

			// 데미지 적용
			ApplyDamageToEnemy(enemy, passiveDamage);

			// 스턴 적용
			ApplyStunToEnemy(enemy, passiveStunDuration);
		}

		Debug.Log($"<color=yellow>[ThunderBird] 정전기장 발동! {targetCount}명 타격</color>");
	}

	#endregion

	#region Tap - Chain Spark

	private void ExecuteChainSpark()
	{
		if (_tapCooldownTimer > 0f) return;

		_tapCooldownTimer = tapCooldown;

		// 가장 가까운 적 찾기
		Transform firstTarget = FindClosestEnemy(transform.position, 30f); // 넉넉한 범위
		if (firstTarget == null)
		{
			Debug.Log("[ThunderBird] 체인 스파크: 타겟 없음");
			return;
		}

		// 체인 실행
		StartCoroutine(ChainSparkRoutine(firstTarget));
	}

	private IEnumerator ChainSparkRoutine(Transform firstTarget)
	{
		List<Transform> hitTargets = new List<Transform>();
		Transform currentTarget = firstTarget;
		Vector3 currentPos = transform.position;
		float currentDamage = passiveDamage; // 기본 데미지 사용

		for (int bounce = 0; bounce <= chainMaxBounces; bounce++)
		{
			if (currentTarget == null || hitTargets.Contains(currentTarget))
				break;

			// 번개 이펙트
			CreateLightningEffect(currentPos, currentTarget.position);

			// 데미지 적용
			ApplyDamageToEnemy(currentTarget, Mathf.RoundToInt(currentDamage));

			hitTargets.Add(currentTarget);

			// 다음 타겟 찾기
			if (bounce < chainMaxBounces)
			{
				Transform nextTarget = FindClosestEnemy(currentTarget.position, chainSearchRadius, hitTargets);

				if (nextTarget != null)
				{
					currentPos = currentTarget.position;
					currentTarget = nextTarget;
					currentDamage *= (1f - chainDamageReduction);
					yield return new WaitForSeconds(0.05f); // 체인 간격
				}
				else
				{
					break;
				}
			}
		}

		Debug.Log($"<color=cyan>[ThunderBird] 체인 스파크! {hitTargets.Count}회 튕김</color>");
	}

	#endregion

	#region Release - Thunder Fall

	private void ExecuteThunderFall()
	{
		// 범위 내 모든 적 찾기
		Collider[] hits = Physics.OverlapSphere(_targetGroundPosition, _currentChargeRadius);
		int hitCount = 0;

		foreach (var hit in hits)
		{
			if (hit.CompareTag("Enemy"))
			{
				// 데미지 적용
				ApplyDamageToEnemy(hit.transform, passiveDamage * 5); // 강력한 데미지

				// 넉백 적용
				ApplyKnockback(hit.transform, _targetGroundPosition);

				hitCount++;
			}
		}

		// 임팩트 이펙트 (간단한 디버그 표시)
		Debug.DrawRay(_targetGroundPosition, Vector3.up * 10f, Color.yellow, 1f);

		Debug.Log($"<color=red>[ThunderBird] 썬더 폴! {hitCount}명 타격, 범위: {_currentChargeRadius:F1}m</color>");

		// TODO: 임팩트 프레임, 카메라 쉐이크 등 연출
		PlayImpactEffect();
	}

	private IEnumerator ReturnDelayRoutine()
	{
		yield return new WaitForSeconds(returnDelay);
		_currentState = State.Idle;
	}

	#endregion

	#region Utility Methods

	private Transform FindClosestEnemy(Vector3 position, float radius, List<Transform> exclude = null)
	{
		Collider[] hits = Physics.OverlapSphere(position, radius);
		Transform closest = null;
		float closestDist = float.MaxValue;

		foreach (var hit in hits)
		{
			if (!hit.CompareTag("Enemy")) continue;
			if (exclude != null && exclude.Contains(hit.transform)) continue;

			float dist = Vector3.Distance(position, hit.transform.position);
			if (dist < closestDist)
			{
				closestDist = dist;
				closest = hit.transform;
			}
		}

		return closest;
	}

	private Vector3 GetGroundPosition()
	{
		// 마우스 위치를 월드로 변환 (간단하게 플레이어 위치 사용)
		// TODO: 마우스 레이캐스트 구현
		return owner.position;
	}

	private void ApplyDamageToEnemy(Transform enemy, int damage)
	{
		// 보스 체크
		IBossEnemy boss = enemy.GetComponent<IBossEnemy>();
		if (boss != null)
		{
			boss.TakeDamage(damage);
			return;
		}

		// 일반 적
		IEnemy normalEnemy = enemy.GetComponent<IEnemy>();
		if (normalEnemy != null)
		{
			// Die() 호출 -> 스포너에 통보되고 처치 카운트 증가
			normalEnemy.Die();
		}
	}

	private void ApplyStunToEnemy(Transform enemy, float duration)
	{
		// 보스는 스턴 면역
		IBossEnemy boss = enemy.GetComponent<IBossEnemy>();
		if (boss != null)
		{
			Debug.Log($"[ThunderBird] 보스는 스턴 면역!");
			return;
		}

		// TODO: 실제 스턴 시스템 구현
		// 현재는 로그만
		Debug.Log($"[ThunderBird] {enemy.name}에게 {duration}초 스턴 적용");

		// 임시: Rigidbody 정지
		Rigidbody rb = enemy.GetComponent<Rigidbody>();
		if (rb != null)
		{
			StartCoroutine(StunRoutine(rb, duration));
		}
	}

	private IEnumerator StunRoutine(Rigidbody rb, float duration)
	{
		Vector3 originalVelocity = rb.linearVelocity;
		rb.linearVelocity = Vector3.zero;

		yield return new WaitForSeconds(duration);

		// TODO: 적 AI가 다시 움직이도록 처리
	}

	private void ApplyKnockback(Transform enemy, Vector3 center)
	{
		Rigidbody rb = enemy.GetComponent<Rigidbody>();
		if (rb == null) return;

		Vector3 direction = (enemy.position - center).normalized;
		direction.y = 0.3f; // 약간 위로 튕김

		rb.AddForce(direction * knockbackForce, ForceMode.Impulse);

		Debug.Log($"[ThunderBird] {enemy.name} 넉백!");
	}

	#endregion

	#region Visual Effects

	private void CreateLightningEffect(Vector3 start, Vector3 end)
	{
		GameObject lightningObj = new GameObject("Lightning");
		LineRenderer line = lightningObj.AddComponent<LineRenderer>();

		line.positionCount = 2;
		line.SetPosition(0, start);
		line.SetPosition(1, end);

		line.startWidth = lightningWidth;
		line.endWidth = lightningWidth;
		line.startColor = lightningColor;
		line.endColor = lightningColor;

		line.material = new Material(Shader.Find("Sprites/Default"));
		line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

		// 지속 시간 후 삭제
		Destroy(lightningObj, lightningDuration);
	}

	private void CreateRangeIndicator()
	{
		_rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		_rangeIndicator.name = "ThunderFallIndicator";
		_rangeIndicator.transform.localScale = new Vector3(1f, 0.01f, 1f);

		Renderer renderer = _rangeIndicator.GetComponent<Renderer>();
		renderer.material = new Material(Shader.Find("Sprites/Default"));
		renderer.material.color = new Color(1f, 1f, 0f, 0.3f); // 노란색 반투명

		// 콜라이더 제거
		Destroy(_rangeIndicator.GetComponent<Collider>());

		_rangeIndicator.SetActive(false);
	}

	private void UpdateRangeIndicator(Vector3 position, float radius)
	{
		if (_rangeIndicator == null) return;

		_rangeIndicator.transform.position = position + Vector3.up * 0.1f; // 약간 띄우기
		_rangeIndicator.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
	}

	private void ShowRangeIndicator()
	{
		if (_rangeIndicator != null)
			_rangeIndicator.SetActive(true);
	}

	private void HideRangeIndicator()
	{
		if (_rangeIndicator != null)
			_rangeIndicator.SetActive(false);
	}

	#endregion

	#region TODO: Not Implemented Features

	/// <summary>
	/// TODO: 임팩트 프레임 연출
	/// - 화면 색상 반전 (1프레임)
	/// - 파티클 이펙트
	/// - 빛 번쩍임
	/// </summary>
	private void PlayImpactEffect()
	{
		// 현재: 미구현
		Debug.Log("[ThunderBird] TODO: 임팩트 이펙트");
	}

	/// <summary>
	/// TODO: 카메라 쉐이크
	/// - 썬더 폴 착지 시 강렬한 쉐이크
	/// - 충전 완료 시 약한 쉐이크
	/// </summary>
	private void PlayCameraShake(float intensity)
	{
		// 현재: 미구현
		Debug.Log($"[ThunderBird] TODO: 카메라 쉐이크 (강도: {intensity})");
	}

	/// <summary>
	/// TODO: 왜곡 쉐이더
	/// - 충전 중 주변 공기가 왜곡되는 효과
	/// - PostProcessing Volume 사용
	/// </summary>
	private void PlayDistortionEffect(bool enable)
	{
		// 현재: 미구현
		Debug.Log($"[ThunderBird] TODO: 왜곡 쉐이더 ({enable})");
	}

	/// <summary>
	/// TODO: 복잡한 마법진 패턴
	/// - 현재는 단순 원형 표시
	/// - 회전하는 기하학 패턴 필요
	/// - Projector 또는 Custom Shader 사용
	/// </summary>
	private void CreateMagicCircleIndicator()
	{
		// 현재: 미구현 (단순 원형만 표시 중)
		Debug.Log("[ThunderBird] TODO: 마법진 패턴");
	}

	/// <summary>
	/// TODO: 충전 완료 피드백
	/// - 최대 충전 시 시각/청각 피드백
	/// - 파티클, 사운드, 진동 등
	/// </summary>
	private void PlayChargeCompleteEffect()
	{
		// 현재: 미구현
		Debug.Log("[ThunderBird] TODO: 충전 완료 피드백");
	}

	/// <summary>
	/// TODO: 체인 스파크 잔상 효과
	/// - Trail Renderer로 청록->보라 그라데이션
	/// - Bloom 효과
	/// </summary>
	private void CreateChainSparkTrail()
	{
		// 현재: 미구현
		Debug.Log("[ThunderBird] TODO: 체인 스파크 잔상");
	}

	#endregion

	private void OnDrawGizmos()
	{
		if (!Application.isPlaying) return;

		// 패시브 범위
		Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
		Gizmos.DrawWireSphere(transform.position, passiveRadius);

		// 충전 범위
		if (_currentState == State.Charging)
		{
			Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
			Gizmos.DrawWireSphere(_targetGroundPosition, _currentChargeRadius);
		}
	}

	private void OnDestroy()
	{
		if (_rangeIndicator != null)
			Destroy(_rangeIndicator);
	}
}
