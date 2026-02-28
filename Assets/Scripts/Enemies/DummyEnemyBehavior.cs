using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DummyEnemyBehavior : MonoBehaviour
{
    private const string BaseColorPropertyName = "_BaseColor";
    private const string ColorPropertyName = "_Color";

    [Header("References")]
    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private Animator animator;
    [SerializeField] private Renderer[] flashRenderers;

    [Header("Animation")]
    [SerializeField] private string hitReactTriggerName = "HitReact";
    [SerializeField] private string deathTriggerName = "Die";
    [SerializeField] private float deathDisableDelay = 0f;

    [Header("Fallback Hit Flash")]
    [SerializeField] private float hitFlashDuration = 0.1f;
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.25f, 0.25f, 1f);

    private readonly List<MaterialColorBinding> _materialBindings = new List<MaterialColorBinding>();

    private float _currentHealth;
    private float _maxHealth;
    private float _hitFlashTimer;
    private float _deathDisableTimer = -1f;
    private bool _flashApplied;
    private bool _hasHitReactTrigger;
    private bool _hasDeathTrigger;
    private bool _isDead;

    private readonly int _baseColorPropertyId = Shader.PropertyToID(BaseColorPropertyName);
    private readonly int _colorPropertyId = Shader.PropertyToID(ColorPropertyName);

    public void SetReferences(
        HealthSystem healthSystemReference,
        Animator animatorReference,
        Renderer[] flashRenderersReference = null)
    {
        if (healthSystemReference != null)
        {
            healthSystem = healthSystemReference;
        }

        if (animatorReference != null)
        {
            animator = animatorReference;
        }

        if (flashRenderersReference != null && flashRenderersReference.Length > 0)
        {
            flashRenderers = flashRenderersReference;
        }

        CacheAnimatorParameters();
        CacheRenderersAndColors();
        InitializeHealthCache();
    }

    private void Awake()
    {
        ResolveReferences();
        CacheAnimatorParameters();
        CacheRenderersAndColors();
        InitializeHealthCache();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeHealthEvents();
    }

    private void OnDisable()
    {
        UnsubscribeHealthEvents();
        RestoreFlashColors();
        _hitFlashTimer = 0f;
        _deathDisableTimer = -1f;
        _isDead = false;
    }

    private void Update()
    {
        if (_hitFlashTimer > 0f)
        {
            if (!_flashApplied)
            {
                ApplyFlashColor();
            }

            _hitFlashTimer = Mathf.Max(0f, _hitFlashTimer - Time.deltaTime);
            if (_hitFlashTimer <= 0f)
            {
                RestoreFlashColors();
            }
        }

        if (_deathDisableTimer > 0f)
        {
            _deathDisableTimer = Mathf.Max(0f, _deathDisableTimer - Time.deltaTime);
            if (_deathDisableTimer <= 0f)
            {
                gameObject.SetActive(false);
            }
        }
    }

    private void OnValidate()
    {
        hitFlashDuration = Mathf.Max(0f, hitFlashDuration);
        deathDisableDelay = Mathf.Max(0f, deathDisableDelay);
    }

    private void HandleDamaged(DamageInfo damageInfo)
    {
        _currentHealth = healthSystem != null ? healthSystem.CurrentHealth : _currentHealth;
        Debug.Log(
            $"Enemy took {damageInfo.Amount:0.##} damage, HP: {_currentHealth:0.##}/{_maxHealth:0.##}",
            this);

        if (animator != null && _hasHitReactTrigger)
        {
            animator.SetTrigger(hitReactTriggerName);
            return;
        }

        BeginHitFlash();
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        _currentHealth = currentHealth;
        _maxHealth = maxHealth;
    }

    private void HandleDied()
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;
        Debug.Log("Enemy died", this);

        if (animator != null && _hasDeathTrigger && deathDisableDelay > 0f)
        {
            animator.SetTrigger(deathTriggerName);
            _deathDisableTimer = deathDisableDelay;
            return;
        }

        gameObject.SetActive(false);
    }

    private void BeginHitFlash()
    {
        if (hitFlashDuration <= 0f || _materialBindings.Count == 0)
        {
            return;
        }

        _hitFlashTimer = hitFlashDuration;
        _flashApplied = false;
    }

    private void ApplyFlashColor()
    {
        for (int index = 0; index < _materialBindings.Count; index++)
        {
            MaterialColorBinding binding = _materialBindings[index];
            if (binding.material == null)
            {
                continue;
            }

            binding.material.SetColor(binding.colorPropertyId, hitFlashColor);
        }

        _flashApplied = true;
    }

    private void RestoreFlashColors()
    {
        if (!_flashApplied)
        {
            return;
        }

        for (int index = 0; index < _materialBindings.Count; index++)
        {
            MaterialColorBinding binding = _materialBindings[index];
            if (binding.material == null)
            {
                continue;
            }

            binding.material.SetColor(binding.colorPropertyId, binding.originalColor);
        }

        _flashApplied = false;
    }

    private void ResolveReferences()
    {
        if (healthSystem == null)
        {
            healthSystem = GetComponent<HealthSystem>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(includeInactive: true);
        }

        if (flashRenderers == null || flashRenderers.Length == 0)
        {
            flashRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        }
    }

    private void SubscribeHealthEvents()
    {
        if (healthSystem == null)
        {
            return;
        }

        healthSystem.OnDamaged -= HandleDamaged;
        healthSystem.OnHealthChanged -= HandleHealthChanged;
        healthSystem.OnDied -= HandleDied;

        healthSystem.OnDamaged += HandleDamaged;
        healthSystem.OnHealthChanged += HandleHealthChanged;
        healthSystem.OnDied += HandleDied;
    }

    private void UnsubscribeHealthEvents()
    {
        if (healthSystem == null)
        {
            return;
        }

        healthSystem.OnDamaged -= HandleDamaged;
        healthSystem.OnHealthChanged -= HandleHealthChanged;
        healthSystem.OnDied -= HandleDied;
    }

    private void InitializeHealthCache()
    {
        if (healthSystem == null)
        {
            return;
        }

        _currentHealth = healthSystem.CurrentHealth;
        _maxHealth = Mathf.Max(_maxHealth, healthSystem.CurrentHealth);
    }

    private void CacheAnimatorParameters()
    {
        _hasHitReactTrigger = HasTriggerParameter(animator, hitReactTriggerName);
        _hasDeathTrigger = HasTriggerParameter(animator, deathTriggerName);
    }

    private void CacheRenderersAndColors()
    {
        _materialBindings.Clear();
        if (flashRenderers == null)
        {
            return;
        }

        for (int rendererIndex = 0; rendererIndex < flashRenderers.Length; rendererIndex++)
        {
            Renderer renderer = flashRenderers[rendererIndex];
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.materials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                int propertyId = ResolveColorPropertyId(material);
                if (propertyId == -1)
                {
                    continue;
                }

                MaterialColorBinding binding = new MaterialColorBinding
                {
                    material = material,
                    colorPropertyId = propertyId,
                    originalColor = material.GetColor(propertyId)
                };
                _materialBindings.Add(binding);
            }
        }
    }

    private int ResolveColorPropertyId(Material material)
    {
        if (material.HasProperty(_baseColorPropertyId))
        {
            return _baseColorPropertyId;
        }

        if (material.HasProperty(_colorPropertyId))
        {
            return _colorPropertyId;
        }

        return -1;
    }

    private static bool HasTriggerParameter(Animator targetAnimator, string parameterName)
    {
        if (targetAnimator == null || string.IsNullOrEmpty(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = targetAnimator.parameters;
        for (int index = 0; index < parameters.Length; index++)
        {
            AnimatorControllerParameter parameter = parameters[index];
            if (parameter.type == AnimatorControllerParameterType.Trigger &&
                parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    private struct MaterialColorBinding
    {
        public Material material;
        public int colorPropertyId;
        public Color originalColor;
    }
}
