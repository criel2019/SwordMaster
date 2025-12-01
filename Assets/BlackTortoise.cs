using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 현무 신수 - 전장을 빠르게 튕겨다니는 연타형 신수
/// 패시브: 유영하는 등껍질 (랜덤 이동 + 튕김)
/// 탭: 12연격 (초고속 연쇄 돌진)
/// 홀드: 대지의 기운 (충전 + 저스트 타이밍)
/// 릴리즈: 영체 강림 (소멸 → 거대한 발 타격 → 재등장)
/// </summary>
public class BlackTortoise : DivineBeastBase
{
	[Header("Passive - Roaming Shell")]
	[SerializeField] private float roamRadius = 5f;
	[SerializeField] private float roamSpeed = 3f;
	[SerializeField] private float roamChangeInterval = 2f; // 방향 전환 주기
	[SerializeField] private int passiveDamage = 5;

	[Header("Tap - Rapid Pinball")]
	[SerializeField] private float tapThreshold = 0.2f;
	[SerializeField] private int rapidHitCount = 12;
	[SerializeField] private float rapidSpeed = 30f;
	[SerializeField] private float rapidInterval = 0.08f; // 타격 간격
	[SerializeField] private float rapidSearchRadius = 10f;
	[SerializeField] private int rapidDamage = 15;

	[Header("Hold - Earth Channeling")]
	[SerializeField] private float justFrameStart = 1.0f;
	[SerializeField] private float justFrameEnd = 1.2f;
	[SerializeField] private float maxChargeTime = 2f;
	[SerializeField] private float chargeScale = 0.5f; // 웅크린 크기

	[Header("Release - Spectral Stomp")]
	[SerializeField] private float stompRadius = 4f;
	[SerializeField] private float justStompRadius = 6f;
	[SerializeField] private int normalStompDamage = 80;
	[SerializeField] private int justStompDamage = 150;
	[SerializeField] private float shockwaveRadius = 8f; // 2차 충격파 (저스트)
	[SerializeField] private int shockwaveDamage = 50;

	[Header("Visual")]
	[SerializeField] private Color normalColor = new Color(0.2f, 0.3f, 0.4f); // 어두운 청록색
	[SerializeField] private Color justFrameColor = Color.cyan;

	// 내부 상태
	private Vector3 _roamDirection;
	private float _roamTimer;
	private bool _isHolding;
	private float _holdTime;
	private bool _isInJustFrame;
	private Vector3 _targetStompPosition;

	// 상태
	private enum State { Roaming, RapidAttacking, Charging, Stomping, Reappearing }
	private State _currentState = State.Roaming;

	// Rigidbody (물리 기반 튕김)
	private Rigidbody _rb;
	private Renderer _renderer;

	// 마법진 표시
	private GameObject _magicCircle;

	// 거대한 발
	private GameObject _spectralFoot;

	public override void Activate()
	{
		Debug.Log("[BlackTortoise] Activate 호출됨!");
		base.Activate();
	}

	public override void Deactivate()
	{
		Debug.Log("[BlackTortoise] Deactivate 호출됨!");
		base.Deactivate();
	}

