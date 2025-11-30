using UnityEngine;

public class EnemyDestruction : MonoBehaviour
{
    [SerializeField] private int debrisCount = 8; // 파편 개수
    [SerializeField] private float debrisForce = 15f; // 파편 튀는 힘

    public void ShatterAndDie()
    {
        // 1. 파편 생성 (큐브를 8조각 냄)
        Vector3 originalPos = transform.position;
        Vector3 originalScale = transform.localScale;
        Material originalMat = GetComponent<Renderer>().material;
        Color originalColor = originalMat.color;

        // 2x2x2 그리드로 파편 생성
        float subScale = 0.5f;
        
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    CreateDebris(originalPos, originalScale * subScale, new Vector3(x, y, z), originalColor);
                }
            }
        }

        // 2. 본체 삭제
        Destroy(gameObject);
    }

    private void CreateDebris(Vector3 center, Vector3 scale, Vector3 offsetDir, Color color)
    {
        GameObject debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
        
        // 위치: 중심에서 약간 떨어진 곳
        debris.transform.position = center + Vector3.Scale(offsetDir, scale * 0.5f);
        debris.transform.localScale = scale * 0.8f; // 약간 작게 해서 틈새를 줌

        // 시각 설정
        var mr = debris.GetComponent<Renderer>();
        mr.material = new Material(Shader.Find("Universal Render Pipeline/Lit")); // 혹은 Standard
        if (!mr.material) mr.material = new Material(Shader.Find("Standard"));
        mr.material.color = color;

        // 물리 설정
        var rb = debris.AddComponent<Rigidbody>();
        rb.mass = 0.1f;
        
        // 폭발력 적용 (중심에서 바깥으로 확 퍼지게)
        Vector3 forceDir = offsetDir.normalized + Random.insideUnitSphere * 0.5f;
        rb.AddForce(forceDir * debrisForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * debrisForce * 2f, ForceMode.Impulse);

        // 충돌 설정 (파편끼리는 부딪히되, 플레이어나 공 길막은 안 하게)
        // 레이어 설정이 복잡하니 여기선 콜라이더를 끄거나, 작게 만들거나, 금방 삭제함
        // 여기선 0.5초 뒤 삭제하므로 둠
        Destroy(debris, 0.6f);
    }
}