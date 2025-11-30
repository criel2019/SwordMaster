using UnityEngine;
using System.Collections;

public class DivineBeastSystem : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private GameObject beastPrefab;      // 신수(펫) 프리팹 (공 모양)
    [SerializeField] private float passiveOrbitSpeed = 180f; // 평소 회전 속도
    [SerializeField] private float passiveRadius = 4f;       // 평소 회전 반경
    [SerializeField] private float passiveDamage = 10f;      // 스칠 때 데미지

    [Header("Q Interaction (Focus)")]
    [SerializeField] private float focusShrinkSpeed = 5f;    // Q 누를 때 반경 줄어드는 속도
    [SerializeField] private float minFocusRadius = 1.0f;    // 이 반경이 '골드 존' (타이밍)
    [SerializeField] private float perfectZoneTolerance = 0.5f; // 판정 여유 범위 (+- 0.5)

    [Header("Fire Settings")]
    [SerializeField] private float fireSpeed = 40f;
    [SerializeField] private float returnDelay = 2.0f;       // 발사 후 복귀까지 걸리는 시간

    // 내부 상태
    private Transform _beast;      // 실제 신수 오브젝트
    private Rigidbody _beastRb;
    private TrailRenderer _beastTrail;
    
    private float _currentAngle;
    private float _currentRadius;
    private bool _isFocusing;      // Q 누르는 중인가
    private bool _isFired;         // 발사되어 날아가는 중인가

    private void Start()
    {
        if (beastPrefab)
        {
            // 신수 생성
            GameObject obj = Instantiate(beastPrefab, transform.position, Quaternion.identity);
            _beast = obj.transform;
            _beastRb = obj.GetComponent<Rigidbody>();
            _beastTrail = obj.GetComponent<TrailRenderer>();

            // 신수 자체에 충돌 데미지 스크립트를 붙여주거나, 
            // 여기서 OnCollision을 감지할 수 있도록 설정해야 함.
            // (간단하게 구현하기 위해 여기서 물리 제어만 담당)
            if (_beastRb) _beastRb.isKinematic = true; // 평소엔 물리 끄고 직접 이동
        }

        _currentRadius = passiveRadius;
    }

    private void FixedUpdate()
    {
        if (_beast == null) return;

        // 1. 발사된 상태면 오비트 로직 무시
        if (_isFired) return;

        // 2. Q 입력에 따른 반경 조절
        if (_isFocusing)
        {
            // 반경을 줄임 (플레이어 쪽으로 당김)
            _currentRadius = Mathf.MoveTowards(_currentRadius, minFocusRadius, focusShrinkSpeed * Time.fixedDeltaTime);
        }
        else
        {
            // 다시 원래대로 복구
            _currentRadius = Mathf.MoveTowards(_currentRadius, passiveRadius, focusShrinkSpeed * 2f * Time.fixedDeltaTime);
        }

        // 3. 회전 처리 (Vampire Survivors 스타일)
        float speed = _isFocusing ? passiveOrbitSpeed * 2f : passiveOrbitSpeed; // 집중하면 더 빨리 돔
        _currentAngle += speed * Time.fixedDeltaTime;
        if (_currentAngle >= 360f) _currentAngle -= 360f;

        // 4. 위치 갱신
        float rad = _currentAngle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * _currentRadius;
        _beastRb.MovePosition(transform.position + offset);

        // 5. 패시브 데미지 (회전 중 적과 닿으면)
        CheckPassiveCollision();
    }

    // Q 누름 시작
    public void StartFocus()
    {
        if (_isFired) return; // 이미 날아갔으면 무시
        _isFocusing = true;
        
        // 시각적 피드백 (예: 신수가 노랗게 빛남)
        SetBeastColor(Color.yellow);
    }

    // Q 뗌 (발사 시도)
    public void ReleaseFocus(Vector3 aimPos)
    {
        if (_isFired) return;
        _isFocusing = false;

        // 타이밍 판정: 현재 반경이 최소 반경 근처인가?
        float diff = Mathf.Abs(_currentRadius - minFocusRadius);
        bool isPerfect = diff <= perfectZoneTolerance;

        if (isPerfect)
        {
            // 성공: 마우스 방향으로 발사!
            FireBeast(aimPos);
        }
        else
        {
            // 실패: 그냥 뻘줌하게 원래 궤도로 돌아감 (색상 복구)
            SetBeastColor(Color.white);
            Debug.Log("Miss... 타이밍이 안맞았습니다.");
        }
    }

    private void FireBeast(Vector3 targetPos)
    {
        _isFired = true;
        _beastRb.isKinematic = false; // 물리 켜기

        // 방향 계산
        Vector3 dir = (targetPos - transform.position).normalized;
        dir.y = 0; // 높이 고정

        _beastRb.linearVelocity = dir * fireSpeed;
        
        // 이펙트 강화
        SetBeastColor(Color.red); // 공격색
        if (_beastTrail) _beastTrail.startWidth = 1.0f; // 꼬리 굵게

        // 일정 시간 후 복귀
        StartCoroutine(Routine_ReturnBeast());
    }

    private IEnumerator Routine_ReturnBeast()
    {
        yield return new WaitForSeconds(returnDelay);

        // 복귀 로직
        _isFired = false;
        _beastRb.isKinematic = true;
        _beastRb.linearVelocity = Vector3.zero;
        
        // 즉시 플레이어 옆으로 텔레포트하지 않고 부드럽게 오게 하려면 추가 로직 필요하지만,
        // 여기선 즉시 오비트 궤도로 복귀시킴
        _currentRadius = passiveRadius;
        SetBeastColor(Color.white);
        if (_beastTrail) _beastTrail.startWidth = 0.5f;
    }

    private void CheckPassiveCollision()
    {
        // 간단한 충돌 체크 (SphereOverlap)
        Collider[] hits = Physics.OverlapSphere(_beast.position, 1.0f);
        foreach (var h in hits)
        {
            if (h.CompareTag("Enemy"))
            {
                // 데미지 주기 (기존 EnemyDestruction 활용)
                // 패시브 데미지는 약하게, 발사 데미지는 강하게 처리 가능
                // 여기선 데미지 로직 생략하고 로그만
                // Debug.Log("신수 접촉 데미지!");
            }
        }
    }

    private void SetBeastColor(Color c)
    {
        if (_beast)
        {
            var rend = _beast.GetComponent<Renderer>();
            if (rend) rend.material.color = c;
        }
    }
}