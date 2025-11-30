

using System.Collections.Generic;
using UnityEngine;

public class WaveProjectile : MonoBehaviour
{
	private bool _isPerfect;
	private HashSet<Collider> _hitEnemies = new HashSet<Collider>();
	private Vector3 _lastPosition;
	private float _checkRadius = 2f; // 검기 주변 체크 반경

	private void Awake()
	{
		// Rigidbody와 Collider 자동 설정
		var rb = GetComponent<Rigidbody>();
		if (rb == null)
		{
			rb = gameObject.AddComponent<Rigidbody>();
		}
		rb.useGravity = false;
		rb.isKinematic = false;
		rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // 빠른 물체를 위한 연속 충돌 감지

		// Collider가 없으면 추가
		var collider = GetComponent<Collider>();
		if (collider == null)
		{
			var sphereCollider = gameObject.AddComponent<SphereCollider>();
			sphereCollider.radius = 1.5f; // 검기 크기에 맞게 조정
			sphereCollider.isTrigger = true;
		}
		else
		{
			collider.isTrigger = true;
		}
	}

	public void Initialize(bool isPerfect)
	{
		_isPerfect = isPerfect;
		_lastPosition = transform.position;
	}

	private void FixedUpdate()
	{
		// 매 물리 프레임마다 주변 적 체크 (빠른 이동으로 인한 누락 방지)
		CheckForEnemiesInPath();
		_lastPosition = transform.position;
	}

	private void CheckForEnemiesInPath()
	{
		// 현재 위치에서 구형 범위로 적 감지
		Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, _checkRadius * transform.localScale.x);

		foreach (var col in nearbyColliders)
		{
			if (col.CompareTag("Enemy") && !_hitEnemies.Contains(col))
			{
				_hitEnemies.Add(col);
				var destruction = col.GetComponent<EnemyDestruction>();
				if (destruction != null)
				{
					destruction.ShatterAndDie();
					Debug.Log($"<color=yellow>Wave hit enemy: {col.name}</color>");
				}
			}
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
				Debug.Log($"<color=yellow>Wave hit enemy (Trigger): {other.name}</color>");
			}
		}
	}

	private void OnCollisionEnter(Collision collision)
	{
		// Trigger가 작동하지 않을 경우를 대비한 추가 충돌 처리
		if (collision.collider.CompareTag("Enemy") && !_hitEnemies.Contains(collision.collider))
		{
			_hitEnemies.Add(collision.collider);
			var destruction = collision.collider.GetComponent<EnemyDestruction>();
			if (destruction != null)
			{
				destruction.ShatterAndDie();
				Debug.Log($"<color=yellow>Wave hit enemy (Collision): {collision.collider.name}</color>");
			}
		}
	}
}