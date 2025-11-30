using UnityEngine;
using System.Collections.Generic;

public class EnemyRuntimeSpawner : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField] private GameObject enemyPrefab;
	[SerializeField] private Transform targetPlayer;
	[SerializeField] private int maxEnemyCount = 800;

	[Header("Spawn Range")]
	[SerializeField] private float minSpawnRadius = 30f;
	[SerializeField] private float maxSpawnRadius = 90f;
	[SerializeField] private float despawnDistance = 130f;

	private List<Transform> _activeEnemies = new List<Transform>();
	private WaitForSeconds _updateInterval = new WaitForSeconds(0.5f);

	public void Initialize(GameObject prefab, Transform player, int count)
	{
		enemyPrefab = prefab;
		targetPlayer = player;
		maxEnemyCount = count;
	}

	private void Start()
	{
		for (int i = 0; i < maxEnemyCount; i++) SpawnNewEnemy();
		StartCoroutine(Routine_CheckAndRelocate());
	}

	private void SpawnNewEnemy()
	{
		if (!enemyPrefab || !targetPlayer) return;
		Vector3 pos = GetRandomPosAroundPlayer();

		GameObject obj = Instantiate(enemyPrefab, pos, Quaternion.identity);

		// [수정] 템플릿 프리팹이 꺼져있으므로 반드시 켜줘야 함
		obj.SetActive(true);

		obj.transform.SetParent(transform);
		_activeEnemies.Add(obj.transform);
	}

	private Vector3 GetRandomPosAroundPlayer()
	{
		if (!targetPlayer) return Vector3.zero;
		Vector2 randomCircle = Random.insideUnitCircle.normalized;
		float distance = Random.Range(minSpawnRadius, maxSpawnRadius);
		Vector3 offset = new Vector3(randomCircle.x, 0, randomCircle.y) * distance;
		return targetPlayer.position + offset;
	}

	private System.Collections.IEnumerator Routine_CheckAndRelocate()
	{
		while (true)
		{
			if (targetPlayer)
			{
				Vector3 playerPos = targetPlayer.position;
				for (int i = 0; i < _activeEnemies.Count; i++)
				{
					Transform enemy = _activeEnemies[i];
					if (enemy == null) continue;

					float distSqr = (enemy.position - playerPos).sqrMagnitude;
					if (distSqr > despawnDistance * despawnDistance)
					{
						enemy.position = GetRandomPosAroundPlayer();
						Rigidbody rb = enemy.GetComponent<Rigidbody>();
						if (rb) rb.linearVelocity = Vector3.zero;
					}
				}
			}
			yield return _updateInterval;
		}
	}
}