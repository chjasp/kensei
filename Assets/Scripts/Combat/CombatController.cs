using UnityEngine;

public enum CombatState
{
    Idle,
    Attacking,
    Parrying,
    StealthKill,
    Stunned,
    Dead
}

[DisallowMultipleComponent]
public sealed class CombatController : MonoBehaviour
{
    private const string ParryTriggerParameter = "Parry";
    private const string ParryHoldParameter = "ParryHeld";

    [Header("References")]
    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private HitBox hitBox;
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private PlayerMovementController movementController;
    [SerializeField] private Animator animator;
    [SerializeField] private MeleeAttackSystem meleeAttackSystem;
    [SerializeField] private AudioSource audioSource;

    [Header("Parry")]
    [SerializeField] private float parryCooldownDuration = 1f;
    [SerializeField] private AudioClip parryClangClip;
    [SerializeField] private bool enableParryStagger = true;
    [SerializeField] private float parryStaggerDuration = 0.5f;

    private CombatState _currentState = CombatState.Idle;
    private float _lastAttackStartTime = -1f;
    private float _nextParryAllowedTime = -1f;
    private bool _isParryHeld;

    private static readonly int ParryTriggerHash = Animator.StringToHash(ParryTriggerParameter);
    private static readonly int ParryHoldHash = Animator.StringToHash(ParryHoldParameter);

    public CombatState CurrentState => _currentState;
    public float LastAttackStartTime => _lastAttackStartTime;
    public bool IsParryWindowActive => _isParryHeld;

    public void SetReferences(
        HealthSystem healthSystemReference,
        HitBox hitBoxReference,
        WeaponData weaponDataReference,
        PlayerMovementController movementControllerReference,
        Animator animatorReference)
    {
        if (healthSystemReference != null)
        {
            healthSystem = healthSystemReference;
        }

        if (hitBoxReference != null)
        {
            hitBox = hitBoxReference;
        }

        if (weaponDataReference != null)
        {
            weaponData = weaponDataReference;
        }

        if (movementControllerReference != null)
        {
            movementController = movementControllerReference;
        }

        if (animatorReference != null)
        {
            animator = animatorReference;
        }

        ResolveReferences();
        ApplyWeaponData();
        ApplyStateEffects();
    }

    public void ResetCombat()
    {
        meleeAttackSystem?.ForceResetCombo();

        if (hitBox != null)
        {
            hitBox.DisableHitBox();
        }

        if (_currentState == CombatState.Dead)
        {
            ApplyStateEffects();
            return;
        }

        SetState(CombatState.Idle);
    }

