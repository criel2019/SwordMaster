using UnityEngine;

public class PlayerInputBrain : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private ArcadePhysicsMover mover;
	[SerializeField] private Camera mainCam;
	[SerializeField] private SwordMaster swordMaster;
	[SerializeField] private SkillManager skillManager;
	[SerializeField] private DivineBeastBase currentBeast;

	private Plane _groundPlane = new Plane(Vector3.up, Vector3.zero);
	private float _mouseDownTime;
	private const float TAP_THRESHOLD = 0.2f;

	private void Awake()
	{
		if (!mover) mover = GetComponent<ArcadePhysicsMover>();
		if (!swordMaster) swordMaster = GetComponent<SwordMaster>();
		if (!skillManager) skillManager = GetComponent<SkillManager>();
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
		Vector3 mousePos = GetMouseWorldPosition();

		if (IsAnySkillExecuting())
		{
			return;
		}

		HandleMovement();
		HandleSkillInput(mousePos);

		if (swordMaster != null)
		{
			HandleSwordInput(mousePos);
		}

		if (currentBeast != null)
		{
			HandleBeastInput(mousePos);
		}
	}

	private bool IsAnySkillExecuting()
	{
		if (skillManager == null) return false;

		var flashCut = skillManager.GetSkill("flash_cut");
		if (flashCut != null && flashCut.IsExecuting) return true;

		var flashCutChain = skillManager.GetSkill("flash_cut_chain");
		if (flashCutChain != null && flashCutChain.IsExecuting) return true;

		return false;
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

			if (!IsAnySkillExecuting())
			{
				mover.SetLookPosition(hitPoint);
			}

			return hitPoint;
		}
		return transform.position + transform.forward;
	}

	private void HandleSkillInput(Vector3 mousePos)
	{
		if (skillManager == null) return;

		// [Space]: 발검
		if (Input.GetKeyDown(KeyCode.Space))
		{
			skillManager.TryExecute("flash_cut", mousePos);
		}

		// [1]: 발검 연격
		if (Input.GetKeyDown(KeyCode.Alpha1))
		{
			skillManager.TryExecute("flash_cut_chain", mousePos);
		}
	}

	private void HandleSwordInput(Vector3 mousePos)
	{
		// [Left Click]: 기본 공격(Tap) 및 검기(Hold)
		if (Input.GetMouseButtonDown(0))
		{
			_mouseDownTime = Time.time;
			swordMaster.StartCharge();
		}

		if (Input.GetMouseButton(0))
		{
			swordMaster.UpdateCharge(Time.time - _mouseDownTime);
		}

		if (Input.GetMouseButtonUp(0))
		{
			float duration = Time.time - _mouseDownTime;

			if (duration < TAP_THRESHOLD)
			{
				swordMaster.PerformSlash(mousePos);
			}
			else
			{
				swordMaster.FireWave(mousePos, duration);
			}
		}
	}

	private void HandleBeastInput(Vector3 mousePos)
	{
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

	public void SwapBeast(DivineBeastBase newBeast)
	{
		if (currentBeast != null) currentBeast.Deactivate();
		currentBeast = newBeast;
		if (currentBeast != null) currentBeast.Activate();
	}
}