using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ElasticTether : MonoBehaviour
{
	// ... (기존 설정 변수들 동일) ...
	[Header("Multi-Weapon Settings")]
	[SerializeField] private int maxWeaponCount = 5;
	[SerializeField] private float spawnInterval = 1.0f;

	[Header("Prefab & Spawning")]
	[SerializeField] private GameObject weaponPrefab;
	[SerializeField] private Collider playerCollider;
	[SerializeField] private float lifeTime = 5.0f;
	[SerializeField] private float spawnScaleTime = 0.5f;

	[Header("Spiral Settings")]
	[SerializeField] private float startRadius = 3f;
	[SerializeField] private float minRadius = 3f;
	[SerializeField] private float maxRadius = 25f;
	[SerializeField] private float expansionSpeed = 12f;
	[SerializeField] private float retractSpeed = 30f;

	[Header("Speed Settings")]
	[SerializeField] private float[] phaseSpeeds = { 360f, 720f, 1440f, 2500f };
	[SerializeField] private float acceleration = 1200f;

	[Header("Release")]
	[SerializeField] private float releaseMultiplier = 1.8f;

	[Header("Skill Check (Perfect Reload)")]
	// [수정] 0.3초 -> 0.6초로 넉넉하게 변경 (사람 반응속도 고려)
	[SerializeField] private float perfectWindowDuration = 0.6f;
	[SerializeField] private Color perfectColor = Color.yellow;
	[SerializeField] private Color normalColor = new Color(1f, 0.4f, 0f);

	[Header("Decoy & Guillotine Settings")]
	[SerializeField] private float swapCooldown = 0.05f;
	[SerializeField] private float justDodgeRadius = 8.0f;
	[SerializeField] private float decoyFuseTime = 3.0f;
	[SerializeField] private float guillotineWidth = 3.0f;
	[SerializeField] private float guillotineForce = 8000f;
	[SerializeField] private LineRenderer slashLine;

	private List<Rigidbody> _activeWeapons = new List<Rigidbody>();
	private bool _isCharging;
	private bool _isRetracting;
	private bool _isReplenishing;
	private float _lastSwapTime;

	private float _currentRadius;
	private float _currentAngleDeg;
	private float _currentRotationSpeed;
	private float _chargeTimer;

	// 퍼펙트 타이밍 관련
	private float _perfectWindowTimer = 0f;
	private bool _isInPerfectZone = false;

	// [신규] 버튼을 떼도 잠시동안 판정을 유지해주는 '코요테 타임' 변수
	private float _coyoteTimer = 0f;

	public bool HasWeapon => _activeWeapons.Count > 0;
	public Rigidbody TargetWeapon { get; private set; }

	private void Start()
	{
		_currentRadius = startRadius;
		_currentRotationSpeed = phaseSpeeds[0];
		StartCoroutine(Routine_ReplenishWeapons());
	}

	public void OnCharging()
	{
		if (_activeWeapons.Count == 0) return;
		_isCharging = true;
		foreach (var rb in _activeWeapons) if (rb) rb.isKinematic = true;
	}

	public void OnRelease()
	{
		if (_activeWeapons.Count == 0 || !_isCharging) return;

		_isCharging = false;
		_isRetracting = false;

		// [핵심] 현재 퍼펙트 존이거나, 코요테 타임이 남아있다면 성공!
		bool isPerfectShot = _isInPerfectZone || (_coyoteTimer > 0f);

		if (isPerfectShot)
		{
			Debug.Log("<color=yellow>★ PERFECT SHOT! (Ping-Ping-Ping) ★</color>");
		}
		else
		{
			Debug.Log("<color=grey>Normal Shot...</color>");
		}

		ThrowAllWeapons(isPerfectShot);
		StartCoroutine(Routine_AfterFireSequence());
	}

	public void SetRetract(bool retract)
	{
		_isRetracting = retract;

		// [수정] 여기서 _isInPerfectZone을 끄지 않음!
		// 버튼을 떼더라도 FixedUpdate의 코요테 타임 로직이 자연스럽게 꺼지도록 둠.
		if (!retract)
		{
			UpdateWeaponColors(normalColor);
		}
	}

	// ... (Targeting, Swap 등 기존 함수 동일 유지) ...
	public void UpdateTargeting(Vector3 mousePos)
	{
		if (_activeWeapons.Count == 0) { TargetWeapon = null; return; }
		Rigidbody closest = null; float minDst = float.MaxValue;
		foreach (var rb in _activeWeapons)
		{
			if (rb == null) continue;
			float dst = Vector3.Distance(mousePos, rb.position);
			if (dst < minDst) { minDst = dst; closest = rb; }
		}
		TargetWeapon = closest;
	}

	public void PerformDecoySwap(Vector3 targetPos)
	{
		if (_activeWeapons.Count == 0) return;
		if (Time.time < _lastSwapTime + swapCooldown) return;
		_lastSwapTime = Time.time;
		int weaponIdx = _activeWeapons.Count - 1;
		Rigidbody decoyRb = _activeWeapons[weaponIdx];
		_activeWeapons.RemoveAt(weaponIdx);
		bool isJustDodge = CheckJustDodge();
		Vector3 startPos = transform.position; Vector3 endPos = targetPos;
		transform.position = endPos;
		if (GetComponent<Rigidbody>()) GetComponent<Rigidbody>().MovePosition(endPos);
		decoyRb.transform.position = startPos; decoyRb.isKinematic = false;
		decoyRb.linearVelocity = Random.onUnitSphere * 20f;
		decoyRb.linearVelocity = new Vector3(decoyRb.linearVelocity.x, 0, decoyRb.linearVelocity.z);
		ExplosiveDecoy explosive = decoyRb.gameObject.AddComponent<ExplosiveDecoy>();
		explosive.Setup(decoyFuseTime);
		ProcessGuillotine(startPos, endPos, isJustDodge);
		if (isJustDodge)
		{
			explosive.DetonateInstant(2.0f);
			StartCoroutine(Routine_TimeSlow(0.1f, 0.3f));
		}
	}

	private void FixedUpdate()
	{
		if (_activeWeapons.Count > 0)
		{
			ProcessMultiSpiralKinematic();
			CheckPerfectTiming(); // 타이밍 체크
		}
	}

	/// <summary>
	/// 퍼펙트 타이밍 체크 (코요테 타임 적용)
	/// </summary>
	private void CheckPerfectTiming()
	{
		// 1. 코요테 타임 감소
		if (_coyoteTimer > 0f) _coyoteTimer -= Time.fixedDeltaTime;

		// 2. 당기는 중이고, 완전히 당겨졌을 때
		if (_isCharging && _isRetracting && Mathf.Abs(_currentRadius - minRadius) < 0.5f)
		{
			_perfectWindowTimer += Time.fixedDeltaTime;

			// 0.6초 이내면 퍼펙트
			if (_perfectWindowTimer <= perfectWindowDuration)
			{
				_isInPerfectZone = true;
				_coyoteTimer = 0.2f; // 성공 중일 때는 코요테 타임 계속 충전 (0.2초 여유)
				UpdateWeaponColors(perfectColor);
			}
			else
			{
				// 시간 초과 (너무 오래 잡고 있었음)
				_isInPerfectZone = false;
				UpdateWeaponColors(normalColor);
			}
		}
		else
		{
			// 당기기 멈췄거나 멀어졌을 때
			_perfectWindowTimer = 0f;
			_isInPerfectZone = false;

			// 코요테 타임이 남아있다면 아직 노란색 유지!
			if (_coyoteTimer > 0f && _isCharging)
			{
				UpdateWeaponColors(perfectColor);
			}
			else if (_isCharging)
			{
				UpdateWeaponColors(normalColor);
			}
		}
	}

	private void UpdateWeaponColors(Color color)
	{
		foreach (var rb in _activeWeapons)
		{
			if (rb == null) continue;
			Renderer r = rb.GetComponent<Renderer>();
			if (r) r.material.color = color;
		}
	}

	// ... (ThrowAllWeapons 등 나머지 로직은 100% 동일) ...
	private void ThrowAllWeapons(bool isPerfect)
	{
		for (int i = 0; i < _activeWeapons.Count; i++)
		{
			Rigidbody rb = _activeWeapons[i];
			if (rb == null) continue;
			rb.isKinematic = false;
			float angleOffset = (360f / maxWeaponCount) * i;
			float rad = (_currentAngleDeg + angleOffset) * Mathf.Deg2Rad;
			Vector3 tangentDir = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));
			float linearSpeed = _currentRadius * (_currentRotationSpeed * Mathf.Deg2Rad);
			float speedMult = isPerfect ? releaseMultiplier * 1.2f : releaseMultiplier;
			rb.linearVelocity = tangentDir * (linearSpeed * speedMult);

			RicochetBall rico = rb.GetComponent<RicochetBall>();
			if (rico != null) rico.Activate(isPerfect);
		}
	}

	// ... (나머지 전체 로직 동일) ...
	private void ProcessMultiSpiralKinematic()
	{
		if (_isCharging)
		{
			_chargeTimer += Time.fixedDeltaTime;
			int targetIndex = Mathf.Min(Mathf.FloorToInt(_chargeTimer), phaseSpeeds.Length - 1);
			_currentRotationSpeed = Mathf.MoveTowards(_currentRotationSpeed, phaseSpeeds[targetIndex], acceleration * Time.fixedDeltaTime);
			float targetRadius = _isRetracting ? minRadius : maxRadius;
			float moveSpeed = _isRetracting ? retractSpeed : expansionSpeed;
			_currentRadius = Mathf.MoveTowards(_currentRadius, targetRadius, moveSpeed * Time.fixedDeltaTime);
		}
		else
		{
			_currentRotationSpeed = Mathf.Lerp(_currentRotationSpeed, phaseSpeeds[0], Time.fixedDeltaTime);
			_currentRadius = Mathf.Lerp(_currentRadius, startRadius, Time.fixedDeltaTime * 5f);
		}
		_currentAngleDeg += _currentRotationSpeed * Time.fixedDeltaTime;
		if (_currentAngleDeg >= 360f) _currentAngleDeg -= 360f;
		float angleStep = 360f / maxWeaponCount;
		for (int i = 0; i < _activeWeapons.Count; i++)
		{
			Rigidbody rb = _activeWeapons[i]; if (rb == null) continue;
			float rad = (_currentAngleDeg + (angleStep * i)) * Mathf.Deg2Rad;
			float x = Mathf.Cos(rad) * _currentRadius; float z = Mathf.Sin(rad) * _currentRadius;
			Vector3 targetPos = transform.position + new Vector3(x, 0f, z);
			rb.MovePosition(targetPos); rb.MoveRotation(Quaternion.Euler(0, -(_currentAngleDeg + (angleStep * i)), 0));
		}
	}

	private void ProcessGuillotine(Vector3 s, Vector3 e, bool c)
	{
		Vector3 d = e - s; float dist = d.magnitude; if (dist < 0.5f) return;
		float r = c ? guillotineWidth * 2f : guillotineWidth;
		RaycastHit[] hits = Physics.SphereCastAll(s, r, d.normalized, dist);
		foreach (var h in hits)
		{
			if (h.collider.CompareTag("Enemy"))
			{
				Rigidbody er = h.collider.GetComponent<Rigidbody>();
				if (er) { er.AddForce((d.normalized + Vector3.up).normalized * guillotineForce, ForceMode.Impulse); er.AddTorque(Random.insideUnitSphere * guillotineForce, ForceMode.Impulse); }
				Destroy(h.collider.gameObject, 0.1f);
			}
		}
		if (slashLine != null) StartCoroutine(Routine_ShowSlash(s, e, c));
	}

	private IEnumerator Routine_ShowSlash(Vector3 s, Vector3 e, bool c)
	{
		slashLine.enabled = true; slashLine.positionCount = 2; slashLine.SetPosition(0, s); slashLine.SetPosition(1, e);
		float w = c ? 2.0f : 0.5f; slashLine.startWidth = w; slashLine.endWidth = w;
		Color col = c ? Color.red : Color.white; slashLine.startColor = col; slashLine.endColor = new Color(col.r, col.g, col.b, 0f);
		yield return new WaitForSeconds(0.2f); slashLine.enabled = false;
	}
	private bool CheckJustDodge() { Collider[] hits = Physics.OverlapSphere(transform.position, justDodgeRadius); foreach (var h in hits) if (h.CompareTag("Enemy")) return true; return false; }
	private IEnumerator Routine_TimeSlow(float s, float d) { Time.timeScale = s; Time.fixedDeltaTime = 0.02f * s; yield return new WaitForSecondsRealtime(d); Time.timeScale = 1.0f; Time.fixedDeltaTime = 0.02f; }
	private IEnumerator Routine_ReplenishWeapons() { while (true) { if (_activeWeapons.Count < maxWeaponCount && !_isReplenishing) { SpawnOneWeapon(); yield return new WaitForSeconds(spawnInterval); } else { yield return null; } } }
	private void SpawnOneWeapon() { if (!weaponPrefab) return; GameObject n = Instantiate(weaponPrefab, transform.position, Quaternion.identity); n.SetActive(true); Rigidbody r = n.GetComponent<Rigidbody>(); if (r) { r.interpolation = RigidbodyInterpolation.Interpolate; r.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; r.isKinematic = true; } if (playerCollider) { Collider c = n.GetComponent<Collider>(); if (c) Physics.IgnoreCollision(playerCollider, c); } _activeWeapons.Add(r); StartCoroutine(Routine_ScaleUp(n.transform)); }
	private IEnumerator Routine_AfterFireSequence() { _isReplenishing = true; List<Rigidbody> f = new List<Rigidbody>(_activeWeapons); _activeWeapons.Clear(); _currentRadius = startRadius; _currentRotationSpeed = phaseSpeeds[0]; _chargeTimer = 0f; yield return new WaitForSeconds(lifeTime); foreach (var r in f) if (r != null) Destroy(r.gameObject); _isReplenishing = false; }
	private IEnumerator Routine_ScaleUp(Transform t) { float timer = 0f; Vector3 f = Vector3.one * 2f; if (t) f = t.localScale; t.localScale = Vector3.zero; while (timer < spawnScaleTime) { if (!t) yield break; timer += Time.deltaTime; float v = Mathf.SmoothStep(0f, 1f, timer / spawnScaleTime); t.localScale = Vector3.Lerp(Vector3.zero, f, v); yield return null; } if (t) t.localScale = f; }
}