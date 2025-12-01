using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 신수 오브 시스템 - 다중 공 회전 및 퍼펙트 릴리즈
/// ElasticTether 기능 통합 버전
/// </summary>
public class OrbBeast : DivineBeastBase
{
	[Header("Multi-Orb Settings")]
	[SerializeField] private int maxOrbCount = 5;
	[SerializeField] private float spawnInterval = 1.0f;
	[SerializeField] private GameObject orbPrefab;
	[SerializeField] private Collider playerCollider;
	[SerializeField] private float orbLifeTime = 5.0f;
	[SerializeField] private float spawnScaleTime = 0.5f;

	[Header("Orbit Settings")]
	[SerializeField] private float startRadius = 3f;
	[SerializeField] private float minRadius = 3f;
	[SerializeField] private float maxRadius = 25f;
	[SerializeField] private float expansionSpeed = 30f; // 빠르게 확장
	[SerializeField] private float retractSpeed = 11f;   // 가속과 동기화 (약 2초)

	[Header("Rotation Speed (4 Phases)")]
	[SerializeField] private float[] phaseSpeeds = { 360f, 720f, 1440f, 2500f };
	[SerializeField] private float acceleration = 1200f;

	[Header("Perfect Release")]
	[SerializeField] private float perfectWindowDuration = 0.6f;
	[SerializeField] private Color perfectColor = Color.yellow;
	[SerializeField] private Color normalColor = new Color(1f, 0.4f, 0f);
	[SerializeField] private float releaseMultiplier = 1.8f;

	[Header("Passive Damage")]
	[SerializeField] private float passiveDamageRadius = 0.8f;

	[Header("Trail Settings")]
	[SerializeField] private Color trailStartColor = new Color(0.4f, 0.8f, 1f, 1f);
	[SerializeField] private Color trailEndColor = new Color(0.2f, 0.5f, 1f, 0f);
	[SerializeField] private float trailWidth = 0.8f;
	[SerializeField] private float trailTime = 0.3f;

	// 내부 상태
	private List<Rigidbody> _activeOrbs = new List<Rigidbody>();
	private List<TrailRenderer> _orbTrails = new List<TrailRenderer>();

	private bool _isCharging;
	private bool _isReplenishing;
	private float _currentRadius;
	private float _currentAngleDeg;
	private float _currentRotationSpeed;
	private float _chargeTimer;

	// 퍼펙트 타이밍
	private float _perfectWindowTimer = 0f;
	private bool _isInPerfectZone = false;
	private float _coyoteTimer = 0f;
	private bool _hasReachedMinRadius = false;
	private bool _hasExpanded = false; // 확장 완료 여부
	private bool _autoFired = false;   // 자동 발사 방지용

	private void Start()
	{
		_currentRadius = startRadius;
		_currentRotationSpeed = phaseSpeeds[0];

		// Owner 자동 찾기
		if (owner == null)
		{
			var p = GameObject.FindGameObjectWithTag("Player");
			if (p) Initialize(p.transform);
		}

		StartCoroutine(Routine_ReplenishOrbs());
	}

	protected override void PassiveUpdate()
	{
		if (_activeOrbs.Count > 0)
		{
			ProcessMultiOrbRotation();
			CheckPerfectTiming();
			CheckPassiveCollision();
		}
	}

	public override void OnInputDown()
	{
		if (_activeOrbs.Count == 0) return;
		if (_isCharging) return;

		_isCharging = true;
		_chargeTimer = 0f;
		_hasReachedMinRadius = false;
		_hasExpanded = false;
		_autoFired = false;
		_perfectWindowTimer = 0f;
		_isInPerfectZone = false;
		_coyoteTimer = 0f;

		// 모든 오브를 키네마틱으로
		foreach (var rb in _activeOrbs)
		{
			if (rb) rb.isKinematic = true;
		}

		UpdateOrbColors(normalColor);
	}

	public override void OnInputHold(Vector3 target)
	{
		// 시각 피드백만 (필요 시 구현)
	}

	public override void OnInputUp(Vector3 target)
	{
		if (!_isCharging) return;

		// Q를 떼면 즉시 발사
		bool isPerfect = _isInPerfectZone || (_coyoteTimer > 0f);
		FireAllOrbs(isPerfect);
	}

