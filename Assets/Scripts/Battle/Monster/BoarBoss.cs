using UnityEngine;
using System.Collections;

/// <summary>
/// 멧돼지 보스 - 돌진 공격
/// 전조: 붉은 카펫(트레일)을 깔고 그 경로를 따라 돌진
/// </summary>
public class BoarBoss : BossBase
{
    [Header("Charge Attack Settings")]
    [SerializeField] private float chargeSpeed = 15f; // 돌진 속도 (3배 빠름)
    [SerializeField] private float chargeDistance = 30f; // 돌진 거리
    [SerializeField] private float chargeDuration = 2f; // 돌진 지속 시간
    [SerializeField] private float chargeIndicatorDuration = 1.5f; // 전조 표시 시간

    [Header("Trail Settings (Red Carpet)")]
    [SerializeField] private Color trailColor = Color.red;
    [SerializeField] private float trailWidth = 3f;
    [SerializeField] private float trailTime = 2f;

    [Header("Player Damage")]
    [SerializeField] private float damageRadius = 2f;
    [SerializeField] private int playerDamage = 1;

    // 돌진 상태
    private enum ChargeState { Idle, Preparing, Charging, Recovering }
    private ChargeState _chargeState = ChargeState.Idle;
    private Vector3 _chargeDirection;
    private float _chargeTimer;
    private LineRenderer _chargeIndicator;
    private bool _hasHitPlayerThisCharge;

    protected override void Awake()
    {
        base.Awake();
        CreateChargeIndicator();
    }

    public override void UpdateBehavior()
    {
        if (!_isAlive || !_player) return;

        switch (_chargeState)
        {
            case ChargeState.Idle:
                UpdateIdleBehavior();
                break;

            case ChargeState.Preparing:
                UpdatePreparingBehavior();
                break;

            case ChargeState.Charging:
                UpdateChargingBehavior();
                break;

            case ChargeState.Recovering:
                UpdateRecoveringBehavior();
                break;
        }
    }

    private void UpdateIdleBehavior()
    {
        // 플레이어 추적 (느리게)
        Vector3 dirToPlayer = (_player.position - transform.position);
        dirToPlayer.y = 0;
        float distance = dirToPlayer.magnitude;

        // 일정 거리 이상 떨어지면 추적
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

        // 공격 시도
        if (CanAttack())
        {
            ExecuteBasicAttack();
        }
    }

    public override void ExecuteBasicAttack()
    {
        if (_chargeState != ChargeState.Idle) return;

        // 돌진 준비 시작
        _chargeState = ChargeState.Preparing;
        _chargeTimer = 0f;
        _hasHitPlayerThisCharge = false;

        // 플레이어 방향으로 돌진 방향 설정
        _chargeDirection = (_player.position - transform.position).normalized;
        _chargeDirection.y = 0;

        // 붉은 카펫 표시
        ShowChargeIndicator();

        StartAttackCooldown();

        Debug.Log("[BoarBoss] 돌진 준비 중...");
    }

    private void UpdatePreparingBehavior()
    {
        _chargeTimer += Time.deltaTime;

        // 멈춤
        _rb.linearVelocity = Vector3.zero;

        // 전조 시간 종료 → 돌진 시작
        if (_chargeTimer >= chargeIndicatorDuration)
        {
            _chargeState = ChargeState.Charging;
            _chargeTimer = 0f;
            HideChargeIndicator();

            Debug.Log("[BoarBoss] 돌진 시작!");
        }
    }

    private void UpdateChargingBehavior()
    {
        _chargeTimer += Time.deltaTime;

        // 돌진
        _rb.linearVelocity = _chargeDirection * chargeSpeed;
        transform.rotation = Quaternion.LookRotation(_chargeDirection);

        // 플레이어와 충돌 체크
        CheckPlayerCollision();

        // 돌진 종료
        if (_chargeTimer >= chargeDuration)
        {
            _chargeState = ChargeState.Recovering;
            _chargeTimer = 0f;

            Debug.Log("[BoarBoss] 돌진 종료");
        }
    }

    private void UpdateRecoveringBehavior()
    {
        _chargeTimer += Time.deltaTime;

        // 감속
        _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, Vector3.zero, Time.deltaTime * 3f);

        // 복구 시간 (0.5초)
        if (_chargeTimer >= 0.5f)
        {
            _chargeState = ChargeState.Idle;
        }
    }

    private void CheckPlayerCollision()
    {
        if (_hasHitPlayerThisCharge) return;

        float distToPlayer = Vector3.Distance(transform.position, _player.position);

        if (distToPlayer <= damageRadius)
        {
            _hasHitPlayerThisCharge = true;

            // 플레이어 데미지 (로그만)
            Debug.Log($"<color=yellow>[BoarBoss] 플레이어에게 {playerDamage} 데미지!</color>");

            // TODO: 플레이어 체력 시스템 추가 시 실제 데미지 처리
            // _player.GetComponent<PlayerHealth>()?.TakeDamage(playerDamage);
        }
    }

    private void CreateChargeIndicator()
    {
        GameObject indicatorObj = new GameObject("ChargeIndicator");
        indicatorObj.transform.SetParent(transform);
        indicatorObj.transform.localPosition = Vector3.zero;

        _chargeIndicator = indicatorObj.AddComponent<LineRenderer>();
        _chargeIndicator.startWidth = trailWidth;
        _chargeIndicator.endWidth = trailWidth;
        _chargeIndicator.positionCount = 2;
        _chargeIndicator.startColor = trailColor;
        _chargeIndicator.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0.3f);
        _chargeIndicator.material = new Material(Shader.Find("Sprites/Default"));
        _chargeIndicator.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _chargeIndicator.receiveShadows = false;
        _chargeIndicator.enabled = false;
    }

    private void ShowChargeIndicator()
    {
        if (_chargeIndicator == null) return;

        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + (_chargeDirection * chargeDistance);

        _chargeIndicator.SetPosition(0, startPos);
        _chargeIndicator.SetPosition(1, endPos);
        _chargeIndicator.enabled = true;
    }

    private void HideChargeIndicator()
    {
        if (_chargeIndicator != null)
        {
            _chargeIndicator.enabled = false;
        }
    }

    public override void ExecuteSpecialAttack()
    {
        // TODO: 특수 공격 패턴 (범위, 투사체 등)
        // 현재는 구현하지 않음 (인터페이스만 준비)
        Debug.Log("[BoarBoss] 특수 공격 - 아직 미구현");
    }

    protected override void OnDamaged()
    {
        base.OnDamaged();

        // 피격 시 약간 넉백 효과 (선택적)
        // 돌진 중이 아닐 때만
        if (_chargeState == ChargeState.Idle && _rb != null)
        {
            Vector3 knockbackDir = (transform.position - _player.position).normalized;
            _rb.AddForce(knockbackDir * 3f, ForceMode.Impulse);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 벽이나 장애물과 충돌 시 돌진 중단
        if (_chargeState == ChargeState.Charging)
        {
            if (!collision.gameObject.CompareTag("Player"))
            {
                _chargeState = ChargeState.Recovering;
                _chargeTimer = 0f;
                Debug.Log("[BoarBoss] 장애물 충돌! 돌진 중단");
            }
        }
    }

    private void OnDestroy()
    {
        if (_chargeIndicator != null)
        {
            Destroy(_chargeIndicator.gameObject);
        }
    }
}
