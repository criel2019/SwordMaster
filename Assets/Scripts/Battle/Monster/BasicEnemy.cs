using UnityEngine;

public class BasicEnemy : EnemyBase
{
	public override void UpdateBehavior()
	{
		if (!_isAlive || !_player || _rb == null) return;

		// 단순 추적
		Vector3 dirToPlayer = (_player.position - transform.position);
		dirToPlayer.y = 0;
		float distance = dirToPlayer.magnitude;

		if (distance > stopDistance)
		{
			Vector3 moveDir = dirToPlayer.normalized;
			_rb.linearVelocity = moveDir * moveSpeed;

			if (moveDir != Vector3.zero)
				transform.rotation = Quaternion.LookRotation(moveDir);
		}
		else
		{
			_rb.linearVelocity = Vector3.zero;
		}
	}
}