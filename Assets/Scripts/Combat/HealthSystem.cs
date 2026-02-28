using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HealthSystem : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float invincibilityDuration = 0.2f;

    private float _currentHealth;
    private float _invincibilityTimer;
    private bool _hasInvokedDeath;

    public event Action<DamageInfo> OnDamaged;
    public event Action<float> OnHealed;
    public event Action OnDied;
    public event Action<float, float> OnHealthChanged;

    public float CurrentHealth => _currentHealth;
    public bool IsAlive => _currentHealth > 0f;

    private void Awake()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        _currentHealth = maxHealth;
    }

    private void Update()
    {
        if (_invincibilityTimer <= 0f)
        {
            return;
        }

        _invincibilityTimer = Mathf.Max(0f, _invincibilityTimer - Time.deltaTime);
    }

    public void TakeDamage(DamageInfo damage)
    {
        if (!IsAlive || damage.Amount <= 0f || _invincibilityTimer > 0f)
        {
            return;
        }

        float previousHealth = _currentHealth;
        _currentHealth = Mathf.Max(0f, _currentHealth - damage.Amount);
        _invincibilityTimer = invincibilityDuration;

        if (_currentHealth < previousHealth)
        {
            OnDamaged?.Invoke(damage);
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        }

        if (_currentHealth <= 0f && !_hasInvokedDeath)
        {
            _hasInvokedDeath = true;
            OnDied?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (!IsAlive || amount <= 0f)
        {
            return;
        }

        float previousHealth = _currentHealth;
        _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);

        float actualHealedAmount = _currentHealth - previousHealth;
        if (actualHealedAmount <= 0f)
        {
            return;
        }

        OnHealed?.Invoke(actualHealedAmount);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        invincibilityDuration = Mathf.Max(0f, invincibilityDuration);
    }
}
