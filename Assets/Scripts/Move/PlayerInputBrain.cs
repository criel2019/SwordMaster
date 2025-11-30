using UnityEngine;

public class PlayerInputBrain : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private ArcadePhysicsMover mover;
	[SerializeField] private Camera mainCam;

	// [핵심] 기능별 모듈 분리
	[SerializeField] private SwordMaster swordMaster;       // 검술 (발도/베기/검기)
	[SerializeField] private DivineBeastBase currentBeast;  // 현재 장착된 신수 (모듈)

	private Plane _groundPlane = new Plane(Vector3.up, Vector3.zero);

	// 좌클릭 분기 처리를 위한 타이머
	private float _mouseDownTime;
	private const float TAP_THRESHOLD = 0.2f; // 이 시간보다 짧으면 탭(베기), 길면 홀드(검기)

	private void Awake()
	{
		if (!mover) mover = GetComponent<ArcadePhysicsMover>();

		// 컴포넌트 자동 할당 (없으면 에러 방지용 null 체크 필요)
		if (!swordMaster) swordMaster = GetComponent<SwordMaster>();

		// 신수는 교체 가능하므로 GetComponent로 가져오거나, 외부에서 할당받음
		if (!currentBeast) currentBeast = GetComponent<DivineBeastBase>();

		if (!mainCam)
		{
			var camObj = GameObject.FindGameObjectWithTag("MainCamera");
			if (camObj) mainCam = camObj.GetComponent<Camera>();
			else mainCam = Camera.main;
		}
	}

	private void Update()
	{
		// 1. 이동 및 시선 처리 (기존 로직 유지)
		HandleMovement();
		Vector3 mousePos = GetMouseWorldPosition();

		// 2. 검술 입력 처리 (스페이스바 & 좌클릭)
		if (swordMaster != null)
		{
			HandleSwordInput(mousePos);
		}

		// 3. 신수 입력 처리 (Q키)
		// 신수가 무엇이든 상관없이 입력 신호만 전달 (인터페이스 패턴)
		if (currentBeast != null)
		{
			HandleBeastInput(mousePos);
		}
	}

	private void HandleMovement()
	{
		float x = Input.GetAxisRaw("Horizontal");
		float y = Input.GetAxisRaw("Vertical");
		mover.SetMoveInput(new Vector2(x, y).normalized);

		if (Input.GetKeyDown(KeyCode.LeftShift))
		{
			mover.Dash();
		}
	}

	private Vector3 GetMouseWorldPosition()
	{
		Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
		if (_groundPlane.Raycast(ray, out float enter))
		{
			Vector3 hitPoint = ray.GetPoint(enter);
			mover.SetLookPosition(hitPoint); // 캐릭터 회전 동기화
			return hitPoint;
		}
		return transform.position + transform.forward;
	}

	private void HandleSwordInput(Vector3 mousePos)
	{
		// [Space]: 순간이동 발도 (기존 기능에서 이동+공격만 남김)
		if (Input.GetKeyDown(KeyCode.Space))
		{
			swordMaster.PerformFlashCut(mousePos);
		}

		// [Left Click]: 기본 공격(Tap) 및 검기(Hold)
		if (Input.GetMouseButtonDown(0))
		{
			_mouseDownTime = Time.time;
			swordMaster.StartCharge(); // 차징 시작 (이펙트 등)
		}

		if (Input.GetMouseButton(0))
		{
			// 누르고 있는 동안 차징 상태 업데이트
			swordMaster.UpdateCharge(Time.time - _mouseDownTime);
		}

		if (Input.GetMouseButtonUp(0))
		{
			float duration = Time.time - _mouseDownTime;

			if (duration < TAP_THRESHOLD)
			{
				// 짧게 클릭 -> 제자리 베기 (Slash)
				swordMaster.PerformSlash(mousePos);
			}
			else
			{
				// 길게 클릭 -> 검기 발사 (Wave)
				swordMaster.FireWave(mousePos, duration);
			}
		}
	}

	private void HandleBeastInput(Vector3 mousePos)
	{
		// 입력 상태만 전달하고 구체적인 동작은 신수 모듈에 위임
		if (Input.GetKeyDown(KeyCode.Q))
		{
			currentBeast.OnInputDown();
		}

		if (Input.GetKey(KeyCode.Q))
		{
			currentBeast.OnInputHold(mousePos);
		}

		if (Input.GetKeyUp(KeyCode.Q))
		{
			currentBeast.OnInputUp(mousePos);
		}
	}

	// 런타임에 신수 교체 (아이템 획득 등)
	public void SwapBeast(DivineBeastBase newBeast)
	{
		if (currentBeast != null) currentBeast.Deactivate();
		currentBeast = newBeast;
		if (currentBeast != null) currentBeast.Activate();
	}
}