	private void Start()
	{
		Debug.Log("[BlackTortoise] Start 시작");

		// Owner 자동 찾기
		if (owner == null)
		{
			var player = GameObject.FindGameObjectWithTag("Player");
			if (player) Initialize(player.transform);
		}

		Debug.Log("[BlackTortoise] Owner 설정 완료: " + (owner != null ? owner.name : "null"));

		// Rigidbody 설정
		Debug.Log("[BlackTortoise] Rigidbody 설정 시작");
		_rb = GetComponent<Rigidbody>();
		if (_rb == null)
		{
			_rb = gameObject.AddComponent<Rigidbody>();
			Debug.Log("[BlackTortoise] Rigidbody 새로 생성");
		}
		_rb.useGravity = false;
		_rb.constraints = RigidbodyConstraints.FreezeRotation; // 회전 고정
		_rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
		Debug.Log("[BlackTortoise] Rigidbody 설정 완료");

		// Collider 설정 (충돌 감지 필수)
		Debug.Log("[BlackTortoise] Collider 설정 시작");
		SphereCollider collider = GetComponent<SphereCollider>();
		if (collider == null)
		{
			collider = gameObject.AddComponent<SphereCollider>();
			collider.radius = 0.5f;
			Debug.Log("[BlackTortoise] Collider 새로 생성");
		}
		Debug.Log("[BlackTortoise] Collider 설정 완료");

		// 렌더러
		_renderer = GetComponent<Renderer>();
		if (_renderer == null)
		{
			var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			sphere.transform.SetParent(transform);
			sphere.transform.localPosition = Vector3.zero;
			sphere.transform.localScale = Vector3.one * 0.7f;
			_renderer = sphere.GetComponent<Renderer>();
			_renderer.material.color = normalColor;
			Destroy(sphere.GetComponent<Collider>());
		}

		// 초기 위치
		transform.position = owner.position + Vector3.right * 3f;

		// 랜덤 방향 초기화
		_roamDirection = Random.insideUnitSphere.normalized;
		_roamDirection.y = 0;

		CreateMagicCircle();
		CreateSpectralFoot();

		Debug.Log("[BlackTortoise] Start 완료!");
	}

	protected override void PassiveUpdate()
	{
		// 디버그: 첫 프레임에만 로그
		if (Time.frameCount % 300 == 0)  // 5초마다
		{
			Debug.Log($"[BlackTortoise] PassiveUpdate 실행 중, State: {_currentState}");
		}

		switch (_currentState)
		{
			case State.Roaming:
				UpdateRoaming();
				break;
			case State.RapidAttacking:
				// 코루틴이 처리
				break;
			case State.Charging:
				UpdateCharging();
				break;
			case State.Stomping:
				// 코루틴이 처리
				break;
			case State.Reappearing:
				UpdateReappearing();
				break;
		}
	}

	#region State Updates

	private void UpdateRoaming()
	{
		// 타이머
		_roamTimer += Time.deltaTime;

		if (_roamTimer >= roamChangeInterval)
		{
			// 방향 재설정
			_roamTimer = 0f;
			_roamDirection = Random.insideUnitSphere.normalized;
			_roamDirection.y = 0;
		}

		// 플레이어 중심으로부터 거리 체크
		Vector3 offsetFromPlayer = transform.position - owner.position;
		float distFromPlayer = offsetFromPlayer.magnitude;

		// 너무 멀면 플레이어 쪽으로 방향 전환
		if (distFromPlayer > roamRadius)
		{
			_roamDirection = -offsetFromPlayer.normalized;
		}

		// 이동
		_rb.linearVelocity = _roamDirection * roamSpeed;
	}

	private void UpdateCharging()
	{
		// 제자리에 고정
		_rb.linearVelocity = Vector3.zero;

		// 홀드 시간 증가는 OnInputHold에서 처리
	}

	private void UpdateReappearing()
	{
		// 스케일 복구
		float targetScale = 1f;
		float currentScale = transform.localScale.x;
		float newScale = Mathf.Lerp(currentScale, targetScale, Time.deltaTime * 5f);
		transform.localScale = Vector3.one * newScale;

		// 복구 완료
		if (Mathf.Abs(newScale - targetScale) < 0.05f)
		{
			transform.localScale = Vector3.one;
			_currentState = State.Roaming;
		}
	}

	#endregion

	#region Input Handling

	public override void OnInputDown()
	{
		if (_currentState != State.Roaming) return;

		_isHolding = true;
		_holdTime = 0f;
		_isInJustFrame = false;
	}

	public override void OnInputHold(Vector3 target)
	{
		if (!_isHolding) return;

		_holdTime += Time.deltaTime;

		// 충전 상태로 전환
		if (_currentState == State.Roaming)
		{
			_currentState = State.Charging;
			transform.localScale = Vector3.one * chargeScale; // 웅크림
			ShowMagicCircle(target);
		}

		// 마법진 위치 업데이트
		UpdateMagicCircle(target);

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
			// 탭: 12연격
			StartCoroutine(RapidPinballRoutine());
		}
		else
		{
			// 릴리즈: 영체 강림
			_targetStompPosition = target;
			StartCoroutine(SpectralStompRoutine());
		}

