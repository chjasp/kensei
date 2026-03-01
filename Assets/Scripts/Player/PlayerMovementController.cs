using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerMovementController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private VirtualJoystick joystick;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform visualRoot;

    [Header("Input Fallback")]
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private InputActionAsset inputActions;
#endif
    [SerializeField] private string moveActionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";

    [Header("Movement")]
    [SerializeField] private float visualYawOffset = 0f;
    [SerializeField] private float maxSpeed = 6.75f;
    [SerializeField] private float acceleration = 26f;
    [SerializeField] private float deceleration = 34f;
    [SerializeField] private float turnSharpness = 16f;
    [SerializeField] private float gravity = -30f;
    [SerializeField] private float groundedVerticalVelocity = -2f;
    [SerializeField] private float inputThreshold = 0.03f;

    private CharacterController _characterController;
    private Vector3 _planarVelocity;
    private float _verticalVelocity;
    private Vector2 _currentInput;
    private Vector3 _currentDesiredMove;
    private bool _isMovementLocked;
    private bool _warnedMissingCamera;
    private bool _warnedMissingInput;
    private bool _warnedMissingMoveAction;

#if ENABLE_INPUT_SYSTEM
    private InputAction _moveAction;
#endif

    public Vector2 CurrentInput => _currentInput;
    public Vector3 CurrentDesiredMove => _currentDesiredMove;
    public Vector3 CurrentPlanarVelocity => _planarVelocity;
    public float CurrentVerticalVelocity => _verticalVelocity;
    public float VisualYawOffset => visualYawOffset;
    public bool CanMove => !_isMovementLocked;

    public void SetVisualYawOffset(float yawDegrees)
    {
        visualYawOffset = yawDegrees;
    }

    public void SetMovementLocked(bool isLocked)
    {
        _isMovementLocked = isLocked;
        if (_isMovementLocked)
        {
            _currentInput = Vector2.zero;
            _currentDesiredMove = Vector3.zero;
        }
    }

    public void SetDependencies(VirtualJoystick joystickReference, Transform cameraReference, Transform visualReference)
    {
        SetDependencies(joystickReference, cameraReference, visualReference, null, moveActionMapName, moveActionName);
    }

#if ENABLE_INPUT_SYSTEM
    public void SetDependencies(
        VirtualJoystick joystickReference,
        Transform cameraReference,
        Transform visualReference,
        InputActionAsset inputActionsReference,
        string actionMapName = "Player",
        string actionName = "Move")
    {
        joystick = joystickReference;
        cameraTransform = cameraReference;
        if (visualReference != null)
        {
            visualRoot = visualReference;
        }

        if (inputActionsReference != null)
        {
            inputActions = inputActionsReference;
        }

        if (!string.IsNullOrWhiteSpace(actionMapName))
        {
            moveActionMapName = actionMapName;
        }

        if (!string.IsNullOrWhiteSpace(actionName))
        {
            moveActionName = actionName;
        }

        ResolveMoveAction();
    }
#else
    public void SetDependencies(
        VirtualJoystick joystickReference,
        Transform cameraReference,
        Transform visualReference,
        Object _inputActionsReference,
        string _actionMapName = "Player",
        string _actionName = "Move")
    {
        joystick = joystickReference;
        cameraTransform = cameraReference;
        if (visualReference != null)
        {
            visualRoot = visualReference;
        }
    }
#endif

    private void Reset()
    {
        _characterController = GetComponent<CharacterController>();
        if (visualRoot == null)
        {
            visualRoot = transform;
        }
    }

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

#if ENABLE_INPUT_SYSTEM
        ResolveMoveAction();
#endif
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        if (_moveAction != null)
        {
            _moveAction.Enable();
        }
#endif
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (_moveAction != null)
        {
            _moveAction.Disable();
        }