	private void ProcessMultiOrbRotation()
	{
		if (_isCharging)
		{
			_chargeTimer += Time.deltaTime;

			// === 1단계: 확장 (빠르게) ===
			if (!_hasExpanded)
			{
				_currentRadius = Mathf.MoveTowards(_currentRadius, maxRadius, expansionSpeed * Time.deltaTime);

				// 확장 완료 체크
				if (Mathf.Abs(_currentRadius - maxRadius) < 0.5f)
				{
					_hasExpanded = true;
					_chargeTimer = 0f; // 수축 시작 시 타이머 리셋
				}
			}
			// === 2단계: 수축 + 가속 (동시 진행) ===
			else
			{
				// 4단계 속도 증가 (약 2초)
				int targetIndex = Mathf.Min(Mathf.FloorToInt(_chargeTimer), phaseSpeeds.Length - 1);
				_currentRotationSpeed = Mathf.MoveTowards(
					_currentRotationSpeed,
					phaseSpeeds[targetIndex],
					acceleration * Time.deltaTime
				);

				// 반경 수축 (약 2초, 속도와 동기화)
				_currentRadius = Mathf.MoveTowards(_currentRadius, minRadius, retractSpeed * Time.deltaTime);

				// 최소 반경 도달 체크
				if (Mathf.Abs(_currentRadius - minRadius) < 0.5f)
				{
					_hasReachedMinRadius = true;
				}
			}
		}
		else
		{
			// 평소: 천천히 원래 반경으로 복귀
			_currentRotationSpeed = Mathf.Lerp(_currentRotationSpeed, phaseSpeeds[0], Time.deltaTime);
			_currentRadius = Mathf.Lerp(_currentRadius, startRadius, Time.deltaTime * 5f);
		}

		// 회전 각도 갱신
		_currentAngleDeg += _currentRotationSpeed * Time.deltaTime;
		if (_currentAngleDeg >= 360f) _currentAngleDeg -= 360f;

		// 모든 오브 위치 계산 (등간격 배치)
		float angleStep = 360f / maxOrbCount;
		for (int i = 0; i < _activeOrbs.Count; i++)
		{
			Rigidbody rb = _activeOrbs[i];
			if (rb == null) continue;

			float rad = (_currentAngleDeg + (angleStep * i)) * Mathf.Deg2Rad;
			float x = Mathf.Cos(rad) * _currentRadius;
			float z = Mathf.Sin(rad) * _currentRadius;
			Vector3 targetPos = owner.position + new Vector3(x, 0f, z);

			if (rb.isKinematic)
			{
				rb.MovePosition(targetPos);
			}
			else
			{
				rb.transform.position = targetPos;
			}

			// 회전도 적용 (공이 회전하는 느낌)
			rb.MoveRotation(Quaternion.Euler(0, -(_currentAngleDeg + (angleStep * i)), 0));
		}
	}

	private void CheckPerfectTiming()
	{
		if (!_isCharging) return;
		if (!_hasExpanded) return; // 확장 중에는 체크 안 함

		// Coyote 타이머 감소
		if (_coyoteTimer > 0f) _coyoteTimer -= Time.deltaTime;

		// 최소 반경 도달 후 퍼펙트 윈도우 체크
		if (_hasReachedMinRadius && !_autoFired)
		{
			_perfectWindowTimer += Time.deltaTime;

			if (_perfectWindowTimer <= perfectWindowDuration)
			{
				_isInPerfectZone = true;
				_coyoteTimer = 0.2f; // Coyote time
				UpdateOrbColors(perfectColor);
			}
			else
			{
				// 퍼펙트 윈도우 종료 → 자동 일반 발사 (1회만)
				_isInPerfectZone = false;
				_autoFired = true;
				FireAllOrbs(false);
			}
		}
		else if (!_hasReachedMinRadius)
		{
			// 아직 수축 중
			_perfectWindowTimer = 0f;
			_isInPerfectZone = false;

			if (_coyoteTimer > 0f)
			{
				UpdateOrbColors(perfectColor);
			}
			else
			{
				UpdateOrbColors(normalColor);
			}
		}
	}

	private void FireAllOrbs(bool isPerfect)
	{
		if (_activeOrbs.Count == 0) return;

		_isCharging = false;

		float angleStep = 360f / maxOrbCount;

		for (int i = 0; i < _activeOrbs.Count; i++)
		{
			Rigidbody rb = _activeOrbs[i];
			if (rb == null) continue;

			rb.isKinematic = false;

			// 접선 방향으로 발사
			float rad = (_currentAngleDeg + (angleStep * i)) * Mathf.Deg2Rad;
			Vector3 tangentDir = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));

			// === 각운동량 보존 법칙 적용 ===
			// 최대 반경에서의 선속도 기준
			float baseLinearSpeed = maxRadius * (_currentRotationSpeed * Mathf.Deg2Rad);

			// 현재 반경(최소 반경)에서의 실제 속도 (각운동량 보존)
			// v₂ = v₁ × (r₁ / r₂)
			float conservedSpeed = baseLinearSpeed * (maxRadius / _currentRadius);

			// 발사 배율 적용
			float speedMult = isPerfect ? releaseMultiplier * 1.2f : releaseMultiplier;
			float finalSpeed = conservedSpeed * speedMult;

			rb.linearVelocity = tangentDir * finalSpeed;

