using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyRuntimeSpawner : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField] private GameObject enemyPrefab;
	[SerializeField] private Transform targetPlayer;
	[SerializeField] private int maxEnemyCount = 800;

	[Header("Boss Settings")]
	[SerializeField] private GameObject bossPrefab;
	[SerializeField] private int killsPerBoss = 100; // 100마리 처치마다 보스 생성
	[SerializeField] private float bossSpawnDistance = 60f; // 보스 생성 거리

	[Header("Spawn Range")]
	[SerializeField] private float minSpawnRadius = 30f;
	[SerializeField] private float maxSpawnRadius = 90f;
	[SerializeField] private float despawnDistance = 130f;

	private List<IEnemy> _activeEnemies = new List<IEnemy>();
	private List<IBossEnemy> _activeBosses = new List<IBossEnemy>();
	private WaitForSeconds _updateInterval = new WaitForSeconds(0.5f);
	private WaitForSeconds _spawnCheckInterval = new WaitForSeconds(0.5f); // 스폰 체크 주기
	private int _totalKillCount = 0; // 총 처치 수
	private int _killsSinceLastBoss = 0; // 마지막 보스 이후 처치 수

	public static EnemyRuntimeSpawner Instance { get; private set; }

	private void Awake()
	{
		Instance = this;
	}

	public void Initialize(GameObject prefab, Transform player, int count)
	{
		enemyPrefab = prefab;
		targetPlayer = player;
		maxEnemyCount = count;
	}

	private void Start()
	{
		StartCoroutine(Routine_UpdateEnemies());
		StartCoroutine(Routine_CheckAndRelocate());
		StartCoroutine(Routine_SpawnCheck()); // 주기적 스폰 체크
	}

	/// <summary>
	/// 적이 죽었을 때 호출
	/// </summary>
	public void NotifyEnemyDied(IEnemy enemy)
	{
		// 리스트에서 제거
		_activeEnemies.Remove(enemy);

		// 처치 카운트
		_totalKillCount++;
		_killsSinceLastBoss++;

		// 보스 생성 체크
		if (_killsSinceLastBoss >= killsPerBoss && bossPrefab != null)
		{
			SpawnBoss();
			_killsSinceLastBoss = 0;
		}
	}

	/// <summary>
	/// 보스가 죽었을 때 호출
	/// </summary>
	public void NotifyBossDied(IBossEnemy boss)
	{
		_activeBosses.Remove(boss);
		Debug.Log($"<color=cyan>[Spawner] 보스 처치! 총 처치 수: {_totalKillCount}, 다음 보스까지: {killsPerBoss - _killsSinceLastBoss}마리</color>");
	}

	/// <summary>
	/// 주기적으로 몬스터 수를 체크해서 부족하면 스폰
	/// </summary>
	private IEnumerator Routine_SpawnCheck()
	{
		while (true)
		{
			yield return _spawnCheckInterval;

			// null 정리 (파괴된 오브젝트)
			_activeEnemies.RemoveAll(e => e == null || e.Transform == null);

			// 부족한 몬스터 수 계산
			int currentCount = _activeEnemies.Count;
			int needed = maxEnemyCount - currentCount;

			// 부족한 만큼 스폰
			for (int i = 0; i < needed; i++)
			{
				SpawnNewEnemy();
			}
		}
	}

	private void SpawnNewEnemy()
	{
		if (!enemyPrefab || !targetPlayer) return;

		Vector3 pos = GetRandomPosAroundPlayer();
		GameObject obj = Instantiate(enemyPrefab, pos, Quaternion.identity);
		obj.SetActive(true);
		obj.transform.SetParent(transform);

		// IEnemy 초기화
		IEnemy enemy = obj.GetComponent<IEnemy>();
		if (enemy != null)
		{
			enemy.Initialize(targetPlayer);
			_activeEnemies.Add(enemy);
		}
		else
		{
			Debug.LogWarning($"[EnemyRuntimeSpawner] {enemyPrefab.name}에 IEnemy 컴포넌트가 없습니다!");
		}
	}

	private void SpawnBoss()
	{
		if (!bossPrefab || !targetPlayer) return;

		// 화면 밖 먼 거리에서 보스 생성
		Vector3 spawnPos = GetBossSpawnPosition();
		GameObject obj = Instantiate(bossPrefab, spawnPos, Quaternion.identity);
		obj.SetActive(true);
		obj.transform.SetParent(transform);

		// IBossEnemy 초기화
		IBossEnemy boss = obj.GetComponent<IBossEnemy>();
		if (boss != null)
		{
			boss.Initialize(targetPlayer);
			_activeBosses.Add(boss);

			Debug.Log($"<color=red>[Spawner] 보스 등장! 위치: {spawnPos}</color>");
		}
		else
		{
			Debug.LogWarning($"[EnemyRuntimeSpawner] {bossPrefab.name}에 IBossEnemy 컴포넌트가 없습니다!");
			Destroy(obj);
		}
	}

	private Vector3 GetBossSpawnPosition()
	{
		if (!targetPlayer) return Vector3.zero;

		// 플레이어로부터 먼 거리에 생성 (화면 밖)
		Vector2 randomCircle = Random.insideUnitCircle.normalized;
		Vector3 offset = new Vector3(randomCircle.x, 0, randomCircle.y) * bossSpawnDistance;

		return targetPlayer.position + offset;
	}

	private Vector3 GetRandomPosAroundPlayer()
	{
		if (!targetPlayer) return Vector3.zero;

		Vector2 randomCircle = Random.insideUnitCircle.normalized;
		float distance = Random.Range(minSpawnRadius, maxSpawnRadius);
		Vector3 offset = new Vector3(randomCircle.x, 0, randomCircle.y) * distance;

		return targetPlayer.position + offset;
	}

	private IEnumerator Routine_UpdateEnemies()
	{
		while (true)
		{
			// IEnemy AI 업데이트
			for (int i = _activeEnemies.Count - 1; i >= 0; i--)
			{
				IEnemy enemy = _activeEnemies[i];

				// null 체크
				if (enemy == null || enemy.Transform == null)
				{
					_activeEnemies.RemoveAt(i);
					continue;
				}

				if (enemy.IsAlive)
				{
					enemy.UpdateBehavior();
				}
			}

			// 보스 AI 업데이트
			for (int i = _activeBosses.Count - 1; i >= 0; i--)
			{
				IBossEnemy boss = _activeBosses[i];

				// null 체크
				if (boss == null || boss.Transform == null)
				{
					_activeBosses.RemoveAt(i);
					continue;
				}

				if (boss.IsAlive)
				{
					boss.UpdateBehavior();
				}
			}

			yield return new WaitForFixedUpdate();
		}
	}

	private IEnumerator Routine_CheckAndRelocate()
	{
		while (true)
		{
			if (targetPlayer)
			{
				Vector3 playerPos = targetPlayer.position;

				for (int i = _activeEnemies.Count - 1; i >= 0; i--)
				{
					IEnemy enemy = _activeEnemies[i];

					// null üũ (�ı��� ��)
					if (enemy == null || enemy.Transform == null)
					{
						_activeEnemies.RemoveAt(i);
						SpawnNewEnemy();
						continue;
					}

					// ���� ���� NotifyEnemyDied()�� ó����
					if (!enemy.IsAlive)
					{
						continue;
					}

					// ���ġ
					float distSqr = (enemy.Transform.position - playerPos).sqrMagnitude;
					if (distSqr > despawnDistance * despawnDistance)
					{
						enemy.Transform.position = GetRandomPosAroundPlayer();
						Rigidbody rb = enemy.Transform.GetComponent<Rigidbody>();
						if (rb) rb.linearVelocity = Vector3.zero;
					}
				}
			}

			yield return _updateInterval;
		}
	}
}