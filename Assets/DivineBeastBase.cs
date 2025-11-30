using UnityEngine;

// [핵심] 모듈화를 위한 추상 클래스
// 어떤 종류의 신수(오브, 터렛, 힐러 등)가 와도 PlayerInputBrain은 이 인터페이스만 알면 됨.
public abstract class DivineBeastBase : MonoBehaviour
{
    protected Transform owner;

    // 초기화 (생성 시 또는 장착 시 호출)
    public virtual void Initialize(Transform player)
    {
        owner = player;
    }

    // 장착/해제 시 로직
    public virtual void Activate() { gameObject.SetActive(true); }
    public virtual void Deactivate() { gameObject.SetActive(false); }

    // 매 프레임 실행될 패시브 동작 (공전, 추적, 대기 등)
    protected abstract void PassiveUpdate();

    // 입력 신호 인터페이스 (Q 키)
    public abstract void OnInputDown();              // 눌렀을 때
    public abstract void OnInputHold(Vector3 target); // 누르는 중
    public abstract void OnInputUp(Vector3 target);   // 뗐을 때

    protected void Update()
    {
        if (owner) PassiveUpdate();
    }
}