			// RicochetBall 연동
			RicochetBall rico = rb.GetComponent<RicochetBall>();
			if (rico != null)
			{
				rico.Activate(isPerfect);
			}
		}

		// 발사 후 처리
		StartCoroutine(Routine_AfterFireSequence());

		if (isPerfect)
		{
			Debug.Log("<color=yellow>★★★ PERFECT RELEASE! ★★★</color>");
		}
	}

	private void CheckPassiveCollision()
	{
		// 회전 중 적과 충돌 시 데미지
		foreach (var rb in _activeOrbs)
		{
			if (rb == null) continue;

			Collider[] hits = Physics.OverlapSphere(rb.position, passiveDamageRadius);
			foreach (var hit in hits)
			{
				if (hit.CompareTag("Enemy"))
				{
					var enemy = hit.GetComponent<IEnemy>();
					if (enemy != null)
					{
						enemy.Die();
					}
				}
			}
		}
	}

	private void UpdateOrbColors(Color color)
	{
		foreach (var rb in _activeOrbs)
		{
			if (rb == null) continue;
			Renderer r = rb.GetComponent<Renderer>();
			if (r && r.material)
			{
				r.material.color = color;
			}
		}
	}

	private IEnumerator Routine_ReplenishOrbs()
	{
		while (true)
		{
			if (_activeOrbs.Count < maxOrbCount && !_isReplenishing)
			{
				SpawnOneOrb();
				yield return new WaitForSeconds(spawnInterval);
			}
			else
			{
				yield return null;
			}
		}
	}

	private void SpawnOneOrb()
	{
		if (orbPrefab == null)
		{
			Debug.LogError("OrbBeast: orbPrefab이 할당되지 않았습니다!");
			return;
		}

		GameObject newObj = Instantiate(orbPrefab, owner.position, Quaternion.identity);
		newObj.SetActive(true);

		Rigidbody rb = newObj.GetComponent<Rigidbody>();
		if (rb == null)
		{
			rb = newObj.AddComponent<Rigidbody>();
		}

		rb.interpolation = RigidbodyInterpolation.Interpolate;
		rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
		rb.isKinematic = true;
		rb.useGravity = false;

		// 플레이어와 충돌 무시
		if (playerCollider != null)
		{
			Collider orbCol = newObj.GetComponent<Collider>();
			if (orbCol)
			{
				Physics.IgnoreCollision(playerCollider, orbCol);
			}
		}

		// 트레일 생성
		TrailRenderer trail = CreateTrail(newObj.transform);
		_orbTrails.Add(trail);

		_activeOrbs.Add(rb);
		StartCoroutine(Routine_ScaleUp(newObj.transform));
	}

	private TrailRenderer CreateTrail(Transform parent)
	{
		GameObject trailObj = new GameObject("OrbTrail");
		trailObj.transform.SetParent(parent);
		trailObj.transform.localPosition = Vector3.zero;

		TrailRenderer trail = trailObj.AddComponent<TrailRenderer>();
		trail.time = trailTime;
		trail.startWidth = trailWidth;
		trail.endWidth = trailWidth * 0.3f;
		trail.minVertexDistance = 0.1f;
		trail.autodestruct = false;
		trail.emitting = true;
		trail.numCapVertices = 4;
		trail.numCornerVertices = 4;
		trail.startColor = trailStartColor;
		trail.endColor = trailEndColor;

		Material trailMat = new Material(Shader.Find("Sprites/Default"));
		trail.material = trailMat;
		trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		trail.receiveShadows = false;

		return trail;
	}

	private IEnumerator Routine_AfterFireSequence()
	{
		_isReplenishing = true;

		List<Rigidbody> firedOrbs = new List<Rigidbody>(_activeOrbs);
		_activeOrbs.Clear();
		_orbTrails.Clear();

		// 초기 상태로 리셋
		_currentRadius = startRadius;
		_currentRotationSpeed = phaseSpeeds[0];
		_chargeTimer = 0f;
		_hasReachedMinRadius = false;
		_hasExpanded = false;
		_autoFired = false;
		_perfectWindowTimer = 0f;

		// 발사된 오브들이 살아있는 동안 대기
		yield return new WaitForSeconds(orbLifeTime);

		// 발사된 오브 삭제
		foreach (var rb in firedOrbs)
		{
			if (rb != null)
			{
				Destroy(rb.gameObject);
			}
		}

		_isReplenishing = false;
	}

	private IEnumerator Routine_ScaleUp(Transform target)
	{
		if (target == null) yield break;

		float timer = 0f;
		Vector3 finalScale = target.localScale;
		target.localScale = Vector3.zero;

		while (timer < spawnScaleTime)
		{
			if (target == null) yield break;

			timer += Time.deltaTime;
			float t = Mathf.SmoothStep(0f, 1f, timer / spawnScaleTime);
			target.localScale = Vector3.Lerp(Vector3.zero, finalScale, t);
			yield return null;
		}

		if (target != null)
		{
			target.localScale = finalScale;
		}
	}
}