#endif
    }

    private void Update()
    {
        if (_characterController == null)
        {
            return;
        }

        Vector2 input;
        Vector3 desiredMove;
        if (_isMovementLocked)
        {
            input = Vector2.zero;
            desiredMove = Vector3.zero;
        }
        else
        {
            input = ReadInput();
            desiredMove = ResolveMoveDirection(input);
        }

        _currentInput = input;
        _currentDesiredMove = desiredMove;
        Vector3 desiredPlanarVelocity = desiredMove * maxSpeed;

        float speedChangeRate = desiredPlanarVelocity.sqrMagnitude > 0.0001f
            ? acceleration
            : deceleration;

        _planarVelocity = Vector3.MoveTowards(_planarVelocity, desiredPlanarVelocity, speedChangeRate * Time.deltaTime);

        if (_characterController.isGrounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = groundedVerticalVelocity;
        }

        _verticalVelocity += gravity * Time.deltaTime;

        Vector3 frameVelocity = _planarVelocity + (Vector3.up * _verticalVelocity);
        _characterController.Move(frameVelocity * Time.deltaTime);

        if (_characterController.isGrounded && _verticalVelocity < groundedVerticalVelocity)
        {
            _verticalVelocity = groundedVerticalVelocity;
        }

        UpdateVisualFacing(desiredMove);
    }

    private Vector2 ReadInput()
    {
        float thresholdSqr = inputThreshold * inputThreshold;

        if (joystick != null)
        {
            Vector2 joystickInput = joystick.Value;
            if (joystickInput.sqrMagnitude > thresholdSqr)
            {
                _warnedMissingInput = false;
                return joystickInput;
            }
        }

        Vector2 fallbackInput = ReadActionInput();
        if (fallbackInput.sqrMagnitude > thresholdSqr)
        {
            _warnedMissingInput = false;
            return fallbackInput;
        }

        if (joystick == null && !HasActionInputConfigured())
        {
            if (!_warnedMissingInput)
            {
                Debug.LogWarning(
                    "PlayerMovementController has no active movement input source. Assign a VirtualJoystick or InputActionAsset fallback.",
                    this);
                _warnedMissingInput = true;
            }
        }
        else
        {
            _warnedMissingInput = false;
        }

        return Vector2.zero;
    }

    private Vector3 ResolveMoveDirection(Vector2 input)
    {
        float thresholdSqr = inputThreshold * inputThreshold;
        if (input.sqrMagnitude <= thresholdSqr)
        {
            return Vector3.zero;
        }

        if (cameraTransform == null)
        {
            if (!_warnedMissingCamera)
            {
                Debug.LogWarning("PlayerMovementController has no cameraTransform reference. Falling back to world-space axes.", this);
                _warnedMissingCamera = true;
            }

            return Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);
        }

        _warnedMissingCamera = false;

        Vector3 cameraForward = cameraTransform.forward;
        cameraForward.y = 0f;
        if (cameraForward.sqrMagnitude < 0.0001f)
        {
            cameraForward = Vector3.forward;
        }
        cameraForward.Normalize();

        Vector3 cameraRight = cameraTransform.right;
        cameraRight.y = 0f;
        if (cameraRight.sqrMagnitude < 0.0001f)
        {
            cameraRight = Vector3.right;
        }
        cameraRight.Normalize();

        Vector3 moveDirection = (cameraRight * input.x) + (cameraForward * input.y);
        return Vector3.ClampMagnitude(moveDirection, 1f);
    }

    private void UpdateVisualFacing(Vector3 desiredMoveDirection)
    {
        float thresholdSqr = inputThreshold * inputThreshold;
        if (desiredMoveDirection.sqrMagnitude <= thresholdSqr)
        {
            return;
        }

        float rotationLerp = 1f - Mathf.Exp(-Mathf.Max(0.01f, turnSharpness) * Time.deltaTime);

        Quaternion facingRotation = Quaternion.LookRotation(desiredMoveDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, facingRotation, rotationLerp);

        if (visualRoot != null && visualRoot != transform)
        {
            Quaternion visualRotation = facingRotation * Quaternion.Euler(0f, visualYawOffset, 0f);
            visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, visualRotation, rotationLerp);
        }
    }

    private void OnValidate()
    {
        maxSpeed = Mathf.Max(0f, maxSpeed);
        acceleration = Mathf.Max(0.01f, acceleration);
        deceleration = Mathf.Max(0.01f, deceleration);
        turnSharpness = Mathf.Max(0.01f, turnSharpness);
        inputThreshold = Mathf.Clamp(inputThreshold, 0f, 1f);

#if ENABLE_INPUT_SYSTEM
        if (Application.isPlaying)
        {
            ResolveMoveAction();
        }
#endif
    }

    private bool HasActionInputConfigured()
    {
#if ENABLE_INPUT_SYSTEM
        return _moveAction != null;
#else
        return false;
#endif
    }

    private Vector2 ReadActionInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (_moveAction == null)
        {
            ResolveMoveAction();
            if (_moveAction == null)
            {
                return Vector2.zero;
            }
        }

        return _moveAction.ReadValue<Vector2>();
#else
        return Vector2.zero;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private void ResolveMoveAction()
    {
        InputAction previousMoveAction = _moveAction;
        _moveAction = null;

        if (inputActions == null)
        {
            _warnedMissingMoveAction = false;
            return;
        }

        InputActionMap actionMap = inputActions.FindActionMap(moveActionMapName, throwIfNotFound: false);
        if (actionMap == null)
        {
            if (!_warnedMissingMoveAction)
            {
                Debug.LogWarning(
                    $"PlayerMovementController could not find action map '{moveActionMapName}' in assigned InputActionAsset.",
                    this);
                _warnedMissingMoveAction = true;
            }

            return;
        }

        InputAction resolvedMoveAction = actionMap.FindAction(moveActionName, throwIfNotFound: false);
        if (resolvedMoveAction == null)
        {
            if (!_warnedMissingMoveAction)
            {
                Debug.LogWarning(
                    $"PlayerMovementController could not find action '{moveActionName}' in map '{moveActionMapName}'.",
                    this);
                _warnedMissingMoveAction = true;
            }

            return;
        }

        _warnedMissingMoveAction = false;
        _moveAction = resolvedMoveAction;

        if (previousMoveAction != null && previousMoveAction != _moveAction && previousMoveAction.enabled)
        {
            previousMoveAction.Disable();
        }

        if (isActiveAndEnabled && !_moveAction.enabled)
        {
            _moveAction.Enable();
        }
    }
#endif
}
