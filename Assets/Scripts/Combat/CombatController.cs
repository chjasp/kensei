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
    [Header("References")]
    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private HitBox hitBox;
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private PlayerMovementController movementController;
    [SerializeField] private Animator animator;
    [SerializeField] private MeleeAttackSystem meleeAttackSystem;

    private CombatState _currentState = CombatState.Idle;
    private float _lastAttackStartTime = -1f;

    public CombatState CurrentState => _currentState;
    public float LastAttackStartTime => _lastAttackStartTime;

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

        _currentState = newState;
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
    }

    private void Update()
    {
        if (_currentState == CombatState.Dead)
        {
            return;
        }

        // TODO: Prompt 2 - attack input and combo flow.
        // TODO: Prompt 3 - parry timing and state handling.
        // TODO: Prompt 4 - stealth takedown state handling.
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
}
