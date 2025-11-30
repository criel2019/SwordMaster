using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FlashCutChainSkill : MonoBehaviour, ISkill
{
	[Header("Skill Info")]
	[SerializeField] private string skillId = "flash_cut_chain";
	[SerializeField] private string skillName = "발검 연격";
	[SerializeField] private int staminaCost = 2;
	[SerializeField] private float cooldown = 3f;
	[SerializeField] private float cooldownReductionPerMiss = 0.5f;

	[Header("Combo")]
	[SerializeField] private string[] comboFrom = null;
	[SerializeField] private float comboAllowTime = 0f;

	[Header("Chain Settings")]
	[SerializeField] private int maxChainCount = 6;
	[SerializeField] private float searchRadius = 15f;
	[SerializeField] private float flashDuration = 0.05f;
	[SerializeField] private float flashWidth = 2.5f;

	[Header("Trail Settings")]
	[SerializeField] private Color trailStartColor = new Color(1f, 0.4f, 0.4f, 1f);
	[SerializeField] private Color trailEndColor = new Color(1f, 0.2f, 0.2f, 0f);
	[SerializeField] private float trailWidth = 1.2f;
	[SerializeField] private float trailTime = 0.2f;

	[Header("Debug")]
	[SerializeField] private bool showRangeGizmo = true;

	private float _lastUseTime = -999f;
	private float _currentCooldown;
	private bool _isExecuting;

	private TrailRenderer _flashTrail;
	private Rigidbody _rigidbody;
	private ArcadePhysicsMover _mover;

	// ISkill 구현
	public string SkillId => skillId;
	public string SkillName => skillName;
	public int StaminaCost => staminaCost;
	public float Cooldown => _currentCooldown;
	public bool IsReady => !_isExecuting && Time.time >= _lastUseTime + _currentCooldown;
	public bool IsExecuting => _isExecuting;
	public bool IsChargeSkill => false;
	public string[] ComboFrom => comboFrom;
	public float ComboAllowTime => comboAllowTime;

	private void Awake()
	{
		_rigidbody = GetComponentInParent<Rigidbody>();
		_mover = GetComponentInParent<ArcadePhysicsMover>();
		_currentCooldown = cooldown;
		CreateFlashTrail();
	}

	private void CreateFlashTrail()
	{
		GameObject trailObj = new GameObject("ChainFlashTrail");
		trailObj.transform.SetParent(transform.parent);
		trailObj.transform.localPosition = Vector3.zero;

		_flashTrail = trailObj.AddComponent<TrailRenderer>();
		_flashTrail.time = trailTime;
		_flashTrail.startWidth = trailWidth;
		_flashTrail.endWidth = trailWidth * 0.3f;
		_flashTrail.minVertexDistance = 0.1f;
		_flashTrail.autodestruct = false;
		_flashTrail.emitting = false;
		_flashTrail.numCapVertices = 4;
		_flashTrail.numCornerVertices = 4;
		_flashTrail.startColor = trailStartColor;
		_flashTrail.endColor = trailEndColor;

		Material trailMat = new Material(Shader.Find("Sprites/Default"));
		_flashTrail.material = trailMat;
		_flashTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		_flashTrail.receiveShadows = false;
	}

	public void Execute(Vector3 targetPos)
	{
		if (_isExecuting) return;

		List<Transform> enemies = FindEnemiesInRange();

		if (enemies.Count == 0) return;

		int actualChainCount = Mathf.Min(enemies.Count, maxChainCount);
		int missedCount = maxChainCount - actualChainCount;

		_currentCooldown = cooldown - (missedCount * cooldownReductionPerMiss);
		_currentCooldown = Mathf.Max(0f, _currentCooldown);

		_lastUseTime = Time.time;
		StartCoroutine(ChainFlashRoutine(enemies, actualChainCount));
	}

	// 차징 스킬 아님
	public void StartCharge() { }
	public void UpdateCharge(float duration) { }
	public void ReleaseCharge(Vector3 targetPos, float duration) { }
	public void Cancel() { _isExecuting = false; }

	private List<Transform> FindEnemiesInRange()
	{
		List<Transform> enemies = new List<Transform>();
		Collider[] colliders = Physics.OverlapSphere(transform.parent.position, searchRadius);

		foreach (var col in colliders)
		{
			if (col.CompareTag("Enemy"))
			{
				var destruction = col.GetComponent<EnemyDestruction>();
				if (destruction != null)
				{
					enemies.Add(col.transform);
				}
			}
		}

		return enemies;
	}

	private IEnumerator ChainFlashRoutine(List<Transform> enemies, int chainCount)
	{
		_isExecuting = true;
		Transform playerTransform = transform.parent;

		if (_mover != null)
		{
			_mover.Freeze();
		}

		if (_rigidbody != null)
		{
			_rigidbody.linearVelocity = Vector3.zero;
			_rigidbody.angularVelocity = Vector3.zero;
			_rigidbody.isKinematic = true;
		}

		_flashTrail.Clear();
		_flashTrail.emitting = true;

		for (int i = 0; i < chainCount; i++)
		{
			if (i >= enemies.Count) break;

			Transform target = enemies[i];
			if (target == null || target.gameObject == null) continue;

			// 이미 파괴된 오브젝트 체크
			EnemyDestruction destruction = target.GetComponent<EnemyDestruction>();
			if (destruction == null) continue;

			Vector3 startPos = playerTransform.position;
			Vector3 endPos = target.position;
			Vector3 dir = (endPos - startPos).normalized;
			float dist = Vector3.Distance(startPos, endPos);

			// 이동 보간
			float elapsed = 0f;
			while (elapsed < flashDuration)
			{
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / flashDuration);
				float easedT = 1f - Mathf.Pow(1f - t, 3f);

				playerTransform.position = Vector3.Lerp(startPos, endPos, easedT);
				yield return null;
			}

			playerTransform.position = endPos;

			// 타격 (다시 한번 null 체크)
			if (target != null && destruction != null)
			{
				destruction.ShatterAndDie();
			}
		}

		_flashTrail.emitting = false;

		if (_rigidbody != null)
		{
			_rigidbody.isKinematic = false;
			_rigidbody.linearVelocity = Vector3.zero;
			_rigidbody.angularVelocity = Vector3.zero;
		}

		if (_mover != null)
		{
			_mover.Unfreeze();
		}

		_isExecuting = false;
		_currentCooldown = cooldown;
	}

	private void OnDrawGizmos()
	{
		if (!showRangeGizmo) return;

		Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
		Vector3 center = transform.parent != null ? transform.parent.position : transform.position;
		Gizmos.DrawWireSphere(center, searchRadius);

		Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.1f);
		Gizmos.DrawSphere(center, searchRadius);
	}
}