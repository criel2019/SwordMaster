using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SwordMaster : MonoBehaviour
{
	[Header("Flash Cut (Spacebar)")]
	[SerializeField] private float flashDistance = 7.0f;
	[SerializeField] private float flashWidth = 2.5f;
	[SerializeField] private float flashCooldown = 0.8f;
	[SerializeField] private LayerMask obstacleLayer;
	[SerializeField] private TrailRenderer flashTrail; // Trail로 변경

	[Header("Basic Slash (Left Click Tap)")]
	[SerializeField] private float slashRange = 5.0f;        // 전방 공격 거리 (검기보다 짧음)
	[SerializeField] private float slashWidth = 3.0f;        // 베기 너비
	[SerializeField] private float slashCooldown = 0.2f;
	[SerializeField] private GameObject slashProjectilePrefab; // 전방으로 나가는 베기 이펙트

	[Header("Heavy Wave (Left Click Hold)")]
	[SerializeField] private float minWaveSize = 2.0f;
	[SerializeField] private float maxWaveSize = 8.0f;
	[SerializeField] private float chargeTimeForMax = 1.2f;
	[SerializeField] private float waveSpeed = 40f;
	[SerializeField] private GameObject wavePrefab;

	[Header("Perfect Timing System")]
	[SerializeField] private float perfectTimingWindow = 0.15f;  // 완벽 타이밍 허용 오차
	[SerializeField] private float perfectTimingPoint = 0.8f;    // 완벽 타이밍 시점 (chargeTimeForMax 기준 비율)
	[SerializeField] private float perfectBonusSize = 1.5f;      // 완벽 시 크기 보너스
	[SerializeField] private float perfectBonusSpeed = 1.3f;     // 완벽 시 속도 보너스

	[Header("Charge Visual Feedback")]
	[SerializeField] private ParticleSystem chargeParticles;     // 차징 이펙트 (추후 할당)

	private float _lastFlashTime;
	private float _lastSlashTime;
	private bool _isCharging;
	private float _chargeStartTime;
	private bool _perfectTimingNotified;

	public bool IsCharging => _isCharging;
	public float ChargeProgress => _isCharging ? (Time.time - _chargeStartTime) / chargeTimeForMax : 0f;

	public void StartCharge()
	{
		_isCharging = true;
		_chargeStartTime = Time.time;
		_perfectTimingNotified = false;

		// 차징 파티클 재생 (있으면)
		if (chargeParticles != null)
		{
			chargeParticles.Play();
		}
	}

	public void UpdateCharge(float duration)
	{
		if (!_isCharging) return;

		float progress = duration / chargeTimeForMax;

		// 완벽 타이밍 구간 진입 시 로그 출력
		float perfectStart = perfectTimingPoint - perfectTimingWindow;
		float perfectEnd = perfectTimingPoint + perfectTimingWindow;

		if (!_perfectTimingNotified && progress >= perfectStart && progress <= perfectEnd)
		{
			Debug.Log("<color=yellow>★ PERFECT TIMING WINDOW! Release now for bonus! ★</color>");
			_perfectTimingNotified = true;

			// 여기에 차징 이펙트 색상 변경 등 시각적 피드백 추가 가능
		}

		// 차징 파티클 크기/색상 업데이트 (있으면)
		if (chargeParticles != null)
		{
			var main = chargeParticles.main;
			main.startSize = Mathf.Lerp(0.5f, 2.0f, progress);
		}
	}

	// 1. 순간이동 발도 - Trail 사용
	public void PerformFlashCut(Vector3 targetPos)
	{
		_isCharging = false;
		StopChargeEffects();

		if (Time.time < _lastFlashTime + flashCooldown) return;
		_lastFlashTime = Time.time;

		Vector3 startPos = transform.position;
		Vector3 dir = (targetPos - startPos).normalized;
		dir.y = 0;
		float dist = flashDistance;

		// 장애물 체크
		if (Physics.Raycast(startPos, dir, out RaycastHit hit, flashDistance, obstacleLayer))
		{
			dist = Mathf.Max(0.5f, hit.distance - 0.5f);
		}

		Vector3 endPos = startPos + dir * dist;

		// Trail 활성화 (있으면)
		if (flashTrail != null)
		{
			flashTrail.Clear();
			flashTrail.emitting = true;
		}

		// 위치 이동
		var rb = GetComponent<Rigidbody>();
		if (rb != null)
		{
			rb.MovePosition(endPos);
		}
		else
		{
			transform.position = endPos;
		}

		// 적 타격 판정
		RaycastHit[] hits = Physics.SphereCastAll(startPos, flashWidth * 0.5f, dir, dist);
		int killCount = 0;
		foreach (var h in hits)
		{
			if (h.collider.CompareTag("Enemy"))
			{
				var destruction = h.collider.GetComponent<EnemyDestruction>();
				if (destruction != null)
				{
					destruction.ShatterAndDie();
					killCount++;
				}
			}
		}

		if (killCount > 0)
		{
			Debug.Log($"<color=red>Flash Cut: {killCount} enemies slain!</color>");
		}

		// Trail 비활성화 (약간의 딜레이 후)
		if (flashTrail != null)
		{
			StartCoroutine(DisableTrailAfterDelay(0.1f));
		}
	}

	private IEnumerator DisableTrailAfterDelay(float delay)
	{
		yield return new WaitForSeconds(delay);
		if (flashTrail != null)
		{
			flashTrail.emitting = false;
		}
	}

	// 2. 기본 베기 - 뱀파이어 서바이벌 스타일 (전방 일정 거리 공격)
	public void PerformSlash(Vector3 lookPos)
	{
		_isCharging = false;
		StopChargeEffects();

		if (Time.time < _lastSlashTime + slashCooldown) return;
		_lastSlashTime = Time.time;

		Vector3 dir = (lookPos - transform.position).normalized;
		dir.y = 0;

		// 전방으로 나가는 베기 투사체 생성
		if (slashProjectilePrefab != null)
		{
			Vector3 spawnPos = transform.position + dir * 1.0f + Vector3.up * 0.5f;
			GameObject slash = Instantiate(slashProjectilePrefab, spawnPos, Quaternion.LookRotation(dir));
			slash.SetActive(true);

			// SlashProjectile 컴포넌트로 이동 및 판정 처리
			var proj = slash.GetComponent<SlashProjectile>();
			if (proj != null)
			{
				proj.Initialize(dir, slashRange, slashWidth);
			}
			else
			{
				// 컴포넌트가 없으면 기본 동작
				Rigidbody slashRb = slash.GetComponent<Rigidbody>();
				if (slashRb != null)
				{
					slashRb.isKinematic = false;
					slashRb.linearVelocity = dir * 25f;
				}
				Destroy(slash, slashRange / 25f);
			}
		}
		else
		{
			// Prefab 없으면 기존 방식 (OverlapSphere)으로 폴백
			PerformSlashFallback(dir);
		}
	}

	// Prefab 없을 때 폴백 로직
	private void PerformSlashFallback(Vector3 dir)
	{
		// 전방 박스 영역 체크
		Vector3 boxCenter = transform.position + dir * (slashRange * 0.5f);
		Vector3 halfExtents = new Vector3(slashWidth * 0.5f, 1f, slashRange * 0.5f);
		Quaternion rotation = Quaternion.LookRotation(dir);

		Collider[] enemies = Physics.OverlapBox(boxCenter, halfExtents, rotation);
		int killCount = 0;
		foreach (var col in enemies)
		{
			if (col.CompareTag("Enemy"))
			{
				var destruction = col.GetComponent<EnemyDestruction>();
				if (destruction != null)
				{
					destruction.ShatterAndDie();
					killCount++;
				}
			}
		}

		if (killCount > 0)
		{
			Debug.Log($"<color=cyan>Slash: {killCount} enemies hit!</color>");
		}
	}

	// 3. 대형 검기 발사 - 완벽 타이밍 시스템 적용
	public void FireWave(Vector3 targetPos, float duration)
	{
		_isCharging = false;
		StopChargeEffects();

		float t = Mathf.Clamp01(duration / chargeTimeForMax);
		float size = Mathf.Lerp(minWaveSize, maxWaveSize, t);
		float speed = waveSpeed;

		// 완벽 타이밍 체크
		float perfectStart = perfectTimingPoint - perfectTimingWindow;
		float perfectEnd = perfectTimingPoint + perfectTimingWindow;
		bool isPerfect = t >= perfectStart && t <= perfectEnd;

		if (isPerfect)
		{
			size *= perfectBonusSize;
			speed *= perfectBonusSpeed;
			Debug.Log("<color=magenta>★★★ PERFECT RELEASE! Empowered Wave! ★★★</color>");
		}
		else if (t >= 1.0f)
		{
			Debug.Log("<color=orange>Max Charge Wave!</color>");
		}

		Vector3 dir = (targetPos - transform.position).normalized;
		dir.y = 0;

		if (wavePrefab != null)
		{
			// 검기 생성
			Vector3 spawnPos = transform.position + dir * 3f + Vector3.up * 0.5f;
			GameObject wave = Instantiate(wavePrefab, spawnPos, Quaternion.LookRotation(dir));
			wave.SetActive(true);
			wave.transform.localScale = Vector3.one * size;

			// 완벽 타이밍일 때 색상 변경 (MeshRenderer 또는 ParticleSystem)
			if (isPerfect)
			{
				var renderer = wave.GetComponent<Renderer>();
				if (renderer != null && renderer.material != null)
				{
					renderer.material.SetColor("_Color", new Color(1f, 0.5f, 1f) * 3f); // 보라색 강화
				}
			}

			Rigidbody rb = wave.GetComponent<Rigidbody>();
			if (rb != null)
			{
				rb.isKinematic = false;
				rb.linearVelocity = dir * speed;
			}

			// WaveProjectile 컴포넌트가 있으면 초기화
			var waveProj = wave.GetComponent<WaveProjectile>();
			if (waveProj != null)
			{
				waveProj.Initialize(isPerfect);
			}

			Destroy(wave, 4.0f);
		}

		// 반동
		var playerRb = GetComponent<Rigidbody>();
		if (playerRb != null)
		{
			float recoilForce = 25f * size;
			if (isPerfect) recoilForce *= 0.5f; // 완벽 타이밍 시 반동 감소
			playerRb.AddForce(-dir * recoilForce, ForceMode.Impulse);
		}
	}

	private void StopChargeEffects()
	{
		if (chargeParticles != null && chargeParticles.isPlaying)
		{
			chargeParticles.Stop();
		}
	}

	// Inspector에서 설정하기 위한 프로퍼티
	public TrailRenderer FlashTrail
	{
		get => flashTrail;
		set => flashTrail = value;
	}

	public GameObject SlashProjectilePrefab
	{
		get => slashProjectilePrefab;
		set => slashProjectilePrefab = value;
	}

	public ParticleSystem ChargeParticles
	{
		get => chargeParticles;
		set => chargeParticles = value;
	}
}

