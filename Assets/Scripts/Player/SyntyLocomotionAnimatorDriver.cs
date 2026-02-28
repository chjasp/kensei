using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class SyntyLocomotionAnimatorDriver : MonoBehaviour
{
    private enum GaitState
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
        Sprint = 3
    }

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerMovementController movementController;
    [SerializeField] private Transform visualRoot;

    [Header("Input")]
    [SerializeField] private float inputThreshold = 0.03f;
    [SerializeField] private float holdThreshold = 0.15f;

    [Header("Locomotion")]
    [SerializeField] private float walkSpeed = 1.4f;
    [SerializeField] private float runSpeed = 2.5f;
    [SerializeField] private float sprintSpeed = 6.0f;
    [SerializeField] private float stopSpeedThreshold = 0.2f;
    [SerializeField] private float jumpVelocityThreshold = 0.1f;
    [SerializeField] private float startDuration = 0.2f;

    private float _movementInputDuration;
    private float _fallingDuration;
    private float _startingTimer;
    private float _locomotionStartDirection;
    private bool _wasMovementDetected;
    private bool _warnedMissingAnimator;
    private bool _warnedMissingCharacterController;

    private readonly int _movementInputTappedHash = Animator.StringToHash("MovementInputTapped");
    private readonly int _movementInputPressedHash = Animator.StringToHash("MovementInputPressed");
    private readonly int _movementInputHeldHash = Animator.StringToHash("MovementInputHeld");
    private readonly int _moveSpeedHash = Animator.StringToHash("MoveSpeed");
    private readonly int _currentGaitHash = Animator.StringToHash("CurrentGait");
    private readonly int _inclineAngleHash = Animator.StringToHash("InclineAngle");
    private readonly int _strafeDirectionXHash = Animator.StringToHash("StrafeDirectionX");
    private readonly int _strafeDirectionZHash = Animator.StringToHash("StrafeDirectionZ");
    private readonly int _shuffleDirectionXHash = Animator.StringToHash("ShuffleDirectionX");
    private readonly int _shuffleDirectionZHash = Animator.StringToHash("ShuffleDirectionZ");
    private readonly int _forwardStrafeHash = Animator.StringToHash("ForwardStrafe");
    private readonly int _cameraRotationOffsetHash = Animator.StringToHash("CameraRotationOffset");
    private readonly int _isStrafingHash = Animator.StringToHash("IsStrafing");
    private readonly int _isTurningInPlaceHash = Animator.StringToHash("IsTurningInPlace");
    private readonly int _isCrouchingHash = Animator.StringToHash("IsCrouching");
    private readonly int _isWalkingHash = Animator.StringToHash("IsWalking");
    private readonly int _isStoppedHash = Animator.StringToHash("IsStopped");
    private readonly int _isStartingHash = Animator.StringToHash("IsStarting");
    private readonly int _isJumpingHash = Animator.StringToHash("IsJumping");
    private readonly int _isGroundedHash = Animator.StringToHash("IsGrounded");
    private readonly int _fallingDurationHash = Animator.StringToHash("FallingDuration");
    private readonly int _leanValueHash = Animator.StringToHash("LeanValue");
    private readonly int _headLookXHash = Animator.StringToHash("HeadLookX");
    private readonly int _headLookYHash = Animator.StringToHash("HeadLookY");
    private readonly int _bodyLookXHash = Animator.StringToHash("BodyLookX");
    private readonly int _bodyLookYHash = Animator.StringToHash("BodyLookY");
    private readonly int _locomotionStartDirectionHash = Animator.StringToHash("LocomotionStartDirection");

    public void SetDependencies(
        Animator animatorReference,
        CharacterController characterControllerReference,
        Transform visualRootReference,
        PlayerMovementController movementControllerReference = null)
    {
        if (animatorReference != null)
        {
            animator = animatorReference;
        }

        if (characterControllerReference != null)
        {
            characterController = characterControllerReference;
        }

        if (visualRootReference != null)
        {
            visualRoot = visualRootReference;
        }

        if (movementControllerReference != null)
        {
            movementController = movementControllerReference;
        }

        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();
        if (animator == null)
        {
            if (!_warnedMissingAnimator)
            {
                Debug.LogWarning("SyntyLocomotionAnimatorDriver could not resolve an Animator.", this);
                _warnedMissingAnimator = true;
            }
            return;
        }
        _warnedMissingAnimator = false;

        if (characterController == null)
        {
            if (!_warnedMissingCharacterController)
            {
                Debug.LogWarning("SyntyLocomotionAnimatorDriver could not resolve a CharacterController.", this);
                _warnedMissingCharacterController = true;
            }
            return;
        }
        _warnedMissingCharacterController = false;

        Vector2 input = ResolveInput();
        Vector3 desiredMove = ResolveDesiredMove(input);
        Vector3 planarVelocity = ResolvePlanarVelocity();
        float moveSpeed = planarVelocity.magnitude;
        bool movementDetected = input.sqrMagnitude > (inputThreshold * inputThreshold);

        if (!movementDetected)
        {
            movementDetected = moveSpeed > stopSpeedThreshold;
        }

        UpdateInputState(movementDetected, desiredMove, out bool inputTapped, out bool inputPressed, out bool inputHeld);

        bool isGrounded = characterController.isGrounded;
        float verticalVelocity = ResolveVerticalVelocity();
        bool isJumping = !isGrounded && verticalVelocity > jumpVelocityThreshold;

        if (isGrounded)
        {
            _fallingDuration = 0f;
        }
        else if (verticalVelocity < 0f)
        {
            _fallingDuration += Time.deltaTime;
        }

        bool isStopped = !movementDetected && moveSpeed < stopSpeedThreshold;
        bool isStarting = UpdateStartingState(movementDetected);
        GaitState gait = ResolveGait(moveSpeed);

        float strafeDirectionX = 0f;
        float strafeDirectionZ = movementDetected ? 1f : 0f;
        float shuffleDirectionX = 0f;
        float shuffleDirectionZ = 1f;

        animator.SetBool(_movementInputTappedHash, inputTapped);
        animator.SetBool(_movementInputPressedHash, inputPressed);
        animator.SetBool(_movementInputHeldHash, inputHeld);
        animator.SetFloat(_moveSpeedHash, moveSpeed);
        animator.SetInteger(_currentGaitHash, (int)gait);
        animator.SetFloat(_inclineAngleHash, 0f);
        animator.SetFloat(_strafeDirectionXHash, strafeDirectionX);
        animator.SetFloat(_strafeDirectionZHash, strafeDirectionZ);
        animator.SetFloat(_shuffleDirectionXHash, shuffleDirectionX);
        animator.SetFloat(_shuffleDirectionZHash, shuffleDirectionZ);
        animator.SetFloat(_forwardStrafeHash, 1f);
        animator.SetFloat(_cameraRotationOffsetHash, 0f);
        animator.SetFloat(_isStrafingHash, 0f);
        animator.SetBool(_isTurningInPlaceHash, false);
        animator.SetBool(_isCrouchingHash, false);
        animator.SetBool(_isWalkingHash, gait == GaitState.Walk);
        animator.SetBool(_isStoppedHash, isStopped);
        animator.SetBool(_isStartingHash, isStarting);
        animator.SetBool(_isJumpingHash, isJumping);
        animator.SetBool(_isGroundedHash, isGrounded);
        animator.SetFloat(_fallingDurationHash, _fallingDuration);
        animator.SetFloat(_leanValueHash, 0f);
        animator.SetFloat(_headLookXHash, 0f);
        animator.SetFloat(_headLookYHash, 0f);
        animator.SetFloat(_bodyLookXHash, 0f);
        animator.SetFloat(_bodyLookYHash, 0f);
        animator.SetFloat(_locomotionStartDirectionHash, _locomotionStartDirection);
    }

    private void ResolveReferences()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (movementController == null)
        {
            movementController = GetComponent<PlayerMovementController>();
        }

        if (visualRoot == null && animator != null)
        {
            visualRoot = animator.transform;
        }

        if (animator == null)
        {
            if (visualRoot != null)
            {
                animator = visualRoot.GetComponent<Animator>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        if (visualRoot == null)
        {
            visualRoot = transform;
        }
    }

    private Vector2 ResolveInput()
    {
        if (movementController != null)
        {
            return movementController.CurrentInput;
        }

        Vector3 planarVelocity = ResolvePlanarVelocity();
        return new Vector2(planarVelocity.x, planarVelocity.z);
    }

    private Vector3 ResolveDesiredMove(Vector2 fallbackInput)
    {
        if (movementController != null)
        {
            return movementController.CurrentDesiredMove;
        }

        if (fallbackInput.sqrMagnitude > 0.0001f)
        {
            return new Vector3(fallbackInput.x, 0f, fallbackInput.y).normalized;
        }

        return Vector3.zero;
    }

    private Vector3 ResolvePlanarVelocity()
    {
        Vector3 planarVelocity;
        if (movementController != null)
        {
            planarVelocity = movementController.CurrentPlanarVelocity;
        }
        else
        {
            planarVelocity = characterController.velocity;
            planarVelocity.y = 0f;
        }

        planarVelocity.y = 0f;
        return planarVelocity;
    }

    private float ResolveVerticalVelocity()
    {
        if (movementController != null)
        {
            return movementController.CurrentVerticalVelocity;
        }

        return characterController.velocity.y;
    }

    private void UpdateInputState(
        bool movementDetected,
        Vector3 movementDirection,
        out bool inputTapped,
        out bool inputPressed,
        out bool inputHeld)
    {
        inputTapped = false;
        inputPressed = false;
        inputHeld = false;

        if (!movementDetected)
        {
            _movementInputDuration = 0f;
            _wasMovementDetected = false;
            return;
        }

        if (!_wasMovementDetected)
        {
            inputTapped = true;
            _movementInputDuration = 0f;
            _startingTimer = startDuration;

            if (movementDirection.sqrMagnitude > 0.0001f)
            {
                Vector3 facingForward = visualRoot != null ? visualRoot.forward : transform.forward;
                facingForward.y = 0f;
                if (facingForward.sqrMagnitude < 0.0001f)
                {
                    facingForward = Vector3.forward;
                }
                facingForward.Normalize();
                _locomotionStartDirection = Vector3.SignedAngle(facingForward, movementDirection.normalized, Vector3.up);
            }
        }
        else
        {
            _movementInputDuration += Time.deltaTime;
            if (_movementInputDuration < holdThreshold)
            {
                inputPressed = true;
            }
            else
            {
                inputHeld = true;
            }
        }

        _wasMovementDetected = true;
    }

    private bool UpdateStartingState(bool movementDetected)
    {
        if (!movementDetected)
        {
            _startingTimer = 0f;
            return false;
        }

        if (_startingTimer > 0f)
        {
            _startingTimer -= Time.deltaTime;
            return true;
        }

        return false;
    }

    private GaitState ResolveGait(float speed)
    {
        float runThreshold = (walkSpeed + runSpeed) * 0.5f;
        float sprintThreshold = (runSpeed + sprintSpeed) * 0.5f;

        if (speed < 0.01f)
        {
            return GaitState.Idle;
        }

        if (speed < runThreshold)
        {
            return GaitState.Walk;
        }

        if (speed < sprintThreshold)
        {
            return GaitState.Run;
        }

        return GaitState.Sprint;
    }

    private void OnValidate()
    {
        inputThreshold = Mathf.Clamp(inputThreshold, 0f, 1f);
        holdThreshold = Mathf.Max(0.01f, holdThreshold);
        walkSpeed = Mathf.Max(0.01f, walkSpeed);
        runSpeed = Mathf.Max(walkSpeed, runSpeed);
        sprintSpeed = Mathf.Max(runSpeed, sprintSpeed);
        stopSpeedThreshold = Mathf.Max(0.01f, stopSpeedThreshold);
        startDuration = Mathf.Max(0.01f, startDuration);
    }
}
