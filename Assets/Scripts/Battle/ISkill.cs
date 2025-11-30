using UnityEngine;

public interface ISkill
{
    string SkillId { get; }
    string SkillName { get; }
    int StaminaCost { get; }
    float Cooldown { get; }
    bool IsReady { get; }
    bool IsExecuting { get; }
    bool IsChargeSkill { get; }
    
    // 콤보용
    string[] ComboFrom { get; }    // 이 스킬 전에 와야 하는 스킬 ID들 (null이면 콤보 아님)
    float ComboAllowTime { get; }  // 콤보 허용 시간
    
    void Execute(Vector3 targetPos);
    void StartCharge();
    void UpdateCharge(float duration);
    void ReleaseCharge(Vector3 targetPos, float duration);
    void Cancel();
}