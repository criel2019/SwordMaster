using UnityEngine;
using System.Collections;

public class FlashCutSkill : MonoBehaviour, ISkill
{
    [Header("Skill Info")]
    [SerializeField] private string skillId = "flash_cut";
    [SerializeField] private string skillName = "발검";
    [SerializeField] private int staminaCost = 0;
    [SerializeField] private float cooldown = 0f;
    
    [Header("Combo")]
    [SerializeField] private string[] comboFrom = null;
    [SerializeField] private float comboAllowTime = 0f;
    
    [Header("Flash Settings")]
    [SerializeField] private float flashWidth = 2.5f;
    [SerializeField] private float flashDuration = 0.08f;
    [SerializeField] private LayerMask obstacleLayer;
    
    [Header("Trail Settings")]
    [SerializeField] private Color trailStartColor = new Color(0.4f, 0.8f, 1f, 1f);
    [SerializeField] private Color trailEndColor = new Color(0.2f, 0.5f, 1f, 0f);
    [SerializeField] private float trailWidth = 1.2f;
    [SerializeField] private float trailTime = 0.25f;
    
    private float _lastUseTime = -999f;
    private bool _isExecuting;
    
    private TrailRenderer _flashTrail;
    private Rigidbody _rigidbody;
    private ArcadePhysicsMover _mover;
    private Camera _mainCamera;
    
    // ISkill 구현
    public string SkillId => skillId;
    public string SkillName => skillName;
    public int StaminaCost => staminaCost;
    public float Cooldown => cooldown;
    public bool IsReady => !_isExecuting && (cooldown <= 0 || Time.time >= _lastUseTime + cooldown);
    public bool IsExecuting => _isExecuting;
    public bool IsChargeSkill => false;
    public string[] ComboFrom => comboFrom;
    public float ComboAllowTime => comboAllowTime;
    
    private void Awake()
    {
        _rigidbody = GetComponentInParent<Rigidbody>();
        _mover = GetComponentInParent<ArcadePhysicsMover>();
        _mainCamera = Camera.main;
        CreateFlashTrail();
    }
    
    private void CreateFlashTrail()
    {
        GameObject trailObj = new GameObject("FlashTrail");
        trailObj.transform.SetParent(transform.parent);  // 플레이어에 붙임
        trailObj.transform.localPosition = Vector3.zero;
        
        _flashTrail = trailObj.AddComponent<TrailRenderer>();
        _flashTrail.time = trailTime;
        _flashTrail.startWidth = trailWidth;
        _flashTrail.endWidth = trailWidth * 0.3f;
        _flashTrail.minVertexDistance = 0.1f;
        _flashTrail.autodestruct = false;
        _flashTrail.emitting = false;
        _flashTrail.numCapVertices = 4;
        _flashTrail.numCornerVertices = 4;
        _flashTrail.startColor = trailStartColor;
        _flashTrail.endColor = trailEndColor;
        
        Material trailMat = new Material(Shader.Find("Sprites/Default"));
        _flashTrail.material = trailMat;
        _flashTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _flashTrail.receiveShadows = false;
    }
    
    public void Execute(Vector3 targetPos)
    {
        if (_isExecuting) return;
        
        Vector3 clampedTarget = ClampToViewport(targetPos);
        Vector3 startPos = transform.parent.position;  // 플레이어 위치
        Vector3 dir = (clampedTarget - startPos);
        dir.y = 0;
        
        float dist = dir.magnitude;
        if (dist < 0.5f) return;
        
        dir.Normalize();
        
        // 벽(Obstacle)만 체크
        if (Physics.Raycast(startPos, dir, out RaycastHit hit, dist, obstacleLayer, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.CompareTag("Obstacle"))
            {
                dist = Mathf.Max(0.5f, hit.distance - 0.5f);
            }
        }
        
        Vector3 endPos = startPos + dir * dist;
        
        _lastUseTime = Time.time;
        StartCoroutine(FlashCutRoutine(startPos, endPos, dir, dist));
    }
    
    // 차징 스킬 아님 - 빈 구현
    public void StartCharge() { }
    public void UpdateCharge(float duration) { }
    public void ReleaseCharge(Vector3 targetPos, float duration) { }
    public void Cancel() { _isExecuting = false; }
    
    private Vector3 ClampToViewport(Vector3 worldPos)
    {
        if (_mainCamera == null) return worldPos;
        
        Vector3 viewportPos = _mainCamera.WorldToViewportPoint(worldPos);
        const float margin = 0.05f;
        viewportPos.x = Mathf.Clamp(viewportPos.x, margin, 1f - margin);
        viewportPos.y = Mathf.Clamp(viewportPos.y, margin, 1f - margin);
        
        Vector3 clampedWorld = _mainCamera.ViewportToWorldPoint(viewportPos);
        clampedWorld.y = worldPos.y;
        
        return clampedWorld;
    }
    
    private IEnumerator FlashCutRoutine(Vector3 startPos, Vector3 endPos, Vector3 dir, float dist)
    {
        _isExecuting = true;
        Transform playerTransform = transform.parent;
        
        if (_mover != null)
        {
            _mover.Freeze();
        }
        
        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.isKinematic = true;
        }
        
        _flashTrail.Clear();
        _flashTrail.emitting = true;
        
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flashDuration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            
            playerTransform.position = Vector3.Lerp(startPos, endPos, easedT);
            yield return null;
        }
        
        playerTransform.position = endPos;
        
        DealFlashDamage(startPos, dir, dist);
        
        _flashTrail.emitting = false;
        
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
        
        if (_mover != null)
        {
            _mover.Unfreeze();
        }
        
        _isExecuting = false;
    }
    
    private void DealFlashDamage(Vector3 startPos, Vector3 dir, float dist)
    {
        RaycastHit[] hits = Physics.SphereCastAll(startPos, flashWidth * 0.5f, dir, dist);
        int killCount = 0;
        
        foreach (var h in hits)
        {
            if (h.collider.CompareTag("Enemy"))
            {
                var destruction = h.collider.GetComponent<EnemyDestruction>();
                if (destruction != null)
                {
                    destruction.ShatterAndDie();
                    killCount++;
                }
            }
        }
        
        if (killCount > 0)
        {
            Debug.Log($"<color=red>⚡ Flash Cut: {killCount} slain!</color>");
        }
    }
}