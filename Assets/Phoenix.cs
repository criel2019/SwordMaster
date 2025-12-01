using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 주작 신수 - 불꽃을 휘감고 돌진하는 고기동형 신수
/// 패시브: 자유 비행 & 플레어 (자동 돌진 공격)
/// 탭: 호밍 스파이럴 (곡선 비행)
/// 홀드: 태양의 알 (충전 + 저스트 타이밍)
/// 릴리즈: 플레어 드라이브 (강화 돌진 + 화염 장판)
/// </summary>
public class Phoenix : DivineBeastBase
{
	[Header("Passive - Wild Flare")]
	[SerializeField] private float passiveCooldown = 4f;
	[SerializeField] private float passiveSearchRadius = 5f;
	[SerializeField] private float passiveDashSpeed = 20f;
	[SerializeField] private int passiveDamage = 10;

	[Header("Tap - Homing Spiral")]
	[SerializeField] private float tapThreshold = 0.2f;
	[SerializeField] private float spiralSpeed = 15f;
	[SerializeField] private float spiralHeight = 3f; // 곡선 높이
	[SerializeField] private float explosionRadius = 2f;
	[SerializeField] private int tapDamage = 30;

	[Header("Hold - Sun Gathering")]
	[SerializeField] private float minScale = 1f;
	[SerializeField] private float maxScale = 3f;
	[SerializeField] private float justFrameStart = 1.0f; // 저스트 시작
	[SerializeField] private float justFrameEnd = 1.2f; // 저스트 종료
	[SerializeField] private float maxChargeTime = 2f;

	[Header("Release - Flare Drive")]
	[SerializeField] private float driveSpeed = 30f;
	[SerializeField] private float driveDistance = 20f;
	[SerializeField] private float descendSpeed = 10f; // 랜딩 속도
	[SerializeField] private float attackHeight = 1f; // 공격 시 높이
	[SerializeField] private int normalDriveDamage = 50;
	[SerializeField] private int justDriveDamage = 100;

	[Header("Fire Zone (Just Frame)")]
	[SerializeField] private float fireZoneDuration = 1f;
	[SerializeField] private float fireZoneRadius = 3f;
	[SerializeField] private float fireZoneTickInterval = 0.2f;
	[SerializeField] private int fireZoneTickDamage = 10;

	[Header("Visual")]
	[SerializeField] private Color normalColor = Color.red;
	[SerializeField] private Color justFrameColor = Color.yellow;

	// 내부 상태
	private Vector3 _originalLocalPosition;
	private Vector3 _hoverOffset; // 호버링 오프셋
	private bool _isHolding;
	private float _holdTime;
	private float _currentScale;
	private bool _isInJustFrame;
	private bool _wasJustFrameOnRelease; // 릴리즈 시점의 저스트 프레임 여부 저장

	// 상태
	private enum State { Idle, PassiveDashing, TapFlying, Descending, Driving, Returning }
	private State _currentState = State.Idle;

	// 목표 위치
	private Vector3 _targetPosition;
	private Transform _currentTarget;
	private float _movementProgress;

	// 베지어 곡선용
	private Vector3 _bezierStart;
	private Vector3 _bezierEnd;
	private Vector3 _bezierControl;

	// 렌더러
	private Renderer _renderer;

	private void Start()
	{
		// Owner 자동 찾기
		if (owner == null)
		{
			var player = GameObject.FindGameObjectWithTag("Player");
			if (player) Initialize(player.transform);
		}

		_originalLocalPosition = Vector3.right * 1.5f + Vector3.up * 2f; // 우측 위
		_hoverOffset = _originalLocalPosition;
		_currentScale = minScale;
		transform.localScale = Vector3.one * _currentScale;

		_renderer = GetComponent<Renderer>();
		if (_renderer == null)
		{
			// 자동으로 렌더러 추가 (시각화를 위해)
			var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			sphere.transform.SetParent(transform);
			sphere.transform.localPosition = Vector3.zero;
			sphere.transform.localScale = Vector3.one * 0.5f;
			_renderer = sphere.GetComponent<Renderer>();
			_renderer.material.color = normalColor;
			Destroy(sphere.GetComponent<Collider>()); // 콜라이더 제거
		}

		StartCoroutine(Routine_PassiveFlare());
	}

	protected override void PassiveUpdate()
	{
		// 상태별 업데이트
		switch (_currentState)
		{
			case State.Idle:
				UpdateIdleHovering();
				break;
			case State.PassiveDashing:
				UpdatePassiveDashing();
				break;
			case State.TapFlying:
				UpdateTapFlying();
				break;
			case State.Descending:
				UpdateDescending();
				break;
			case State.Driving:
				UpdateDriving();
				break;
			case State.Returning:
				UpdateReturning();
				break;
		}
	}

