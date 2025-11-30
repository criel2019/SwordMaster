using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class ArcadePhysicsMover : MonoBehaviour
{
	[Header("Inertia Movement")]
	[SerializeField] private float moveSpeed = 250000f;
	[SerializeField] private float maxVelocity = 80f;
	[SerializeField] private float drag = 5f;

	[Header("Dash Settings")]
	[SerializeField] private float dashSpeed = 350f;
	[SerializeField] private float dashDuration = 0.15f;
	[SerializeField] private float dashCooldown = 0.5f;
	[SerializeField] private TrailRenderer dashTrail;

	[Header("Rotation")]
	[SerializeField] private float lookSpeed = 25f;

	private Rigidbody _rb;
	private Vector3 _inputDirection;
	private Vector3 _lookPosition;
	private float _lastDashTime;
	private bool _isDashing;

	public bool IsDashing => _isDashing;

	private void Awake()
	{
		_rb = GetComponent<Rigidbody>();
		_rb.linearDamping = drag;
		_rb.angularDamping = 10f;
		_rb.interpolation = RigidbodyInterpolation.Interpolate;
		_rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;

		if (dashTrail) dashTrail.emitting = false;
		else dashTrail = GetComponent<TrailRenderer>();
	}

	public void SetMoveInput(Vector2 direction)
	{
		_inputDirection = new Vector3(direction.x, 0, direction.y).normalized;
	}

	public void SetLookPosition(Vector3 worldPoint)
	{
		_lookPosition = new Vector3(worldPoint.x, transform.position.y, worldPoint.z);
	}

	public void Dash()
	{
		if (Time.time < _lastDashTime + dashCooldown || _isDashing) return;
		StartCoroutine(Routine_PerformDash());
	}

	private IEnumerator Routine_PerformDash()
	{
		_isDashing = true;
		_lastDashTime = Time.time;

		Vector3 dashDir = _inputDirection.sqrMagnitude > 0 ? _inputDirection : transform.forward;
		float originalDrag = _rb.linearDamping;
		_rb.linearDamping = 0f;
		_rb.linearVelocity = dashDir * dashSpeed;

		if (dashTrail) dashTrail.emitting = true;

		yield return new WaitForSeconds(dashDuration);

		_rb.linearDamping = originalDrag;
		if (_rb.linearVelocity.magnitude > maxVelocity)
		{
			_rb.linearVelocity = _rb.linearVelocity.normalized * maxVelocity;
		}

		if (dashTrail) dashTrail.emitting = false;
		_isDashing = false;
	}

	private void FixedUpdate()
	{
		if (_isDashing) return;

		if (_inputDirection.sqrMagnitude > 0.01f)
		{
			_rb.AddForce(_inputDirection * moveSpeed, ForceMode.Force);
			if (_rb.linearVelocity.magnitude > maxVelocity)
			{
				_rb.linearVelocity = _rb.linearVelocity.normalized * maxVelocity;
			}
		}

		Vector3 dir = _lookPosition - _rb.position;
		if (dir.sqrMagnitude > 0.1f)
		{
			Quaternion targetRot = Quaternion.LookRotation(dir);
			_rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, lookSpeed * Time.fixedDeltaTime));
		}
	}
}