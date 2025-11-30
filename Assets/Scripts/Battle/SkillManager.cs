using UnityEngine;
using System.Collections.Generic;

public class SkillManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStamina stamina;
    
    private Dictionary<string, ISkill> _skills = new Dictionary<string, ISkill>();
    
    // 콤보 추적
    private string _lastSkillId;
    private float _lastSkillTime;
    
    public string LastSkillId => _lastSkillId;
    public float TimeSinceLastSkill => Time.time - _lastSkillTime;
    
    private void Awake()
    {
        if (!stamina) stamina = GetComponent<PlayerStamina>();
        
        // 자식 오브젝트에서 모든 스킬 수집
        var skillComponents = GetComponentsInChildren<MonoBehaviour>();
        foreach (var component in skillComponents)
        {
            if (component is ISkill skill)
            {
                RegisterSkill(skill);
            }
        }
    }
    
    public void RegisterSkill(ISkill skill)
    {
        if (!_skills.ContainsKey(skill.SkillId))
        {
            _skills[skill.SkillId] = skill;
            Debug.Log($"[SkillManager] Registered: {skill.SkillName} ({skill.SkillId})");
        }
    }
    
    public ISkill GetSkill(string skillId)
    {
        _skills.TryGetValue(skillId, out var skill);
        return skill;
    }
    
    public bool TryExecute(string skillId, Vector3 targetPos)
    {
        var skill = GetSkill(skillId);
        if (skill == null) return false;
        
        if (!skill.IsReady) return false;
        
        // 스태미나 체크
        if (skill.StaminaCost > 0)
        {
            if (!stamina.TryConsume(skill.StaminaCost)) return false;
        }
        
        // 콤보 체크 (콤보 스킬인 경우)
        if (skill.ComboFrom != null && skill.ComboFrom.Length > 0)
        {
            if (!CheckCombo(skill)) return false;
        }
        
        skill.Execute(targetPos);
        RecordSkillUse(skillId);
        return true;
    }
    
    public bool TryStartCharge(string skillId)
    {
        var skill = GetSkill(skillId);
        if (skill == null || !skill.IsChargeSkill) return false;
        if (!skill.IsReady) return false;
        
        skill.StartCharge();
        return true;
    }
    
    public void UpdateCharge(string skillId, float duration)
    {
        var skill = GetSkill(skillId);
        if (skill == null || !skill.IsChargeSkill) return;
        
        skill.UpdateCharge(duration);
    }
    
    public bool TryReleaseCharge(string skillId, Vector3 targetPos, float duration)
    {
        var skill = GetSkill(skillId);
        if (skill == null || !skill.IsChargeSkill) return false;
        
        // 스태미나 체크
        if (skill.StaminaCost > 0)
        {
            if (!stamina.TryConsume(skill.StaminaCost)) return false;
        }
        
        skill.ReleaseCharge(targetPos, duration);
        RecordSkillUse(skillId);
        return true;
    }
    
    public void CancelSkill(string skillId)
    {
        var skill = GetSkill(skillId);
        skill?.Cancel();
    }
    
    private bool CheckCombo(ISkill skill)
    {
        if (string.IsNullOrEmpty(_lastSkillId)) return false;
        if (TimeSinceLastSkill > skill.ComboAllowTime) return false;
        
        foreach (var fromId in skill.ComboFrom)
        {
            if (_lastSkillId == fromId) return true;
        }
        return false;
    }
    
    private void RecordSkillUse(string skillId)
    {
        _lastSkillId = skillId;
        _lastSkillTime = Time.time;
    }
    
    public void ClearCombo()
    {
        _lastSkillId = null;
    }
}