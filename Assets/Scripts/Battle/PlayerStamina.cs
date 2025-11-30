using UnityEngine;
using System;

public class PlayerStamina : MonoBehaviour
{
    [Header("Stamina Settings")]
    [SerializeField] private int maxStamina = 5;
    [SerializeField] private float regenInterval = 2f;  // 1개 회복에 걸리는 시간
    
    private int _currentStamina;
    private float _regenTimer;
    
    public int CurrentStamina => _currentStamina;
    public int MaxStamina => maxStamina;
    
    public event Action<int, int> OnStaminaChanged;  // current, max
    
    private void Awake()
    {
        _currentStamina = maxStamina;
    }
    
    private void Update()
    {
        if (_currentStamina < maxStamina)
        {
            _regenTimer += Time.deltaTime;
            if (_regenTimer >= regenInterval)
            {
                _regenTimer = 0f;
                AddStamina(1);
            }
        }
        else
        {
            _regenTimer = 0f;
        }
    }
    
    public bool HasEnough(int amount)
    {
        return _currentStamina >= amount;
    }
    
    public bool TryConsume(int amount)
    {
        if (_currentStamina < amount) return false;
        
        _currentStamina -= amount;
        OnStaminaChanged?.Invoke(_currentStamina, maxStamina);
        return true;
    }
    
    public void AddStamina(int amount)
    {
        _currentStamina = Mathf.Min(_currentStamina + amount, maxStamina);
        OnStaminaChanged?.Invoke(_currentStamina, maxStamina);
    }
    
    public void SetMax(int newMax)
    {
        maxStamina = newMax;
        _currentStamina = Mathf.Min(_currentStamina, maxStamina);
        OnStaminaChanged?.Invoke(_currentStamina, maxStamina);
    }
}