using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerMovementController : MonoBehaviour
{
    [SerializeField] private VirtualJoystick joystick;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float visualYawOffset = 90f;
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
    private bool _warnedMissingJoystick;
    private bool _warnedMissingCamera;

    public void SetDependencies(VirtualJoystick joystickReference, Transform cameraReference, Transform visualReference)
    {
        joystick = joystickReference;
        cameraTransform = cameraReference;
        if (visualReference != null)
        {
            visualRoot = visualReference;
        }
    }

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
    }

    private void Update()
    {
        if (_characterController == null)
        {
            return;
        }

        Vector2 input = ReadInput();
        Vector3 desiredMove = ResolveMoveDirection(input);
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

        UpdateVisualFacing();
    }

    private Vector2 ReadInput()
    {
        if (joystick == null)
        {
            if (!_warnedMissingJoystick)
            {
                Debug.LogWarning("PlayerMovementController has no VirtualJoystick reference. Movement input is disabled.", this);
                _warnedMissingJoystick = true;
            }

            return Vector2.zero;
        }

        _warnedMissingJoystick = false;
        return joystick.Value;
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

    private void UpdateVisualFacing()
    {
        Transform targetRoot = visualRoot != null ? visualRoot : transform;

        Vector3 facingDirection = new Vector3(_planarVelocity.x, 0f, _planarVelocity.z);
        float thresholdSqr = inputThreshold * inputThreshold;
        if (facingDirection.sqrMagnitude <= thresholdSqr)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(facingDirection.normalized, Vector3.up) *
            Quaternion.Euler(0f, visualYawOffset, 0f);

        float rotationLerp = 1f - Mathf.Exp(-Mathf.Max(0.01f, turnSharpness) * Time.deltaTime);
        targetRoot.rotation = Quaternion.Slerp(targetRoot.rotation, targetRotation, rotationLerp);
    }

    private void OnValidate()
    {
        maxSpeed = Mathf.Max(0f, maxSpeed);
        acceleration = Mathf.Max(0.01f, acceleration);
        deceleration = Mathf.Max(0.01f, deceleration);
        turnSharpness = Mathf.Max(0.01f, turnSharpness);
        inputThreshold = Mathf.Clamp(inputThreshold, 0f, 1f);
    }
}