		HideMagicCircle();
		_holdTime = 0f;
		_isInJustFrame = false;
		if (_renderer) _renderer.material.color = normalColor;
	}

	#endregion

	#region Passive - Roaming Collision

	private void OnCollisionEnter(Collision collision)
	{
		// 적과 충돌
		if (collision.collider.CompareTag("Enemy"))
		{
			HitEnemy(collision.transform, passiveDamage);

			// 튕김 (반사)
			Vector3 normal = collision.contacts[0].normal;
			_roamDirection = Vector3.Reflect(_roamDirection, normal);
		}
		// 벽/지형과 충돌
		else
		{
			Vector3 normal = collision.contacts[0].normal;
			_roamDirection = Vector3.Reflect(_roamDirection, normal);
		}
	}

	#endregion

	#region Tap - Rapid Pinball (12연격)

	private IEnumerator RapidPinballRoutine()
	{
		_currentState = State.RapidAttacking;
		_rb.linearVelocity = Vector3.zero;

		List<Transform> hitTargets = new List<Transform>();

		for (int i = 0; i < rapidHitCount; i++)
		{
			// 가장 가까운 적 찾기 (이미 맞은 적도 포함)
			Transform target = FindClosestEnemy(transform.position, rapidSearchRadius);

			if (target == null)
			{
				Debug.Log($"[BlackTortoise] 12연격 {i + 1}번째: 타겟 없음");
				break;
			}

			// 타겟을 향해 돌진
			Vector3 direction = (target.position - transform.position).normalized;
			Vector3 startPos = transform.position;
			Vector3 targetPos = target.position;

			float traveled = 0f;
			float distance = Vector3.Distance(startPos, targetPos);

			// 빠르게 이동
			while (traveled < distance)
			{
				float step = rapidSpeed * Time.deltaTime;
				transform.position = Vector3.MoveTowards(transform.position, targetPos, step);
				traveled += step;

				// 타겟이 사라졌으면 중단
				if (target == null || !target.gameObject.activeInHierarchy)
					break;

				yield return null;
			}

			// 타격
			if (target != null && target.gameObject.activeInHierarchy)
			{
				HitEnemy(target, rapidDamage);
				hitTargets.Add(target);
				Debug.Log($"[BlackTortoise] 12연격 {i + 1}/12 타격!");
			}

			// 짧은 대기
			yield return new WaitForSeconds(rapidInterval);
		}

		Debug.Log($"<color=cyan>[BlackTortoise] 12연격 완료! 총 타격: {hitTargets.Count}회</color>");

		_currentState = State.Roaming;
	}

	#endregion

	#region Release - Spectral Stomp (영체 강림)

	private IEnumerator SpectralStompRoutine()
	{
		_currentState = State.Stomping;
		bool isJustFrame = (_holdTime >= justFrameStart && _holdTime <= justFrameEnd);

		if (isJustFrame)
		{
			Debug.Log("<color=yellow>[BlackTortoise] ★ 저스트 프레임 성공! ★</color>");
		}

		// 1. 소멸 (스케일 축소)
		float dissolveTime = 0.3f;
		float elapsed = 0f;

		while (elapsed < dissolveTime)
		{
			elapsed += Time.deltaTime;
			float t = elapsed / dissolveTime;
			transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
			yield return null;
		}

		transform.localScale = Vector3.zero; // 완전히 사라짐

		// 2. 거대한 발 타격
		yield return new WaitForSeconds(0.1f);
		ExecuteSpectralStomp(_targetStompPosition, isJustFrame);

		// 3. 재등장 (타격 위치에서)
		yield return new WaitForSeconds(0.5f);
		transform.position = _targetStompPosition + Vector3.up * 2f;

		_currentState = State.Reappearing;
	}

	private void ExecuteSpectralStomp(Vector3 position, bool isJustFrame)
	{
		// 거대한 발 표시
		ShowSpectralFoot(position);

		// 타격 범위
		float radius = isJustFrame ? justStompRadius : stompRadius;
		int damage = isJustFrame ? justStompDamage : normalStompDamage;

		// 범위 내 적 타격
		Collider[] hits = Physics.OverlapSphere(position, radius);
		int hitCount = 0;

		foreach (var hit in hits)
		{
			if (hit.CompareTag("Enemy"))
			{
				HitEnemy(hit.transform, damage);
				hitCount++;
			}
		}

		Debug.Log($"<color=red>[BlackTortoise] 영체 강림! {hitCount}명 타격 (반경: {radius}m)</color>");

		// 저스트 프레임이면 2차 충격파
		if (isJustFrame)
		{
			StartCoroutine(ShockwaveRoutine(position));
		}

		// 발 숨김
		StartCoroutine(HideSpectralFootRoutine());
	}

	private IEnumerator ShockwaveRoutine(Vector3 center)
	{
		yield return new WaitForSeconds(0.3f);

		// 2차 충격파
		Collider[] hits = Physics.OverlapSphere(center, shockwaveRadius);
		int hitCount = 0;

		foreach (var hit in hits)
		{
			if (hit.CompareTag("Enemy"))
			{
				HitEnemy(hit.transform, shockwaveDamage);
				hitCount++;
			}
		}

		Debug.Log($"<color=yellow>[BlackTortoise] 2차 충격파! {hitCount}명 추가 타격</color>");

		// TODO: 충격파 시각 효과
	}

	#endregion

	#region Utility

	private Transform FindClosestEnemy(Vector3 position, float radius)
	{
		Collider[] hits = Physics.OverlapSphere(position, radius);
		Transform closest = null;
		float closestDist = float.MaxValue;

		foreach (var hit in hits)
		{
			if (!hit.CompareTag("Enemy")) continue;

			float dist = Vector3.Distance(position, hit.transform.position);
			if (dist < closestDist)
			{
				closestDist = dist;
				closest = hit.transform;
			}
		}

		return closest;
	}

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
		Debug.Log("<color=yellow>[BlackTortoise] ★ 저스트 타이밍! ★</color>");

		// 색상 변경
		if (_renderer) _renderer.material.color = justFrameColor;

		// 마법진 밝게
		if (_magicCircle != null)
		{
			Renderer mr = _magicCircle.GetComponent<Renderer>();
			if (mr) mr.material.color = justFrameColor;
		}
	}

	private void OnExitJustFrame()
	{
		Debug.Log("[BlackTortoise] 저스트 타이밍 종료");

		// 색상 복귀
		if (_renderer) _renderer.material.color = normalColor;

		// 마법진 어둡게
		if (_magicCircle != null)
		{
			Renderer mr = _magicCircle.GetComponent<Renderer>();
			if (mr) mr.material.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
		}
	}

	#endregion

	#region Visual Effects

	private void CreateMagicCircle()
	{
		_magicCircle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		_magicCircle.name = "MagicCircle";
		_magicCircle.transform.localScale = new Vector3(stompRadius * 2f, 0.01f, stompRadius * 2f);

		Renderer renderer = _magicCircle.GetComponent<Renderer>();
		renderer.material = new Material(Shader.Find("Sprites/Default"));
		renderer.material.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);

		Destroy(_magicCircle.GetComponent<Collider>());
		_magicCircle.SetActive(false);
	}

	private void ShowMagicCircle(Vector3 position)
	{
		if (_magicCircle == null) return;

		_magicCircle.SetActive(true);
		_magicCircle.transform.position = position + Vector3.up * 0.1f;
	}

	private void UpdateMagicCircle(Vector3 position)
	{
		if (_magicCircle == null || !_magicCircle.activeSelf) return;

		_magicCircle.transform.position = position + Vector3.up * 0.1f;

		// 저스트 프레임이면 크기 증가
		float scale = _isInJustFrame ? justStompRadius : stompRadius;
		_magicCircle.transform.localScale = new Vector3(scale * 2f, 0.01f, scale * 2f);
	}

	private void HideMagicCircle()
	{
		if (_magicCircle != null)
			_magicCircle.SetActive(false);
	}

	private void CreateSpectralFoot()
	{
		// 거대한 발 (간단하게 큐브로 표현)
		_spectralFoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
		_spectralFoot.name = "SpectralFoot";
		_spectralFoot.transform.localScale = new Vector3(3f, 1f, 4f);

		Renderer renderer = _spectralFoot.GetComponent<Renderer>();
		renderer.material = new Material(Shader.Find("Sprites/Default"));
		renderer.material.color = new Color(0.2f, 0.3f, 0.4f, 0.6f); // 반투명

		Destroy(_spectralFoot.GetComponent<Collider>());
		_spectralFoot.SetActive(false);
	}

	private void ShowSpectralFoot(Vector3 position)
	{
		if (_spectralFoot == null) return;

		_spectralFoot.SetActive(true);
		_spectralFoot.transform.position = position + Vector3.up * 5f; // 위에서 떨어지는 연출

		// 떨어지는 애니메이션
		StartCoroutine(FootDropAnimation(position));
	}

	private IEnumerator FootDropAnimation(Vector3 targetPos)
	{
		Vector3 startPos = targetPos + Vector3.up * 5f;
		Vector3 endPos = targetPos + Vector3.up * 0.5f;

		float duration = 0.2f;
		float elapsed = 0f;

		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float t = elapsed / duration;
			_spectralFoot.transform.position = Vector3.Lerp(startPos, endPos, t);
			yield return null;
		}

		_spectralFoot.transform.position = endPos;
	}

	private IEnumerator HideSpectralFootRoutine()
	{
		yield return new WaitForSeconds(0.5f);

		if (_spectralFoot != null)
			_spectralFoot.SetActive(false);
	}

	#endregion

	#region TODO: Not Implemented Features

	/// <summary>
	/// TODO: Dissolve Shader
	/// - 사라질 때 모래처럼 흩어지는 효과
	/// - Alpha Cutoff나 Dissolve Amount 조절
	/// </summary>
	private void PlayDissolveEffect(bool fadeOut)
	{
		Debug.Log($"[BlackTortoise] TODO: Dissolve Shader ({(fadeOut ? "Out" : "In")})");
	}

	/// <summary>
	/// TODO: 타격 사운드
	/// - 12연격: 가벼운 타악기 "탁탁탁"
	/// - 영체 강림: 무거운 "쿵-"
	/// </summary>
	private void PlayHitSound(bool isHeavy)
	{
		Debug.Log($"[BlackTortoise] TODO: 타격 사운드 ({(isHeavy ? "Heavy" : "Light")})");
	}

	/// <summary>
	/// TODO: 충격파 시각 효과
	/// - 링 형태로 퍼져나가는 이펙트
	/// </summary>
	private void PlayShockwaveEffect(Vector3 position)
	{
		Debug.Log($"[BlackTortoise] TODO: 충격파 이펙트");
	}

	/// <summary>
	/// TODO: 마법진 고급 연출
	/// - 회전하는 룬 패턴
	/// - 균열 이펙트
	/// </summary>
	private void EnhanceMagicCircle()
	{
		Debug.Log("[BlackTortoise] TODO: 마법진 고급 연출");
	}

	#endregion

	private void OnDrawGizmos()
	{
		if (!Application.isPlaying) return;

		// 로밍 범위
		if (owner != null)
		{
			Gizmos.color = new Color(0.2f, 0.8f, 0.8f, 0.2f);
			Gizmos.DrawWireSphere(owner.position, roamRadius);
		}

		// 12연격 탐색 범위
		if (_currentState == State.RapidAttacking)
		{
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireSphere(transform.position, rapidSearchRadius);
		}

		// 영체 강림 범위
		if (_currentState == State.Stomping)
		{
			Gizmos.color = Color.red;
			Gizmos.DrawWireSphere(_targetStompPosition, stompRadius);

			if (_isInJustFrame)
			{
				Gizmos.color = Color.yellow;
				Gizmos.DrawWireSphere(_targetStompPosition, shockwaveRadius);
			}
		}
	}

	private void OnDestroy()
	{
		if (_magicCircle != null) Destroy(_magicCircle);
		if (_spectralFoot != null) Destroy(_spectralFoot);
	}
}
