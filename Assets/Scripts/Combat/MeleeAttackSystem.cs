using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CombatController))]
public sealed class MeleeAttackSystem : MonoBehaviour
{
    [Serializable]
    public struct AttackTimingData
    {
        [Tooltip("Normalized time when HitBox activates.")]
        public float swingStartTime;
        [Tooltip("Normalized time when HitBox deactivates and combo window opens.")]
        public float swingEndTime;
        [Tooltip("Normalized time when the attack is fully complete.")]
        public float animationEndTime;
    }

    private const string CombatLayerName = "CombatLayer";
    private const string AttackTriggerParameter = "AttackTrigger";
    private const string ComboStepParameter = "ComboStep";
    private const string InCombatParameter = "InCombat";

    [Header("References")]
    [SerializeField] private CombatController combatController;
    [SerializeField] private Animator animator;
    [SerializeField] private HitBox hitBox;
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private PlayerMovementController movementController;
    [SerializeField] private CharacterController characterController;

    [Header("Tuning")]
    [SerializeField] private float attackLungeForce = 2f;
    [SerializeField] private float combatLayerBlendSpeed = 10f;
    [SerializeField] private AttackTimingData[] comboTimings =
    {
        new AttackTimingData { swingStartTime = 0.22f, swingEndTime = 0.56f, animationEndTime = 0.90f },
        new AttackTimingData { swingStartTime = 0.20f, swingEndTime = 0.56f, animationEndTime = 0.88f },
        new AttackTimingData { swingStartTime = 0.26f, swingEndTime = 0.62f, animationEndTime = 0.92f }
    };

    private int currentComboStep;
    private float comboWindowTimer;
    private bool inputBuffered;

    private bool _isAttackStepActive;
    private bool _isComboWindowOpen;
    private bool _didSwingStart;
    private bool _didSwingEnd;
    private bool _didAnimationEnd;
    private bool _isDamageWindowActive;
    private bool _warnedMissingSetup;

    private int _combatLayerIndex = -1;
    private float _targetCombatLayerWeight;

    private static readonly int AttackTriggerHash = Animator.StringToHash(AttackTriggerParameter);
    private static readonly int ComboStepHash = Animator.StringToHash(ComboStepParameter);
    private static readonly int InCombatHash = Animator.StringToHash(InCombatParameter);
    private static readonly int Attack01StateHash = Animator.StringToHash("Attack_01");
    private static readonly int Attack02StateHash = Animator.StringToHash("Attack_02");
    private static readonly int Attack03StateHash = Animator.StringToHash("Attack_03");

    public event Action<int> OnComboStepChanged;
    public event Action OnComboReset;

    public int CurrentComboStep => currentComboStep;

