using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 신수 교체 매니저 - 여러 신수를 전환하며 테스트할 수 있는 시스템
/// 숫자 키(1, 2, 3, 4)로 신수 교체
/// Q 키로 현재 활성화된 신수 조작
/// </summary>
public class DivineBeastManager : MonoBehaviour
{
	[Header("Divine Beasts")]
	[SerializeField] private List<DivineBeastBase> divineBeastList = new List<DivineBeastBase>();

	[Header("Input Settings")]
	[SerializeField] private KeyCode activateKey = KeyCode.Q;
	[SerializeField] private KeyCode switchKey1 = KeyCode.F1;
	[SerializeField] private KeyCode switchKey2 = KeyCode.F2;
	[SerializeField] private KeyCode switchKey3 = KeyCode.F3;
	[SerializeField] private KeyCode switchKey4 = KeyCode.F4;

	[Header("Player")]
	[SerializeField] private Transform playerTransform;

	// 내부 상태
	private int _currentBeastIndex = 0;
	private DivineBeastBase _currentBeast;
	private bool _isHoldingQ = false;
	private Camera _mainCamera;

	private void Start()
	{
		_mainCamera = Camera.main;

		// 플레이어 자동 찾기
		if (playerTransform == null)
		{
			var player = GameObject.FindGameObjectWithTag("Player");
			if (player) playerTransform = player.transform;
		}

		// 모든 신수 초기화
		foreach (var beast in divineBeastList)
		{
			if (beast != null)
			{
				beast.Initialize(playerTransform);
				beast.Deactivate();
			}
		}

		// 첫 번째 신수 활성화
		if (divineBeastList.Count > 0)
		{
			SwitchBeast(0);
		}
		else
		{
			Debug.LogWarning("[DivineBeastManager] 신수가 하나도 등록되지 않았습니다!");
		}
	}

	private void Update()
	{
		// 신수 전환 입력
		if (Input.GetKeyDown(switchKey1))
		{
			SwitchBeast(0);
		}
		if (Input.GetKeyDown(switchKey2))
		{
			SwitchBeast(1);
		}
		if (Input.GetKeyDown(switchKey3))
		{
			SwitchBeast(2);
		}
		if (Input.GetKeyDown(switchKey4))
		{
			SwitchBeast(3);
		}

		// 현재 신수 조작
		if (_currentBeast != null)
		{
			HandleBeastInput();
		}
	}

	private void SwitchBeast(int index)
	{
		if (index < 0 || index >= divineBeastList.Count)
		{
			return;
		}

		if (divineBeastList[index] == null)
		{
			return;
		}

		// 현재 신수 비활성화
		if (_currentBeast != null)
		{
			_currentBeast.Deactivate();
		}

		// 새 신수 활성화
		_currentBeastIndex = index;
		_currentBeast = divineBeastList[index];
		_currentBeast.Activate();

		// 입력 상태 리셋
		_isHoldingQ = false;
	}

	private void HandleBeastInput()
	{
		// Q 누름
		if (Input.GetKeyDown(activateKey))
		{
			_isHoldingQ = true;
			_currentBeast.OnInputDown();
		}

		// Q 홀드
		if (Input.GetKey(activateKey) && _isHoldingQ)
		{
			Vector3 targetPos = GetMouseWorldPosition();
			_currentBeast.OnInputHold(targetPos);
		}

		// Q 뗌
		if (Input.GetKeyUp(activateKey) && _isHoldingQ)
		{
			_isHoldingQ = false;
			Vector3 targetPos = GetMouseWorldPosition();
			_currentBeast.OnInputUp(targetPos);
		}
	}

	private Vector3 GetMouseWorldPosition()
	{
		if (_mainCamera == null || playerTransform == null)
			return Vector3.zero;

		// 마우스 위치를 월드 좌표로 변환
		Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
		Plane groundPlane = new Plane(Vector3.up, playerTransform.position);

		if (groundPlane.Raycast(ray, out float distance))
		{
			return ray.GetPoint(distance);
		}

		return playerTransform.position + playerTransform.forward * 5f;
	}

	private void OnGUI()
	{
		// UI 표시
		GUIStyle style = new GUIStyle();
		style.fontSize = 20;
		style.normal.textColor = Color.white;
		style.alignment = TextAnchor.UpperLeft;

		string currentName = _currentBeast != null ? _currentBeast.GetType().Name : "None";

		GUI.Label(new Rect(10, 10, 400, 30), $"현재 신수: {currentName}", style);
		GUI.Label(new Rect(10, 40, 400, 30), "F1: ThunderBird | F2: Phoenix | F3: BlackTortoise | F4: OrbBeast", style);
		GUI.Label(new Rect(10, 70, 400, 30), "Q: 신수 스킬 사용", style);
	}
}
