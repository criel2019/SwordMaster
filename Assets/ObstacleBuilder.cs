using System.Linq;
using UnityEditor;
using UnityEngine;

public class ObstacleBuilder : EditorWindow
{
    private const string OBSTACLE_TAG = "Obstacle"; // 모든 장애물에 부여할 태그
    private const float MAP_SIZE = 100f; // 맵의 전체 크기 (X, Z 평면)

    // --- Pillar 설정 ---
    private int pillarCount = 10;
    private float pillarRadius = 2f;
    private float pillarHeight = 5f;

    // --- Low Wall 설정 ---
    private int wallSegmentCount = 4;
    private float wallThickness = 3f;
    private float wallLength = 40f;
    private float wallDistanceFromCenter = 30f; // 벽이 중앙에서 얼마나 떨어져 배치될지

    // --- 물리 재질 (바운스) 설정 ---
    private PhysicsMaterial bouncyMaterial;

    [MenuItem("Tools/Build Orbit Hammer Obstacles")]
    public static void ShowWindow()
    {
        GetWindow<ObstacleBuilder>("Obstacle Builder");
    }

    private void OnGUI()
    {
        GUILayout.Label("Orbit Hammer Obstacle Setup", EditorStyles.boldLabel);
        
        // 물리 재질 필드
        bouncyMaterial = (PhysicsMaterial)EditorGUILayout.ObjectField("Bouncy Physics Material", bouncyMaterial, typeof(PhysicsMaterial), false);
        EditorGUILayout.Space(10);

        // Pillar 설정
        GUILayout.Label("Pillar Settings", EditorStyles.boldLabel);
        pillarCount = EditorGUILayout.IntSlider("Pillar Count", pillarCount, 5, 20);
        pillarRadius = EditorGUILayout.FloatField("Pillar Radius", pillarRadius);
        pillarHeight = EditorGUILayout.FloatField("Pillar Height", pillarHeight);

        EditorGUILayout.Space(5);

        // Low Wall 설정
        GUILayout.Label("Low Wall Settings", EditorStyles.boldLabel);
        wallSegmentCount = EditorGUILayout.IntSlider("Wall Segment Count", wallSegmentCount, 2, 8);
        wallThickness = EditorGUILayout.FloatField("Wall Thickness", wallThickness);
        wallLength = EditorGUILayout.FloatField("Wall Length", wallLength);
        wallDistanceFromCenter = EditorGUILayout.FloatField("Wall Distance", wallDistanceFromCenter);

        EditorGUILayout.Space(20);

        if (GUILayout.Button("GENERATE OBSTACLES"))
        {
            GenerateObstacles();
        }
    }

    private void GenerateObstacles()
    {
        // 기존 장애물 제거
        var existingParent = GameObject.Find("Obstacles");
        if (existingParent) DestroyImmediate(existingParent);

        GameObject parent = new GameObject("Obstacles");

        // 1. Tag 설정 확인 및 생성
        if (!UnityEditorInternal.InternalEditorUtility.tags.Contains(OBSTACLE_TAG))
        {
            Debug.LogError($"'{OBSTACLE_TAG}' 태그가 없어 장애물에 적용할 수 없습니다. Tag를 직접 추가해주세요.");
            return;
        }

        // 2. Pillar 배치
        for (int i = 0; i < pillarCount; i++)
        {
            float angle = Random.Range(0f, 360f);
            float radius = Random.Range(pillarRadius * 4f, MAP_SIZE / 2f - wallThickness - 5f);
            
            float x = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
            float z = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
            Vector3 pos = new Vector3(x, pillarHeight / 2f, z);
            
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Pillar_" + i;
            SetupObstacle(pillar, pos, new Vector3(pillarRadius * 2f, pillarHeight, pillarRadius * 2f), parent.transform);
        }

        // 3. Low Wall (Narrow Corridor) 배치
        float angleStep = 360f / wallSegmentCount;
        for (int i = 0; i < wallSegmentCount; i++)
        {
            float angleDeg = angleStep * i;
            Quaternion rotation = Quaternion.Euler(0f, angleDeg, 0f);

            // 벽을 배치할 중심 위치 계산 (원형 배치)
            float xCenter = Mathf.Cos(angleDeg * Mathf.Deg2Rad) * wallDistanceFromCenter;
            float zCenter = Mathf.Sin(angleDeg * Mathf.Deg2Rad) * wallDistanceFromCenter;
            Vector3 centerPos = new Vector3(xCenter, wallThickness / 2f, zCenter);

            // 벽 오브젝트 생성 및 설정
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "LowWall_" + i;
            
            // 크기: 길이(X), 높이(Y), 두께(Z)
            Vector3 wallScale = new Vector3(wallLength, wallThickness, wallThickness); 
            
            SetupObstacle(wall, centerPos, wallScale, parent.transform, rotation);
        }

        Debug.Log($"Orbit Hammer Obstacles Generated: {pillarCount} Pillars, {wallSegmentCount} Walls.");
    }

    private void SetupObstacle(GameObject obj, Vector3 pos, Vector3 scale, Transform parent, Quaternion rotation = default)
    {
        obj.transform.position = pos;
        obj.transform.localScale = scale;
        obj.transform.rotation = rotation;
        obj.transform.SetParent(parent);
        
        // Rigidbody 설정 (Static Obstacle)
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null) rb = obj.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        
        // Collider 및 Tag 설정
        Collider col = obj.GetComponent<Collider>();
        if (col) col.material = bouncyMaterial; 
        obj.tag = OBSTACLE_TAG;
    }
}