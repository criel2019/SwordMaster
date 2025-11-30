using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;

public class ProjectASceneSetup : Editor
{
	private const float MAP_SIZE = 6000f;
	private const int ENEMY_COUNT = 50;
	private const int OBSTACLE_COUNT = 100;

	[MenuItem("Tools/Project A/Setup Combat Scene")]
	public static void SetupScene()
	{
		ClearEnvironment();
		GameObject root = new GameObject("[ProjectA_CombatEnv]");
		Undo.RegisterCreatedObjectUndo(root, "Create Environment");

		Shader shader = GetProperShader();

		// 1. 재질 설정
		PhysicsMaterial floorMat = new PhysicsMaterial("ZeroFriction") { dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };
		PhysicsMaterial wallMat = new PhysicsMaterial("Wall") { bounciness = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };

		// 2. 환경 생성
		SetupEnvironment(root, shader, floorMat);
		SetupObstacles(root, shader, wallMat);

		// 3. 플레이어 및 전투 시스템 (고퀄리티 에셋 생성)
		GameObject player = SetupPlayerAndCombatSystem(root, shader, floorMat);

		// 4. 적 및 카메라
		SetupEnemySpawner(root, shader, player);
		SetupCamera(root, player);

		Debug.Log($"⚔️ [Project A] Final Scene Setup Complete. Procedural Mesh Generation Finished.");
	}

	// --- [1] 환경 설정 (기존 유지) ---
	private static void SetupEnvironment(GameObject root, Shader shader, PhysicsMaterial physMat)
	{
		GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
		floor.name = "Floor";
		floor.transform.SetParent(root.transform);
		floor.transform.localScale = new Vector3(MAP_SIZE / 10, 1, MAP_SIZE / 10);

		Material mat = new Material(shader);
		mat.color = new Color(0.08f, 0.08f, 0.1f); // 더 어둡게
		if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
		mat.mainTexture = GenerateGridTexture();
		mat.mainTextureScale = new Vector2(MAP_SIZE / 10, MAP_SIZE / 10);

		floor.GetComponent<Renderer>().material = mat;
		floor.GetComponent<Collider>().material = physMat;
	}