    public void RequestParry()
    {
        ResolveReferences();
        if (_currentState != CombatState.Idle || Time.time < _nextParryAllowedTime)
        {
            return;
        }

        SetState(CombatState.Parrying);
        if (_currentState != CombatState.Parrying)
        {
            return;
        }

        _isParryHeld = true;
        SetAnimatorParryHold(true);

        if (animator != null && HasAnimatorParameter(animator, ParryTriggerHash, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(ParryTriggerHash);
        }
    }

    public void ReleaseParry()
    {
        if (!_isParryHeld)
        {
            return;
        }

        _isParryHeld = false;
        SetAnimatorParryHold(false);
        _nextParryAllowedTime = Time.time + parryCooldownDuration;

        if (_currentState == CombatState.Parrying)
        {
            SetState(CombatState.Idle);
        }
    }

    public bool TryConsumeIncomingDamage(DamageInfo damage)
    {
        if (_currentState != CombatState.Parrying
            || !_isParryHeld
            || !IsParryDamageType(damage.Type))
        {
            return false;
        }

        if (damage.Source != null && damage.Source.transform.IsChildOf(transform))
        {
            return false;
        }

        if (audioSource != null && parryClangClip != null)
        {
            audioSource.PlayOneShot(parryClangClip);
        }

        if (enableParryStagger && parryStaggerDuration > 0f && damage.Source != null)
        {
            DummyEnemyBehavior enemy = damage.Source.GetComponentInParent<DummyEnemyBehavior>();
            enemy?.ApplyParryStagger(parryStaggerDuration);
        }

        return true;
    }

    public void SetState(CombatState newState)
    {
        if (_currentState == newState)
        {
            return;
        }

        if (_currentState == CombatState.Dead && newState != CombatState.Dead)
        {
            return;
        }

        if (newState == CombatState.Attacking && IsAttackStateBlocked(_currentState))
        {
            return;
        }

        CombatState previousState = _currentState;
        _currentState = newState;
        if (previousState == CombatState.Parrying && _currentState != CombatState.Parrying)
        {
            ClearParryRuntimeState();
        }

        if (_currentState == CombatState.Attacking)
        {
            _lastAttackStartTime = Time.time;
        }

        ApplyStateEffects();
    }

    private void Reset()
    {
        ResolveReferences();
        ApplyWeaponData();
        ApplyStateEffects();
    }

    private void Awake()
    {
        ResolveReferences();
        ApplyWeaponData();
        ApplyStateEffects();
    }

    private void OnEnable()
    {
        if (healthSystem != null)
        {
            healthSystem.OnDied += HandleOwnerDied;
        }
    }

    private void OnDisable()
    {
        if (healthSystem != null)
        {
            healthSystem.OnDied -= HandleOwnerDied;
        }

        ClearParryRuntimeState();
    }

    private void Update()
    {
        if (_currentState == CombatState.Dead)
        {
            return;
        }
    }

    private void HandleOwnerDied()
    {
        SetState(CombatState.Dead);
    }

    private void ResolveReferences()
    {
        if (healthSystem == null)
        {
            healthSystem = GetComponent<HealthSystem>();
        }

        if (movementController == null)
        {
            movementController = GetComponent<PlayerMovementController>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (hitBox == null)
        {
            hitBox = GetComponentInChildren<HitBox>(includeInactive: true);
        }

        if (meleeAttackSystem == null)
        {
            meleeAttackSystem = GetComponent<MeleeAttackSystem>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (hitBox != null)
        {
            hitBox.SetSourceRoot(transform);
        }
    }

    private void ApplyWeaponData()
    {
        if (hitBox != null)
        {
            hitBox.SetWeaponData(weaponData);
        }
    }

    private void ApplyStateEffects()
    {
        bool movementShouldBeLocked = _currentState == CombatState.Attacking
            || _currentState == CombatState.Parrying
            || _currentState == CombatState.StealthKill
            || _currentState == CombatState.Stunned
            || _currentState == CombatState.Dead;

        if (movementController != null)
        {
            movementController.SetMovementLocked(movementShouldBeLocked);
        }

        if (_currentState != CombatState.Attacking && hitBox != null)
        {
            hitBox.DisableHitBox();
        }
    }

    private void OnValidate()
    {
        parryCooldownDuration = Mathf.Max(0f, parryCooldownDuration);
        parryStaggerDuration = Mathf.Max(0f, parryStaggerDuration);

        if (Application.isPlaying)
        {
            return;
        }

        ResolveReferences();
        ApplyWeaponData();
    }

    private static bool IsAttackStateBlocked(CombatState state)
    {
        return state == CombatState.Parrying
            || state == CombatState.StealthKill
            || state == CombatState.Stunned
            || state == CombatState.Dead;
    }

    private void ClearParryRuntimeState()
    {
        _isParryHeld = false;
        SetAnimatorParryHold(false);
    }

    private void SetAnimatorParryHold(bool isHeld)
    {
        if (animator == null || !HasAnimatorParameter(animator, ParryHoldHash, AnimatorControllerParameterType.Bool))
        {
            return;
        }

        animator.SetBool(ParryHoldHash, isHeld);
    }

    private static bool HasAnimatorParameter(
        Animator targetAnimator,
        int parameterNameHash,
        AnimatorControllerParameterType parameterType)
    {
        if (targetAnimator == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = targetAnimator.parameters;
        for (int index = 0; index < parameters.Length; index++)
        {
            AnimatorControllerParameter parameter = parameters[index];
            if (parameter.type == parameterType
                && parameter.nameHash == parameterNameHash)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsParryDamageType(DamageType damageType)
    {
        return damageType == DamageType.LightMelee || damageType == DamageType.HeavyMelee;
    }
}
