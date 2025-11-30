using UnityEngine;

public class TopDownActionCamera : MonoBehaviour
{
	[Header("Target Settings")]
	[SerializeField] private Transform target;
	[SerializeField] private Rigidbody weaponRb; // 무기 속도 감지용
	[SerializeField] private Vector3 offset = new Vector3(0, 60, -10);
	[SerializeField] private float smoothTime = 0.25f;

	[Header("Dynamic Zoom (Velocity Based)")]
	[Tooltip("기본 FOV (평소)")]
	[SerializeField] private float minFOV = 50f;
	[Tooltip("최대 속도일 때 FOV (줌 아웃)")]
	[SerializeField] private float maxFOV = 80f;
	[Tooltip("줌 아웃이 시작되는 무기 속도")]
	[SerializeField] private float zoomThresholdSpeed = 10f;
	[Tooltip("최대 줌 아웃이 되는 무기 속도")]
	[SerializeField] private float maxZoomSpeed = 100f;

	[Header("Shake Settings")]
	[SerializeField] private float shakeRecovery = 10f;

	private Vector3 _currentVelocity;
	private Camera _cam;
	private Vector3 _shakeOffset;

	private void Awake()
	{
		_cam = GetComponent<Camera>();
		_cam.fieldOfView = minFOV;
		transform.rotation = Quaternion.Euler(80, 0, 0);
	}

	public void Shake(float intensity)
	{
		_shakeOffset = Random.insideUnitSphere * intensity;
		_shakeOffset.y = 0;
	}

	private void LateUpdate()
	{
		if (!target) return;

		// 1. 위치 이동
		Vector3 finalPos = target.position + offset;
		if (_shakeOffset.sqrMagnitude > 0.01f)
		{
			finalPos += _shakeOffset;
			_shakeOffset = Vector3.Lerp(_shakeOffset, Vector3.zero, Time.deltaTime * shakeRecovery);
		}
		transform.position = Vector3.SmoothDamp(transform.position, finalPos, ref _currentVelocity, smoothTime);

		// 2. 다이내믹 줌 (속도 기반)
		if (weaponRb)
		{
			float currentSpeed = weaponRb.linearVelocity.magnitude;

			// 속도가 빠를수록 FOV를 높여서 넓게 본다 (Zoom Out)
			float t = Mathf.InverseLerp(zoomThresholdSpeed, maxZoomSpeed, currentSpeed);
			float targetFOV = Mathf.Lerp(minFOV, maxFOV, t);

			_cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFOV, Time.deltaTime * 2f);
		}
	}

	public void SetTarget(Transform t, Rigidbody w)
	{
		target = t;
		weaponRb = w;
	}
}