// 베기 투사체 컴포넌트 (전방으로 이동하며 적 처치)
public class SlashProjectile : MonoBehaviour
{
	private Vector3 _direction;
	private float _maxDistance;
	private float _width;
	private Vector3 _startPos;
	private float _speed = 30f;
	private HashSet<Collider> _hitEnemies = new HashSet<Collider>();

	public void Initialize(Vector3 dir, float range, float width)
	{
		_direction = dir;
		_maxDistance = range;
		_width = width;
		_startPos = transform.position;

		var rb = GetComponent<Rigidbody>();
		if (rb != null)
		{
			rb.isKinematic = false;
			rb.useGravity = false;
			rb.linearVelocity = _direction * _speed;
		}
	}

	private void Update()
	{
		// 최대 거리 도달 시 파괴
		float traveled = Vector3.Distance(_startPos, transform.position);
		if (traveled >= _maxDistance)
		{
			Destroy(gameObject);
		}
	}

	private void OnTriggerEnter(Collider other)
	{
		if (other.CompareTag("Enemy") && !_hitEnemies.Contains(other))
		{
			_hitEnemies.Add(other);
			var destruction = other.GetComponent<EnemyDestruction>();
			if (destruction != null)
			{
				destruction.ShatterAndDie();
			}
		}
	}
}

// 검기 투사체 컴포넌트 (적과 충돌 시 처리)
public class WaveProjectile : MonoBehaviour
{
	private bool _isPerfect;
	private HashSet<Collider> _hitEnemies = new HashSet<Collider>();

	public void Initialize(bool isPerfect)
	{
		_isPerfect = isPerfect;
	}

	private void OnTriggerEnter(Collider other)
	{
		if (other.CompareTag("Enemy") && !_hitEnemies.Contains(other))
		{
			_hitEnemies.Add(other);
			var destruction = other.GetComponent<EnemyDestruction>();
			if (destruction != null)
			{
				destruction.ShatterAndDie();
			}
		}
	}
}
