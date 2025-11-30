using UnityEngine;
using System.Collections;

public class HammerCombat : MonoBehaviour
{
	[Header("Combat Feel")]
	[SerializeField] private float hitStopDuration = 0.05f; // 타격 시 멈춤 시간
	[SerializeField] private float hitShakeIntensity = 1.5f; // 타격 시 카메라 흔들림
	[SerializeField] private float explosionForce = 1500f;   // 파편 날리는 힘

	private TopDownActionCamera _cam;
	private bool _isStopped;

	private void Start()
	{
		// 씬 내의 카메라 찾기 (태그 혹은 타입으로)
		var camObj = GameObject.FindGameObjectWithTag("MainCamera");
		if (camObj) _cam = camObj.GetComponent<TopDownActionCamera>();
		if (!_cam) _cam = FindFirstObjectByType<TopDownActionCamera>();
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (collision.gameObject.CompareTag("Enemy") || collision.gameObject.CompareTag("Obstacle"))
		{
			// 강한 충돌인지 확인 (살짝 스친 건 무시)
			if (collision.relativeVelocity.magnitude > 10f)
			{
				// 1. 타격감: 히트 스톱
				if (!_isStopped) StartCoroutine(HitStop());

				// 2. 타격감: 카메라 쉐이크
				if (_cam) _cam.Shake(hitShakeIntensity);

				// 3. 적 파괴 로직
				if (collision.gameObject.CompareTag("Enemy"))
				{
					ExplodeEnemy(collision.gameObject, collision.contacts[0].point);
				}
			}
		}
	}

	private IEnumerator HitStop()
	{
		_isStopped = true;
		Time.timeScale = 0.0f; // 시간 정지
		yield return new WaitForSecondsRealtime(hitStopDuration); // 리얼타임 대기
		Time.timeScale = 1.0f; // 시간 복구
		_isStopped = false;
	}

	private void ExplodeEnemy(GameObject enemy, Vector3 hitPoint)
	{
		Destroy(enemy);

		// 파편 생성 (간단한 시각 효과)
		int pieces = 8;
		for (int i = 0; i < pieces; i++)
		{
			GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
			piece.transform.position = enemy.transform.position + Random.insideUnitSphere * 0.5f;
			piece.transform.localScale = Vector3.one * 0.3f;
			piece.GetComponent<Renderer>().material.color = Color.red;

			Rigidbody rb = piece.AddComponent<Rigidbody>();
			rb.mass = 0.2f;
			rb.AddExplosionForce(explosionForce, hitPoint, 5f);
			Destroy(piece, 2f);
		}
	}
}