    public void SetReferences(
        CombatController combatControllerReference,
        Animator animatorReference,
        HitBox hitBoxReference,
        WeaponData weaponDataReference,
        PlayerMovementController movementControllerReference,
        CharacterController characterControllerReference)
    {
        if (combatControllerReference != null)
        {
            combatController = combatControllerReference;
        }

        if (animatorReference != null)
        {
            animator = animatorReference;
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

        if (characterControllerReference != null)
        {
            characterController = characterControllerReference;
        }

        ResolveReferences();
        ResolveCombatLayerIndex();
    }

    public void RequestAttack()
    {
        ResolveReferences();
        ResolveCombatLayerIndex();
        if (!CanHandleAttackRequest())
        {
            return;
        }

        if (_isAttackStepActive && !_isComboWindowOpen)
        {
            inputBuffered = true;
            return;
        }

        StartNextAttackStep();
    }

    public void ForceResetCombo()
    {
        ResetComboToIdle(emitLog: true, invokeEvent: true);
    }

    private void Reset()
    {
        ResolveReferences();
        comboTimings = BuildDefaultTimingData();
    }

    private void Awake()
    {
        ResolveReferences();
        ResolveCombatLayerIndex();
        if (hitBox != null)
        {
            hitBox.DisableHitBox();
        }
    }

    private void OnEnable()
    {
        ResolveCombatLayerIndex();
        _targetCombatLayerWeight = 0f;
        if (animator != null)
        {
            animator.SetBool(InCombatHash, false);
            animator.SetInteger(ComboStepHash, 0);
        }
    }

    private void OnDisable()
    {
        ResetComboToIdle(emitLog: false, invokeEvent: false);
    }

    private void Update()
    {
        ResolveReferences();
        UpdateCombatLayerWeight();

        if (comboWindowTimer > 0f)
        {
            comboWindowTimer = Mathf.Max(0f, comboWindowTimer - Time.deltaTime);
            if (comboWindowTimer <= 0f && !inputBuffered)
            {
                ResetComboToIdle(emitLog: true, invokeEvent: true);
                return;
            }
        }

        if (_isAttackStepActive)
        {
            UpdateAttackTiming();
        }

        if (_isDamageWindowActive)
        {
            ApplyForwardLunge();
        }
    }

    private void OnValidate()
    {
        attackLungeForce = Mathf.Max(0f, attackLungeForce);
        combatLayerBlendSpeed = Mathf.Max(0.01f, combatLayerBlendSpeed);

        if (comboTimings == null || comboTimings.Length != 3)
        {
            comboTimings = BuildDefaultTimingData();
        }

        for (int index = 0; index < comboTimings.Length; index++)
        {
            AttackTimingData timing = comboTimings[index];
            timing.swingStartTime = Mathf.Clamp01(timing.swingStartTime);
            timing.swingEndTime = Mathf.Clamp(timing.swingEndTime, timing.swingStartTime, 1f);
            timing.animationEndTime = Mathf.Clamp(timing.animationEndTime, timing.swingEndTime, 1.1f);
            comboTimings[index] = timing;
        }
    }

    private void ResolveReferences()
    {
        if (combatController == null)
        {
            combatController = GetComponent<CombatController>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (hitBox == null)
        {
            hitBox = GetComponentInChildren<HitBox>(includeInactive: true);
        }

        if (weaponData == null && hitBox != null)
        {
            weaponData = hitBox.EquippedWeaponData;
        }

        if (movementController == null)
        {
            movementController = GetComponent<PlayerMovementController>();
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }
    }

    private void ResolveCombatLayerIndex()
    {
        if (animator == null)
        {
            _combatLayerIndex = -1;
            return;
        }

        _combatLayerIndex = animator.GetLayerIndex(CombatLayerName);
    }

    private bool CanHandleAttackRequest()
    {
        if (combatController == null || animator == null || hitBox == null)
        {
            if (!_warnedMissingSetup)
            {
                Debug.LogWarning("MeleeAttackSystem is missing required references.", this);
                _warnedMissingSetup = true;
            }

            return false;
        }

        _warnedMissingSetup = false;

        CombatState state = combatController.CurrentState;
        if (state == CombatState.Parrying
            || state == CombatState.StealthKill
            || state == CombatState.Stunned
            || state == CombatState.Dead)
        {
            return false;
        }

        if (state == CombatState.Idle)
        {
            return true;
        }

        if (state == CombatState.Attacking)
        {
            return _isComboWindowOpen || (_isAttackStepActive && !_isComboWindowOpen);
        }

        return false;
    }

    private void StartNextAttackStep()
    {
        int maxSteps = comboTimings != null && comboTimings.Length > 0 ? comboTimings.Length : 3;
        int nextStep = currentComboStep <= 0 ? 1 : currentComboStep + 1;
        if (nextStep > maxSteps)
        {
            nextStep = 1;
        }

        currentComboStep = nextStep;
        comboWindowTimer = 0f;
        inputBuffered = false;
        _isComboWindowOpen = false;
        _isAttackStepActive = true;
        _didSwingStart = false;
        _didSwingEnd = false;
        _didAnimationEnd = false;
        _isDamageWindowActive = false;

        combatController.SetState(CombatState.Attacking);
        if (combatController.CurrentState != CombatState.Attacking)
        {
            ResetComboToIdle(emitLog: false, invokeEvent: false);
            return;
        }

        if (movementController != null)
        {
            movementController.SetMovementLocked(true);
        }

        if (hitBox != null)
        {
            hitBox.DisableHitBox();
        }

        animator.SetInteger(ComboStepHash, currentComboStep);
        animator.SetBool(InCombatHash, true);
        animator.SetTrigger(AttackTriggerHash);
        _targetCombatLayerWeight = 1f;

        OnComboStepChanged?.Invoke(currentComboStep);
        Debug.Log($"Attack {currentComboStep} started", this);
    }

    private void UpdateAttackTiming()
    {
        if (_combatLayerIndex < 0)
        {
            return;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(_combatLayerIndex);
        int expectedStateHash = GetCurrentAttackStateHash();
        if (stateInfo.shortNameHash != expectedStateHash)
        {
            return;
        }

        AttackTimingData timing = GetTimingDataForCurrentStep();
        float normalizedTime = stateInfo.normalizedTime;

        if (!_didSwingStart && normalizedTime >= timing.swingStartTime)
        {
            OnAttackSwingStart();
        }

        if (!_didSwingEnd && normalizedTime >= timing.swingEndTime)
        {
            OnAttackSwingEnd();
        }

        if (!_didAnimationEnd && normalizedTime >= timing.animationEndTime)
        {
            OnAttackAnimationEnd();
        }
    }

    private void OnAttackSwingStart()
    {
        _didSwingStart = true;
        _isDamageWindowActive = true;
        if (hitBox != null)
        {
            hitBox.EnableHitBox();
        }

        Debug.Log("HitBox ON", this);
    }

    private void OnAttackSwingEnd()
    {
        _didSwingEnd = true;
        _isDamageWindowActive = false;
        _isComboWindowOpen = true;
        comboWindowTimer = ResolveComboResetDuration();

        if (hitBox != null)
        {
            hitBox.DisableHitBox();
        }

        Debug.Log("HitBox OFF", this);
        Debug.Log("Combo window open", this);

        if (inputBuffered)
        {
            inputBuffered = false;
            StartNextAttackStep();
        }
    }

    private void OnAttackAnimationEnd()
    {
        _didAnimationEnd = true;
        _isAttackStepActive = false;
        _isDamageWindowActive = false;

        if (hitBox != null)
        {
            hitBox.DisableHitBox();
        }

        if (inputBuffered)
        {
            inputBuffered = false;
            StartNextAttackStep();
            return;
        }

        ResetComboToIdle(emitLog: true, invokeEvent: true);
    }

    private void ApplyForwardLunge()
    {
        if (characterController == null || attackLungeForce <= 0f)
        {
            return;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        forward.Normalize();
        characterController.Move(forward * (attackLungeForce * Time.deltaTime));
    }

    private void UpdateCombatLayerWeight()
    {
        if (animator == null || _combatLayerIndex < 0)
        {
            return;
        }

        float currentWeight = animator.GetLayerWeight(_combatLayerIndex);
        float nextWeight = Mathf.MoveTowards(
            currentWeight,
            _targetCombatLayerWeight,
            combatLayerBlendSpeed * Time.deltaTime);

        animator.SetLayerWeight(_combatLayerIndex, nextWeight);
    }

    private void ResetComboToIdle(bool emitLog, bool invokeEvent)
    {
        bool hadActiveCombo = currentComboStep != 0
            || comboWindowTimer > 0f
            || _isAttackStepActive
            || _isComboWindowOpen
            || inputBuffered;

        currentComboStep = 0;
        comboWindowTimer = 0f;
        inputBuffered = false;
        _isAttackStepActive = false;
        _isComboWindowOpen = false;
        _didSwingStart = false;
        _didSwingEnd = false;
        _didAnimationEnd = false;
        _isDamageWindowActive = false;
        _targetCombatLayerWeight = 0f;

        if (hitBox != null)
        {
            hitBox.DisableHitBox();
        }

        if (animator != null)
        {
            animator.SetInteger(ComboStepHash, 0);
            animator.SetBool(InCombatHash, false);
        }

        if (combatController != null && combatController.CurrentState != CombatState.Dead)
        {
            combatController.SetState(CombatState.Idle);
        }

        if (movementController != null && (combatController == null || combatController.CurrentState != CombatState.Dead))
        {
            movementController.SetMovementLocked(false);
        }

        if (!hadActiveCombo)
        {
            return;
        }

        if (invokeEvent)
        {
            OnComboReset?.Invoke();
        }

        if (emitLog)
        {
            Debug.Log("Combo reset", this);
        }
    }

    private AttackTimingData GetTimingDataForCurrentStep()
    {
        if (comboTimings == null || comboTimings.Length == 0)
        {
            comboTimings = BuildDefaultTimingData();
        }

        int index = Mathf.Clamp(currentComboStep - 1, 0, comboTimings.Length - 1);
        return comboTimings[index];
    }

    private int GetCurrentAttackStateHash()
    {
        return currentComboStep switch
        {
            1 => Attack01StateHash,
            2 => Attack02StateHash,
            3 => Attack03StateHash,
            _ => Attack01StateHash
        };
    }

    private float ResolveComboResetDuration()
    {
        if (weaponData != null)
        {
            return Mathf.Max(0.01f, weaponData.comboResetTime);
        }

        return 0.7f;
    }

    private static AttackTimingData[] BuildDefaultTimingData()
    {
        return new[]
        {
            new AttackTimingData { swingStartTime = 0.22f, swingEndTime = 0.56f, animationEndTime = 0.90f },
            new AttackTimingData { swingStartTime = 0.20f, swingEndTime = 0.56f, animationEndTime = 0.88f },
            new AttackTimingData { swingStartTime = 0.26f, swingEndTime = 0.62f, animationEndTime = 0.92f }
        };
    }
}
