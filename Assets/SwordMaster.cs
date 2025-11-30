using UnityEngine;
using System.Collections;

public class SwordMaster : MonoBehaviour
{
	[Header("Flash Cut (Spacebar)")]
	[SerializeField] private float flashDistance = 7.0f;
	[SerializeField] private float flashWidth = 2.5f;
	[SerializeField] private float flashCooldown = 0.8f;
	[SerializeField] private LayerMask obstacleLayer;
	[SerializeField] private GameObject flashEffectPrefab;

	[Header("Basic Slash (Left Click Tap)")]
	[SerializeField] private float slashRange = 3.5f;
	[SerializeField] private float slashAngle = 140f;
	[SerializeField] private float slashCooldown = 0.2f;
	[SerializeField] private GameObject slashEffectPrefab;

	[Header("Heavy Wave (Left Click Hold)")]
	[SerializeField] private float minWaveSize = 2.0f;
	[SerializeField] private float maxWaveSize = 8.0f;
	[SerializeField] private float chargeTimeForMax = 1.2f;
	[SerializeField] private float waveSpeed = 40f;
	[SerializeField] private GameObject wavePrefab;

	private float _lastFlashTime;
	private float _lastSlashTime;
	private bool _isCharging;

	public void StartCharge() { _isCharging = true; }
	public void UpdateCharge(float duration) { if (!_isCharging) return; }

	// 1. 순간이동 발도
	public void PerformFlashCut(Vector3 targetPos)
	{
		_isCharging = false;
		if (Time.time < _lastFlashTime + flashCooldown) return;
		_lastFlashTime = Time.time;

		// ... (이동 및 적 처치 로직 동일) ...
		Vector3 startPos = transform.position;
		Vector3 dir = (targetPos - startPos).normalized;
		float dist = flashDistance;
		if (Physics.Raycast(startPos, dir, out RaycastHit hit, flashDistance, obstacleLayer)) dist = hit.distance - 0.5f;
		Vector3 endPos = startPos + dir * dist;

		var rb = GetComponent<Rigidbody>();
		if (rb) rb.MovePosition(endPos); else transform.position = endPos;

		RaycastHit[] hits = Physics.SphereCastAll(startPos, flashWidth * 0.5f, dir, dist);
		foreach (var h in hits) { if (h.collider.CompareTag("Enemy")) h.collider.GetComponent<EnemyDestruction>()?.ShatterAndDie(); }

		// [이펙트 생성 및 활성화]
		if (flashEffectPrefab)
		{
			GameObject vfx = Instantiate(flashEffectPrefab, (startPos + endPos) / 2, Quaternion.LookRotation(dir));
			vfx.SetActive(true); // <--- [수정] 반드시 켜줘야 함!
			vfx.transform.localScale = new Vector3(flashWidth, 1, dist);
			Destroy(vfx, 0.5f);
		}
	}

	// 2. 기본 베기
	public void PerformSlash(Vector3 lookPos)
	{
		_isCharging = false;
		if (Time.time < _lastSlashTime + slashCooldown) return;
		_lastSlashTime = Time.time;

		Vector3 dir = (lookPos - transform.position).normalized;

		// ... (적 처치 로직 동일) ...
		Collider[] enemies = Physics.OverlapSphere(transform.position, slashRange);
		foreach (var col in enemies)
		{
			if (col.CompareTag("Enemy"))
			{
				Vector3 toEnemy = (col.transform.position - transform.position).normalized;
				if (Vector3.Angle(dir, toEnemy) < slashAngle * 0.5f) col.GetComponent<EnemyDestruction>()?.ShatterAndDie();
			}
		}

		// [이펙트 생성 및 활성화]
		if (slashEffectPrefab)
		{
			var vfx = Instantiate(slashEffectPrefab, transform.position + dir * 1.5f, Quaternion.LookRotation(dir));
			vfx.SetActive(true); // <--- [수정] 이거 없으면 안 보임!
			Destroy(vfx, 0.3f);
		}
	}

	// 3. 대형 검기 발사
	public void FireWave(Vector3 targetPos, float duration)
	{
		_isCharging = false;
		float t = Mathf.Clamp01(duration / chargeTimeForMax);
		float size = Mathf.Lerp(minWaveSize, maxWaveSize, t);

		Vector3 dir = (targetPos - transform.position).normalized;
		dir.y = 0;

		if (wavePrefab)
		{
			// 검기 생성
			GameObject wave = Instantiate(wavePrefab, transform.position + dir * 3f, Quaternion.LookRotation(dir));

			// [수정] ★★★ 여기서 켜줘야 합니다! ★★★
			wave.SetActive(true);

			wave.transform.localScale = Vector3.one * size;

			Rigidbody rb = wave.GetComponent<Rigidbody>();
			if (rb)
			{
				rb.isKinematic = false; // 물리 켜기 (템플릿은 꺼져있었음)
				rb.linearVelocity = dir * waveSpeed;
			}

			Destroy(wave, 4.0f);
		}

		var playerRb = GetComponent<Rigidbody>();
		if (playerRb) playerRb.AddForce(-dir * 25f * size, ForceMode.Impulse);
	}
}