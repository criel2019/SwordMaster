using UnityEngine;

// [구현 예시] 기존의 '공' 기능을 담당하는 신수 모듈
public class OrbBeast : DivineBeastBase
{
	[Header("Passive: Orbit")]
	[SerializeField] private float orbitSpeed = 200f;
	[SerializeField] private float orbitRadius = 4.0f;
	[SerializeField] private float damageRadius = 0.8f; // 공 자체 크기

	[Header("Active: Focus & Fire")]
	[SerializeField] private float focusShrinkSpeed = 15f;
	[SerializeField] private float minRadius = 0.5f;
	[SerializeField] private float fireSpeed = 60f;
	[SerializeField] private float returnDelay = 1.5f;

	// 내부 상태 변수
	private float _currentAngle;
	private float _currentRadius;
	private bool _isFocusing; // Q 누르는 중?
	private bool _isFired;    // 발사되어 날아가는 중?
	private Rigidbody _rb;

	private void Start()
	{
		_rb = GetComponent<Rigidbody>();
		if (!_rb) _rb = gameObject.AddComponent<Rigidbody>();

		_rb.useGravity = false;
		_rb.isKinematic = true; // 평소엔 물리 끄고 계산으로 이동

		_currentRadius = orbitRadius;

		// Owner가 할당 안 됐으면 자동 찾기
		if (owner == null)
		{
			var p = GameObject.FindGameObjectWithTag("Player");
			if (p) Initialize(p.transform);
		}
	}

	// 1. 패시브: 플레이어 주변 공전 (물리X, 수학적 이동)
	protected override void PassiveUpdate()
	{
		if (_isFired) return; // 발사 중엔 공전 중단

		// (A) 회전 각도 계산
		float speed = _isFocusing ? orbitSpeed * 4f : orbitSpeed; // 모을 때 더 빨리 돔 (연출)
		_currentAngle += speed * Time.deltaTime;

		// (B) 반경 계산 (Q 누르면 줄어듦)
		float targetR = _isFocusing ? minRadius : orbitRadius;
		_currentRadius = Mathf.Lerp(_currentRadius, targetR, Time.deltaTime * focusShrinkSpeed);

		// (C) 위치 적용
		float rad = _currentAngle * Mathf.Deg2Rad;
		Vector3 offset = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * _currentRadius;
		transform.position = owner.position + offset;

		// (D) 충돌 판정 (회전 톱날처럼)
		CheckCollision();
	}

	// 2. 입력 처리 (Q 키)
	public override void OnInputDown()
	{
		if (_isFired) return;
		_isFocusing = true;
	}

	public override void OnInputHold(Vector3 target)
	{
		// 필요 시 궤적 표시
	}

	public override void OnInputUp(Vector3 target)
	{
		if (_isFired) return;
		_isFocusing = false;

		// 타이밍 판정 로직 (예: 반경이 최소치 근처일 때 떼면 발사)
		// 여기선 단순화하여 무조건 발사 후 복귀로 구현
		Fire(target);
	}

	private void Fire(Vector3 targetPos)
	{
		_isFired = true;
		_rb.isKinematic = false; // 물리 켜기

		Vector3 dir = (targetPos - transform.position).normalized;
		dir.y = 0;

		_rb.linearVelocity = dir * fireSpeed;

		// 일정 시간 후 복귀
		Invoke(nameof(ReturnToOrbit), returnDelay);
	}

	private void ReturnToOrbit()
	{
		_isFired = false;
		_rb.isKinematic = true;
		_rb.linearVelocity = Vector3.zero;
		_currentRadius = orbitRadius; // 즉시 복구하거나 Lerp로 부드럽게
	}

	private void CheckCollision()
	{
		// 공 주변의 적 감지 및 타격
		Collider[] hits = Physics.OverlapSphere(transform.position, damageRadius);
		foreach (var h in hits)
		{
			if (h.CompareTag("Enemy"))
			{
				var enemy = h.GetComponent<EnemyDestruction>();
				if (enemy) enemy.ShatterAndDie();
			}
		}
	}
}