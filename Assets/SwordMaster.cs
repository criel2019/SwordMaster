using UnityEngine;
using System.Collections.Generic;

public class SwordMaster : MonoBehaviour
{
	[Header("Basic Slash (Left Click Tap)")]
	[SerializeField] private float slashRange = 5.0f;
	[SerializeField] private float slashWidth = 3.0f;
	[SerializeField] private float slashCooldown = 0.2f;
	[SerializeField] private GameObject slashProjectilePrefab;

	[Header("Heavy Wave (Left Click Hold)")]
	[SerializeField] private float minWaveSize = 2.0f;
	[SerializeField] private float maxWaveSize = 8.0f;
	[SerializeField] private float chargeTimeForMax = 1.2f;
	[SerializeField] private float waveSpeed = 40f;
	[SerializeField] private GameObject wavePrefab;

	[Header("Perfect Timing System")]
	[SerializeField] private float perfectTimingWindow = 0.15f;
	[SerializeField] private float perfectTimingPoint = 0.8f;
	[SerializeField] private float perfectBonusSize = 1.5f;
	[SerializeField] private float perfectBonusSpeed = 1.3f;

	private float _lastSlashTime;
	private bool _isCharging;
	private float _chargeStartTime;
	private bool _perfectTimingNotified;

	private Rigidbody _rigidbody;

	public bool IsCharging => _isCharging;
	public float ChargeProgress => _isCharging ? (Time.time - _chargeStartTime) / chargeTimeForMax : 0f;

	private void Awake()
	{
		_rigidbody = GetComponent<Rigidbody>();
	}

	public void StartCharge()
	{
		_isCharging = true;
		_chargeStartTime = Time.time;
		_perfectTimingNotified = false;
	}

	public void UpdateCharge(float duration)
	{
		if (!_isCharging) return;

		float progress = duration / chargeTimeForMax;
		float perfectStart = perfectTimingPoint - perfectTimingWindow;
		float perfectEnd = perfectTimingPoint + perfectTimingWindow;

		if (!_perfectTimingNotified && progress >= perfectStart && progress <= perfectEnd)
		{
			Debug.Log("<color=yellow>★ PERFECT TIMING WINDOW! ★</color>");
			_perfectTimingNotified = true;
		}
	}

	public void CancelCharge()
	{
		_isCharging = false;
	}

	public void PerformSlash(Vector3 lookPos)
	{
		_isCharging = false;

		if (Time.time < _lastSlashTime + slashCooldown) return;
		_lastSlashTime = Time.time;

		Vector3 dir = (lookPos - transform.position).normalized;
		dir.y = 0;

		if (slashProjectilePrefab != null)
		{
			Vector3 spawnPos = transform.position + dir * 1.0f + Vector3.up * 0.5f;
			GameObject slash = Instantiate(slashProjectilePrefab, spawnPos, Quaternion.LookRotation(dir));
			slash.SetActive(true);

			var proj = slash.GetComponent<SlashProjectile>();
			if (proj != null)
			{
				proj.Initialize(dir, slashRange, slashWidth);
			}
			else
			{
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
			PerformSlashFallback(dir);
		}
	}

	private void PerformSlashFallback(Vector3 dir)
	{
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
			Debug.Log($"<color=cyan>Slash: {killCount} hit!</color>");
		}
	}

	public void FireWave(Vector3 targetPos, float duration)
	{
		_isCharging = false;

		float t = Mathf.Clamp01(duration / chargeTimeForMax);
		float size = Mathf.Lerp(minWaveSize, maxWaveSize, t);
		float speed = waveSpeed;

		float perfectStart = perfectTimingPoint - perfectTimingWindow;
		float perfectEnd = perfectTimingPoint + perfectTimingWindow;
		bool isPerfect = t >= perfectStart && t <= perfectEnd;

		if (isPerfect)
		{
			size *= perfectBonusSize;
			speed *= perfectBonusSpeed;
			Debug.Log("<color=magenta>★★★ PERFECT WAVE! ★★★</color>");
		}

		Vector3 dir = (targetPos - transform.position).normalized;
		dir.y = 0;

		if (wavePrefab != null)
		{
			Vector3 spawnPos = transform.position + dir * 3f + Vector3.up * 0.5f;
			GameObject wave = Instantiate(wavePrefab, spawnPos, Quaternion.LookRotation(dir));
			wave.SetActive(true);
			wave.transform.localScale = Vector3.one * size;

			if (isPerfect)
			{
				var renderer = wave.GetComponent<Renderer>();
				if (renderer != null && renderer.material != null)
				{
					renderer.material.SetColor("_Color", new Color(1f, 0.5f, 1f) * 3f);
				}
			}

			Rigidbody rb = wave.GetComponent<Rigidbody>();
			if (rb != null)
			{
				rb.isKinematic = false;
				rb.linearVelocity = dir * speed;
			}

			var waveProj = wave.GetComponent<WaveProjectile>();
			if (waveProj != null)
			{
				waveProj.Initialize(isPerfect);
			}

			Destroy(wave, 4.0f);
		}

		if (_rigidbody != null)
		{
			float recoilForce = 25f * size;
			if (isPerfect) recoilForce *= 0.5f;
			_rigidbody.AddForce(-dir * recoilForce, ForceMode.Impulse);
		}
	}

	public GameObject SlashProjectilePrefab
	{
		get => slashProjectilePrefab;
		set => slashProjectilePrefab = value;
	}
}

public class SlashProjectile : MonoBehaviour
{
	private Vector3 _direction;
	private float _maxDistance;
	private Vector3 _startPos;
	private float _speed = 30f;
	private HashSet<Collider> _hitEnemies = new HashSet<Collider>();

	public void Initialize(Vector3 dir, float range, float width)
	{
		_direction = dir;
		_maxDistance = range;
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