	private static void SetupObstacles(GameObject root, Shader shader, PhysicsMaterial physMat)
	{
		GameObject obstaclesRoot = new GameObject("Obstacles");
		obstaclesRoot.transform.SetParent(root.transform);
		Material obstacleMat = new Material(shader) { color = new Color(0.2f, 0.2f, 0.3f) };

		for (int i = 0; i < OBSTACLE_COUNT; i++)
		{
			Vector3 pos = GetRandomPosOnMap(150f);
			GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
			pillar.transform.SetParent(obstaclesRoot.transform);

			float width = Random.Range(5f, 20f);
			float depth = Random.Range(5f, 20f);
			float height = 40f; // 높게

			pillar.transform.position = new Vector3(pos.x, height / 2, pos.z);
			pillar.transform.localScale = new Vector3(width, height, depth);
			pillar.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);

			pillar.GetComponent<Renderer>().material = obstacleMat;
			pillar.GetComponent<Collider>().material = physMat;
			pillar.tag = "Obstacle";

			var rb = pillar.AddComponent<Rigidbody>();
			rb.isKinematic = true;
		}
	}

	// --- [2] 플레이어 및 전투 시스템 (핵심 수정) ---
	private static GameObject SetupPlayerAndCombatSystem(GameObject root, Shader shader, PhysicsMaterial playerMat)
	{
		// A. 플레이어
		GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
		player.name = "Player_TheRiftWalker";
		player.transform.SetParent(root.transform);
		player.transform.position = new Vector3(0, 2f, 0);
		player.tag = "Player";

		Rigidbody pRb = player.AddComponent<Rigidbody>();
		pRb.mass = 80f; pRb.linearDamping = 5f; pRb.angularDamping = 10f;
		pRb.interpolation = RigidbodyInterpolation.Interpolate;
		pRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
		pRb.collisionDetectionMode = CollisionDetectionMode.Continuous;

		player.GetComponent<Renderer>().material = new Material(shader) { color = new Color(0.1f, 0.1f, 0.15f) }; // 다크 수트
		player.GetComponent<Collider>().material = playerMat;

		// B. [신규] 코드로 검 모델링 생성 (Procedural Mesh)
		GenerateProceduralSword(player, shader);

		// C. 시스템 컴포넌트
		var mover = EnsureComponent<ArcadePhysicsMover>(player);
		SetPrivateField(mover, "moveSpeed", 250000f);
		SetPrivateField(mover, "dashSpeed", 450f);

		var swordMaster = EnsureComponent<SwordMaster>(player);

		// D. [신규] 검기(Wave) 및 이펙트 템플릿 생성
		// 큐브나 라인 렌더러가 아닌, 직접 메쉬를 생성하여 할당
		GameObject waveTemplate = GenerateCrescentMeshTemplate(shader, "Template_SwordWave", Color.cyan);
		waveTemplate.transform.SetParent(root.transform);
		waveTemplate.SetActive(false); // 템플릿은 꺼둠 (SwordMaster에서 켤 때 주의해야 함)

		GameObject flashTemplate = CreateParticleVFX(shader, "Template_FlashVFX", Color.white, 2.5f);
		flashTemplate.transform.SetParent(root.transform);
		flashTemplate.SetActive(false);

		GameObject slashTemplate = CreateParticleVFX(shader, "Template_SlashVFX", Color.cyan, 1.0f);
		slashTemplate.transform.SetParent(root.transform);
		slashTemplate.SetActive(false);

		// SwordMaster 연결
		SetPrivateField(swordMaster, "wavePrefab", waveTemplate);
		SetPrivateField(swordMaster, "flashEffectPrefab", flashTemplate);
		SetPrivateField(swordMaster, "slashEffectPrefab", slashTemplate);
		SetPrivateField(swordMaster, "flashDistance", 8.0f);
		SetPrivateField(swordMaster, "obstacleLayer", (LayerMask)LayerMask.GetMask("Default"));

		// E. 신수 (OrbBeast)
		GameObject beastObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		beastObj.name = "DivineBeast_Orb";
		beastObj.transform.localScale = Vector3.one * 1.0f;
		Material beastMat = new Material(shader) { color = new Color(0f, 1f, 0.6f) };
		beastMat.EnableKeyword("_EMISSION");
		beastMat.SetColor("_EmissionColor", new Color(0f, 1f, 0.6f) * 1.5f);
		beastObj.GetComponent<Renderer>().material = beastMat;

		var orbBeast = EnsureComponent<OrbBeast>(beastObj);
		SetPrivateField(orbBeast, "owner", player.transform);
		SetPrivateField(orbBeast, "orbitRadius", 3.5f);
		SetPrivateField(orbBeast, "orbitSpeed", 220f);

		// F. Brain 연결
		var brain = EnsureComponent<PlayerInputBrain>(player);
		SetPrivateField(brain, "swordMaster", swordMaster);
		SetPrivateField(brain, "currentBeast", orbBeast);

		return player;
	}

	// --- [3] 절차적 메쉬 생성 (Procedural Generation) ---

	// 1. 검(Sword) 모델링: 정점 데이터를 코드로 직접 입력하여 날카로운 검 생성
	private static void GenerateProceduralSword(GameObject player, Shader shader)
	{
		GameObject swordRoot = new GameObject("Visual_Sword");
		swordRoot.transform.SetParent(player.transform);
		swordRoot.transform.localPosition = new Vector3(0.6f, 0.8f, 0.4f);
		swordRoot.transform.localRotation = Quaternion.Euler(10, 0, 0); // 들고 있는 각도

		// 메쉬 필터/렌더러 추가
		GameObject bladeObj = new GameObject("BladeMesh");
		bladeObj.transform.SetParent(swordRoot.transform);
		bladeObj.transform.localPosition = Vector3.zero;
		bladeObj.transform.localRotation = Quaternion.Euler(90, 0, 0); // 눕히기

		MeshFilter mf = bladeObj.AddComponent<MeshFilter>();
		MeshRenderer mr = bladeObj.AddComponent<MeshRenderer>();

		// 다이아몬드 단면의 검날 생성
		Mesh mesh = new Mesh();

		// Vertices (검 끝, 손잡이 쪽 4개 포인트)
		float length = 2.5f;
		float width = 0.15f;
		float thickness = 0.05f;
		float guardPos = 0.5f; // 손잡이 길이

		Vector3 tip = new Vector3(0, length, 0);
		Vector3 baseCenter = new Vector3(0, guardPos, 0);
		Vector3 baseLeft = new Vector3(-width, guardPos, 0);
		Vector3 baseRight = new Vector3(width, guardPos, 0);
		Vector3 baseFront = new Vector3(0, guardPos, -thickness); // 날의 두께
		Vector3 baseBack = new Vector3(0, guardPos, thickness);

		// Handle (손잡이)
		Vector3 handleEnd = new Vector3(0, 0, 0);

		mesh.vertices = new Vector3[]
		{
            // Blade (Top part)
            tip, baseLeft, baseFront, // Face 1
            tip, baseFront, baseRight, // Face 2
            tip, baseRight, baseBack, // Face 3
            tip, baseBack, baseLeft,  // Face 4
            // Blade Base (Bottom closing) - 생략 가능하나 렌더링 위해
            baseLeft, baseBack, baseRight,
			baseRight, baseFront, baseLeft
		};

		mesh.triangles = new int[]
		{
			0, 1, 2,  0, 2, 3,  0, 3, 4,  0, 4, 1,
			5, 6, 7,  7, 8, 5 // Base cap (optional)
        };

		mesh.RecalculateNormals();
		mf.mesh = mesh;

		// 재질: 발광하는 에너지 블레이드
		Material bladeMat = new Material(shader);
		bladeMat.color = new Color(0.6f, 0.9f, 1f, 0.8f);
		bladeMat.EnableKeyword("_EMISSION");
		bladeMat.SetColor("_EmissionColor", new Color(0f, 0.5f, 1f) * 3f);
		// 투명 설정
		SetupTransparentMaterial(bladeMat);
		mr.material = bladeMat;

		// 손잡이 (별도 큐브로 간단 처리)
		GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		handle.transform.SetParent(swordRoot.transform);
		handle.transform.localPosition = new Vector3(0, 0, -0.25f);
		handle.transform.localRotation = Quaternion.Euler(90, 0, 0);
		handle.transform.localScale = new Vector3(0.08f, 0.25f, 0.08f);
		handle.GetComponent<Renderer>().material = new Material(shader) { color = Color.black };
		DestroyImmediate(handle.GetComponent<Collider>());
	}

	// 2. 검기(Wave) 템플릿: 초승달 모양의 메쉬 생성
	private static GameObject GenerateCrescentMeshTemplate(Shader shader, string name, Color color)
	{
		GameObject obj = new GameObject(name);

		// 메쉬 생성
		MeshFilter mf = obj.AddComponent<MeshFilter>();
		MeshRenderer mr = obj.AddComponent<MeshRenderer>();
		Mesh mesh = new Mesh();

		int segments = 12; // 곡선 부드러움 정도
		float innerRadius = 2.0f;
		float outerRadius = 3.5f;
		float arcAngle = 100f;

		List<Vector3> verts = new List<Vector3>();
		List<int> tris = new List<int>();
		List<Color> colors = new List<Color>(); // 알파값 페이딩용

		// Vertices 생성
		for (int i = 0; i <= segments; i++)
		{
			float ratio = (float)i / segments;
			float angle = -arcAngle / 2f + arcAngle * ratio;
			float rad = angle * Mathf.Deg2Rad;
			float cos = Mathf.Cos(rad);
			float sin = Mathf.Sin(rad);

			// 중앙은 두껍고 끝은 얇게 (초승달 모양)
			// 비율에 따라 너비 조정 (0 -> 1 -> 0)
			float widthRatio = Mathf.Sin(ratio * Mathf.PI);
			float currentOuterR = Mathf.Lerp(innerRadius, outerRadius, widthRatio);

			Vector3 innerPos = new Vector3(sin * innerRadius, 0, cos * innerRadius);
			Vector3 outerPos = new Vector3(sin * currentOuterR, 0, cos * currentOuterR);

			verts.Add(innerPos);
			verts.Add(outerPos);

			// Vertex Color: 중앙은 불투명, 끝은 투명하게
			float alpha = widthRatio;
			colors.Add(new Color(color.r, color.g, color.b, alpha)); // Inner
			colors.Add(new Color(color.r, color.g, color.b, 0f));    // Outer (Trail feel)
		}

		// Triangles 생성
		for (int i = 0; i < segments; i++)
		{
			int baseIdx = i * 2;
			// Quad를 구성하는 2개의 Triangle
			tris.Add(baseIdx);
			tris.Add(baseIdx + 1);
			tris.Add(baseIdx + 2);

			tris.Add(baseIdx + 1);
			tris.Add(baseIdx + 3);
			tris.Add(baseIdx + 2);
		}

		mesh.SetVertices(verts);
		mesh.SetTriangles(tris, 0);
		mesh.SetColors(colors); // 쉐이더가 Vertex Color를 지원해야 함
		mesh.RecalculateNormals();
		mf.mesh = mesh;

		// 재질: Additive Particle (Vertex Color 지원)
		Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
		mat.SetFloat("_Mode", 1); // Additive
		mat.SetColor("_Color", color * 2f); // Emission boost
		mr.material = mat;

		// 물리 충돌체
		BoxCollider col = obj.AddComponent<BoxCollider>();
		col.isTrigger = true;
		col.size = new Vector3(4f, 0.5f, 2f);

		var rb = obj.AddComponent<Rigidbody>();
		rb.useGravity = false;
		rb.isKinematic = true;
		rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

		// [중요] 인스턴싱 후 즉시 활성화를 위한 스크립트가 없다면, 
		// SwordMaster에서 SetActive(true)를 해야 함. 
		// 하지만 여기선 물리적으로 존재해야 하므로 Collider만 켜둠.

		return obj;
	}

	private static GameObject CreateParticleVFX(Shader shader, string name, Color color, float scale)
	{
		GameObject obj = new GameObject(name);
		ParticleSystem ps = obj.AddComponent<ParticleSystem>();
		var main = ps.main;
		main.duration = 1.0f;
		main.startLifetime = 0.4f;
		main.startSpeed = 0f; // 제자리 폭발
		main.startSize = 0.5f * scale;
		main.startColor = color;
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		main.playOnAwake = true; // 켜지자마자 재생

		var emission = ps.emission;
		emission.enabled = true;
		emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)(20 * scale)) });

		var shape = ps.shape;
		shape.shapeType = ParticleSystemShapeType.Sphere;
		shape.radius = 0.5f * scale;

		var renderer = obj.GetComponent<ParticleSystemRenderer>();
		renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
		renderer.material.SetColor("_Color", color);

		return obj;
	}

	// --- 유틸리티 ---

	private static void SetupEnemySpawner(GameObject root, Shader shader, GameObject player)
	{
		GameObject spawnerObj = new GameObject("Enemy_Manager");
		spawnerObj.transform.SetParent(root.transform);

		GameObject enemyTemplate = GameObject.CreatePrimitive(PrimitiveType.Cube);
		enemyTemplate.name = "Enemy_Template";
		enemyTemplate.transform.SetParent(spawnerObj.transform);
		enemyTemplate.SetActive(false);
		var rb = enemyTemplate.AddComponent<Rigidbody>();
		rb.mass = 1f; rb.linearDamping = 1f;
		rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
		enemyTemplate.GetComponent<Renderer>().material = new Material(shader) { color = new Color(1f, 0.2f, 0.2f) };
		enemyTemplate.AddComponent<EnemyDestruction>();

		var spawner = EnsureComponent<EnemyRuntimeSpawner>(spawnerObj);
		SetPrivateField(spawner, "enemyPrefab", enemyTemplate);
		SetPrivateField(spawner, "targetPlayer", player.transform);
		SetPrivateField(spawner, "maxEnemyCount", ENEMY_COUNT);
	}

	private static void SetupCamera(GameObject root, GameObject target)
	{
		Camera cam = Camera.main;
		if (!cam) { GameObject c = new GameObject("Main Camera"); cam = c.AddComponent<Camera>(); c.tag = "MainCamera"; }
		cam.transform.SetParent(root.transform);
		cam.transform.position = new Vector3(0, 60, -10);
		cam.transform.rotation = Quaternion.Euler(80, 0, 0);
		cam.farClipPlane = 2000f;

		var script = EnsureComponent<TopDownActionCamera>(cam.gameObject);
		var setTargetMethod = script.GetType().GetMethod("SetTarget");
		if (setTargetMethod != null) setTargetMethod.Invoke(script, new object[] { target.transform, null });
		SetPrivateField(script, "minFOV", 50f); SetPrivateField(script, "maxFOV", 75f);
	}

	private static Vector3 GetRandomPosOnMap(float minDistanceFromCenter)
	{
		float range = (MAP_SIZE / 2f) - 200f;
		Vector3 pos;
		do { pos = new Vector3(Random.Range(-range, range), 0, Random.Range(-range, range)); }
		while (pos.magnitude < minDistanceFromCenter);
		return pos;
	}

	private static void SetPrivateField(object t, string n, object v)
	{
		if (t == null) return;
		var f = t.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
		if (f != null) f.SetValue(t, v);
	}

	private static void SetupTransparentMaterial(Material mat)
	{
		mat.SetFloat("_Mode", 2); // Fade
		mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
		mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
		mat.SetInt("_ZWrite", 0);
		mat.DisableKeyword("_ALPHATEST_ON");
		mat.EnableKeyword("_ALPHABLEND_ON");
		mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
		mat.renderQueue = 3000;
	}

	private static void ClearEnvironment() { GameObject g = GameObject.Find("[ProjectA_CombatEnv]"); if (g) Undo.DestroyObjectImmediate(g); }
	private static T EnsureComponent<T>(GameObject o) where T : Component { T c = o.GetComponent<T>(); if (!c) c = o.AddComponent<T>(); return c; }
	private static Shader GetProperShader() { Shader s = Shader.Find("Universal Render Pipeline/Lit"); if (!s) s = Shader.Find("Standard"); return s; }

	private static Texture2D GenerateGridTexture()
	{
		int size = 256; Texture2D tex = new Texture2D(size, size);
		Color c1 = new Color(0.15f, 0.15f, 0.2f); Color c2 = new Color(0.2f, 0.2f, 0.25f);
		for (int y = 0; y < size; y++) { for (int x = 0; x < size; x++) { tex.SetPixel(x, y, ((x / 32) + (y / 32)) % 2 == 0 ? c1 : c2); } }
		tex.filterMode = FilterMode.Point; tex.Apply(); return tex;
	}
}