	#region State Updates

	private void UpdateIdleHovering()
	{
		// 플레이어 주변 호버링
		Vector3 targetPos = owner.position + _hoverOffset;
		transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 3f);

		// 약간 위아래로 움직이는 호버링 효과
		float hoverWave = Mathf.Sin(Time.time * 2f) * 0.3f;
		transform.position += Vector3.up * hoverWave * Time.deltaTime;
	}

	private void UpdatePassiveDashing()
	{
		if (_currentTarget == null || !_currentTarget.gameObject.activeInHierarchy)
		{
			_currentState = State.Returning;
			return;
		}

		// 타겟을 향해 돌진
		Vector3 direction = (_currentTarget.position - transform.position).normalized;
		transform.position += direction * passiveDashSpeed * Time.deltaTime;

		// 타겟 도달 체크
		float distToTarget = Vector3.Distance(transform.position, _currentTarget.position);
		if (distToTarget < 1f)
		{
			// 타격
			HitEnemy(_currentTarget, passiveDamage);

			// 관통 후 복귀
			_currentState = State.Returning;
			Debug.Log("[Phoenix] 패시브 돌진 완료!");
		}
	}

	private void UpdateTapFlying()
	{
		// 베지어 곡선을 따라 이동
		_movementProgress += Time.deltaTime * spiralSpeed / Vector3.Distance(_bezierStart, _bezierEnd);

		if (_movementProgress >= 1f)
		{
			// 목표 도달 - 폭발
			ExecuteSpiralExplosion();
			_currentState = State.Returning;
		}
		else
		{
			// 베지어 곡선 계산
			Vector3 pos = CalculateBezierPoint(_movementProgress, _bezierStart, _bezierControl, _bezierEnd);
			transform.position = pos;
		}
	}

	private void UpdateDescending()
	{
		// 아래로 랜딩 비행
		Vector3 targetPos = owner.position + Vector3.forward * 2f + Vector3.up * attackHeight;
		transform.position = Vector3.MoveTowards(transform.position, targetPos, descendSpeed * Time.deltaTime);

		// 목표 높이 도달
		if (Vector3.Distance(transform.position, targetPos) < 0.5f)
		{
			_currentState = State.Driving;
			_movementProgress = 0f;
			_targetPosition = transform.position + owner.forward * driveDistance;
			Debug.Log("[Phoenix] 랜딩 완료, 드라이브 시작!");
		}
	}

	private void UpdateDriving()
	{
		// 직선 돌진
		transform.position = Vector3.MoveTowards(transform.position, _targetPosition, driveSpeed * Time.deltaTime);

		// 경로상의 적 타격
		CheckDriveHits();

		// 목표 도달
		if (Vector3.Distance(transform.position, _targetPosition) < 0.5f)
		{
			ExecuteDriveExplosion();
			_currentState = State.Returning;
		}
	}

	private void UpdateReturning()
	{
		// 플레이어 곁으로 복귀
		Vector3 targetPos = owner.position + _hoverOffset;
		transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 5f);

		// 크기 원래대로
		_currentScale = Mathf.Lerp(_currentScale, minScale, Time.deltaTime * 3f);
		transform.localScale = Vector3.one * _currentScale;

		// 도착했으면 Idle로
		if (Vector3.Distance(transform.position, targetPos) < 1f)
		{
			_currentState = State.Idle;
		}
	}

	#endregion

	#region Input Handling

	public override void OnInputDown()
	{
		if (_currentState != State.Idle) return;

		_isHolding = true;
		_holdTime = 0f;
		_isInJustFrame = false;
	}

	public override void OnInputHold(Vector3 target)
	{
		if (!_isHolding) return;

		_holdTime += Time.deltaTime;

		// 크기 증가
		float scaleProgress = Mathf.Clamp01(_holdTime / maxChargeTime);
		_currentScale = Mathf.Lerp(minScale, maxScale, scaleProgress);
		transform.localScale = Vector3.one * _currentScale;

		// 저스트 프레임 체크
		if (_holdTime >= justFrameStart && _holdTime <= justFrameEnd)
		{
			if (!_isInJustFrame)
			{
				_isInJustFrame = true;
				OnEnterJustFrame();
			}
		}
		else
		{
			if (_isInJustFrame)
			{
				_isInJustFrame = false;
				OnExitJustFrame();
			}
		}
	}

	public override void OnInputUp(Vector3 target)
	{
		if (!_isHolding) return;

		_isHolding = false;

		// 탭인지 릴리즈인지 판단
		if (_holdTime < tapThreshold)
		{
			// 탭: 호밍 스파이럴
			ExecuteHomingSpiral(target);
			_wasJustFrameOnRelease = false;
		}
		else
		{
			// 릴리즈: 플레어 드라이브 (저스트 프레임 여부 저장)
			_wasJustFrameOnRelease = _isInJustFrame;
			ExecuteFlareDrive();
		}

		_holdTime = 0f;
		_isInJustFrame = false;
		if (_renderer) _renderer.material.color = normalColor;
	}

	#endregion

	#region Passive - Wild Flare

	private IEnumerator Routine_PassiveFlare()
	{
		while (true)
		{
			yield return new WaitForSeconds(passiveCooldown);

			if (_currentState == State.Idle)
			{
				ExecutePassiveFlare();
			}
		}
	}

	private void ExecutePassiveFlare()
	{
		// 반경 내 랜덤 적 찾기
		Collider[] hits = Physics.OverlapSphere(transform.position, passiveSearchRadius);
		List<Transform> enemies = new List<Transform>();

		foreach (var hit in hits)
		{
			if (hit.CompareTag("Enemy"))
			{
				enemies.Add(hit.transform);
			}
		}

		if (enemies.Count == 0) return;

		// 랜덤 선택
		_currentTarget = enemies[Random.Range(0, enemies.Count)];
		_currentState = State.PassiveDashing;

		Debug.Log($"[Phoenix] 패시브 돌진 시작! 타겟: {_currentTarget.name}");
	}

	#endregion

	#region Tap - Homing Spiral

	private void ExecuteHomingSpiral(Vector3 targetWorldPos)
	{
		_bezierStart = transform.position;
		_bezierEnd = targetWorldPos;

		// 중간 제어점 (위로 솟은 지점)
		Vector3 midPoint = (_bezierStart + _bezierEnd) / 2f;
		_bezierControl = midPoint + Vector3.up * spiralHeight;

		_movementProgress = 0f;
		_currentState = State.TapFlying;

		Debug.Log($"[Phoenix] 호밍 스파이럴 발동! 목표: {targetWorldPos}");
	}

	private Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
	{
		// 2차 베지어 곡선
		float u = 1 - t;
		float tt = t * t;
		float uu = u * u;

		Vector3 point = uu * p0; // (1-t)^2 * P0
		point += 2 * u * t * p1; // 2(1-t)t * P1
		point += tt * p2; // t^2 * P2

		return point;
	}

	private void ExecuteSpiralExplosion()
	{
		// 범위 내 적 타격
		Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
		int hitCount = 0;

		foreach (var hit in hits)
		{
			if (hit.CompareTag("Enemy"))
			{
				HitEnemy(hit.transform, tapDamage);
				hitCount++;
			}
		}

		Debug.Log($"<color=orange>[Phoenix] 스파이럴 폭발! {hitCount}명 타격</color>");

		// TODO: 폭발 이펙트
	}

	#endregion

	#region Release - Flare Drive

	private void ExecuteFlareDrive()
	{
		// 저장된 저스트 프레임 여부 사용
		if (_wasJustFrameOnRelease)
		{
			Debug.Log("<color=yellow>[Phoenix] ★ 저스트 프레임 성공! ★</color>");
		}
		else
		{
			Debug.Log("[Phoenix] 일반 플레어 드라이브");
		}

		// 먼저 랜딩 비행 시작
		_currentState = State.Descending;
	}

	private void CheckDriveHits()
	{
		// 현재 위치에서 적 타격
		Collider[] hits = Physics.OverlapSphere(transform.position, 1.5f);

		foreach (var hit in hits)
		{
			if (hit.CompareTag("Enemy"))
			{
				// 저장된 저스트 프레임 여부 사용
				int damage = _wasJustFrameOnRelease ? justDriveDamage : normalDriveDamage;

				HitEnemy(hit.transform, damage);
			}
		}
	}

	private void ExecuteDriveExplosion()
	{
		// 저장된 저스트 프레임 여부 사용
		bool isJustFrame = _wasJustFrameOnRelease;

		// 폭발 범위 타격
		Collider[] hits = Physics.OverlapSphere(transform.position, isJustFrame ? 4f : 2f);
		int hitCount = 0;

		foreach (var hit in hits)
		{
			if (hit.CompareTag("Enemy"))
			{
				int damage = isJustFrame ? justDriveDamage : normalDriveDamage;
				HitEnemy(hit.transform, damage);
				hitCount++;
			}
		}

		Debug.Log($"<color=red>[Phoenix] 플레어 드라이브 폭발! {hitCount}명 타격</color>");

		// 저스트 프레임이면 화염 장판 생성
		if (isJustFrame)
		{
			CreateFireZone(transform.position);
		}
	}

	#endregion

	#region Fire Zone

	private void CreateFireZone(Vector3 position)
	{
		GameObject fireZone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		fireZone.name = "FireZone";
		fireZone.transform.position = position + Vector3.up * 0.1f;
		fireZone.transform.localScale = new Vector3(fireZoneRadius * 2f, 0.05f, fireZoneRadius * 2f);

		// 시각화
		Renderer renderer = fireZone.GetComponent<Renderer>();
		renderer.material = new Material(Shader.Find("Sprites/Default"));
		renderer.material.color = new Color(1f, 0.3f, 0f, 0.5f); // 주황색 반투명

		// 콜라이더 제거
		Destroy(fireZone.GetComponent<Collider>());

		// 틱 데미지 코루틴
		StartCoroutine(FireZoneTickDamage(position, fireZone));

		Debug.Log($"<color=yellow>[Phoenix] 화염 장판 생성! 위치: {position}</color>");
	}

	private IEnumerator FireZoneTickDamage(Vector3 position, GameObject fireZone)
	{
		float elapsed = 0f;

		while (elapsed < fireZoneDuration)
		{
			// 범위 내 적에게 틱 데미지
			Collider[] hits = Physics.OverlapSphere(position, fireZoneRadius);

			foreach (var hit in hits)
			{
				if (hit.CompareTag("Enemy"))
				{
					HitEnemy(hit.transform, fireZoneTickDamage);
				}
			}

			yield return new WaitForSeconds(fireZoneTickInterval);
			elapsed += fireZoneTickInterval;
		}

		// 장판 제거
		Destroy(fireZone);
		Debug.Log("[Phoenix] 화염 장판 소멸");
	}

	#endregion

	#region Utility

	private void HitEnemy(Transform enemy, int damage)
	{
		if (enemy == null) return;

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
			normalEnemy.Die();
		}
	}

	private void OnEnterJustFrame()
	{
		Debug.Log("<color=yellow>[Phoenix] ★ 저스트 타이밍! ★</color>");

		// 색상 변경
		if (_renderer) _renderer.material.color = justFrameColor;

		// TODO: 반짝임 이펙트, 사운드
	}

	private void OnExitJustFrame()
	{
		Debug.Log("[Phoenix] 저스트 타이밍 종료");

		// 색상 복귀
		if (_renderer) _renderer.material.color = normalColor;
	}

	#endregion

	#region TODO: Not Implemented Features

	/// <summary>
	/// TODO: 파티클 시스템
	/// - 충전 중 불꽃 모으기 이펙트
	/// - 돌진 트레일
	/// - 폭발 이펙트
	/// </summary>
	private void PlayParticleEffect(string effectName)
	{
		Debug.Log($"[Phoenix] TODO: 파티클 이펙트 ({effectName})");
	}

	/// <summary>
	/// TODO: 사운드 이펙트
	/// - 저스트 프레임 "칭-!"
	/// - 돌진 "끼에에에엑-"
	/// - 폭발음
	/// </summary>
	private void PlaySoundEffect(string soundName)
	{
		Debug.Log($"[Phoenix] TODO: 사운드 ({soundName})");
	}

	/// <summary>
	/// TODO: 카메라 연출
	/// - 탭 공격 시 줌인
	/// - 저스트 드라이브 시 쉐이크
	/// </summary>
	private void PlayCameraEffect(string effectType)
	{
		Debug.Log($"[Phoenix] TODO: 카메라 효과 ({effectType})");
	}

	/// <summary>
	/// TODO: 화염 장판 고급 연출
	/// - 확장 애니메이션
	/// - 페이드 아웃
	/// - 용암 텍스처
	/// </summary>
	private void EnhanceFireZoneVisuals(GameObject fireZone)
	{
		Debug.Log("[Phoenix] TODO: 화염 장판 고급 연출");
	}

	/// <summary>
	/// TODO: 대기 모션
	/// - 깃털 다듬기
	/// - 제자리 회전
	/// </summary>
	private void PlayIdleAnimation()
	{
		Debug.Log("[Phoenix] TODO: 대기 모션");
	}

	#endregion

	private void OnDrawGizmos()
	{
		if (!Application.isPlaying) return;

		// 패시브 탐색 범위
		Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
		Gizmos.DrawWireSphere(transform.position, passiveSearchRadius);

		// 저스트 프레임 시각화
		if (_isInJustFrame)
		{
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(transform.position, 2f);
		}

		// 드라이브 목표 지점
		if (_currentState == State.Driving)
		{
			Gizmos.color = Color.red;
			Gizmos.DrawLine(transform.position, _targetPosition);
		}
	}
}
