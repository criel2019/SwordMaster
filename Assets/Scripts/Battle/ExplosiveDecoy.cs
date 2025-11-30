using UnityEngine;

public class ExplosiveDecoy : MonoBehaviour
{
    [Header("Explosion Settings")]
    [SerializeField] private float delay = 2.0f;       // 폭발 대기 시간
    [SerializeField] private float radius = 10f;       // 폭발 반경
    [SerializeField] private float force = 2000f;      // 넉백 힘

    private bool _isTriggered = false;
    private float _timer;

    public void Setup(float fuseTime)
    {
        delay = fuseTime;
        _timer = 0f;
    }

    // 저스트 회피 성공 시 즉시 기폭
    public void DetonateInstant(float multiplier = 1.5f)
    {
        if (_isTriggered) return;
        
        // 보너스: 범위와 위력 증가
        radius *= multiplier;
        force *= multiplier;
        Explode();
    }

    private void Update()
    {
        if (_isTriggered) return;

        _timer += Time.deltaTime;
        if (_timer >= delay)
        {
            Explode();
        }
    }

    private void Explode()
    {
        _isTriggered = true;

        // 1. 범위 내 적 넉백 처리
        Collider[] colliders = Physics.OverlapSphere(transform.position, radius);
        foreach (var col in colliders)
        {
            if (col.CompareTag("Enemy"))
            {
                Rigidbody rb = col.GetComponent<Rigidbody>();
                if (rb)
                {
                    rb.AddExplosionForce(force, transform.position, radius, 1f, ForceMode.Impulse);
                }
                
                // 몬스터 체력 시스템이 있다면 여기서 데미지 처리
                // Destroy(col.gameObject, 0.5f); // 예시: 넉백 후 삭제
            }
        }

        // 2. 시각 효과 (임시 빨간 구체)
        GameObject vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        vfx.transform.position = transform.position;
        vfx.transform.localScale = Vector3.one * radius;
        Destroy(vfx.GetComponent<Collider>());
        
        Material mat = vfx.GetComponent<Renderer>().material;
        mat.color = new Color(1f, 0f, 0f, 0.5f); // 반투명 빨강
        // Standard Shader 투명 설정은 코드만으로는 복잡하므로 
        // 실제로는 파티클 프리팹(Instantiate)을 사용하는 것을 권장합니다.

        Destroy(vfx, 0.2f); // 이펙트 0.2초 뒤 삭제

        // 3. 미끼 자신 삭제
        Destroy(gameObject);
    }
}