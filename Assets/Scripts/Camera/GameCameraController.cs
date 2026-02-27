using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class GameCameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Transform ignoreRoot;
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 1.55f, 0f);

    [Header("Shoulder Orbit")]
    [SerializeField] private float shoulderRightOffset = 1.5f;
    [SerializeField] private float shoulderUpOffset = 2f;
    [SerializeField] private float followDistance = 4f;
    [SerializeField] private float initialYaw = 0f;
    [SerializeField] private float initialPitch = 20f;
    [SerializeField] private float pitchMin = -20f;
    [SerializeField] private float pitchMax = 60f;

    [Header("Look Input")]
    [SerializeField] private Vector2 gamepadLookSensitivity = new Vector2(180f, 140f);
    [SerializeField] private Vector2 mouseLookSensitivity = new Vector2(0.18f, 0.14f);
    [SerializeField] private Vector2 touchLookSensitivity = new Vector2(0.16f, 0.12f);
    [SerializeField] private bool requireRightMouseButton = true;
    [SerializeField] private TouchLookDragRegion touchLookRegion;

    [Header("Auto Follow")]
    [SerializeField] private bool autoFollowBehindMovement = true;
    [SerializeField] private PlayerMovementController movementController;
    [SerializeField] private float autoFollowDelay = 0f;
    [SerializeField] private float autoFollowYawSharpness = 7f;
    [SerializeField] private float autoFollowDirectionThreshold = 0.08f;
    [SerializeField] private float manualLookThreshold = 0.001f;

    [Header("Smoothing")]
    [SerializeField] private float followSmoothTime = 0.14f;
    [SerializeField] private float rotationSharpness = 14f;

    [Header("Obstruction")]
    [SerializeField] private LayerMask obstructionMask = ~0;
    [SerializeField] private float obstructionRadius = 0.35f;
    [SerializeField] private float obstructionBuffer = 0.2f;
    [SerializeField] private float minObstructedDistance = 0.6f;
    [SerializeField] private float obstructedDistanceSharpness = 24f;
    [SerializeField] private float clearDistanceSharpness = 6f;

    private readonly RaycastHit[] _obstructionHits = new RaycastHit[16];

    private Transform _resolvedTarget;
    private Vector3 _smoothedPosition;
    private Vector3 _positionVelocity;
    private Quaternion _smoothedRotation;
    private float _smoothedLookDistance;
    private float _orbitYaw;
    private float _orbitPitch;
    private bool _hasSmoothedState;
    private bool _orbitInitialized;
    private bool _warnedMissingTarget;
    private float _lastManualLookTime = float.NegativeInfinity;

    public void SetTarget(Transform newTarget, Transform newIgnoreRoot = null)
    {
        target = newTarget;
        _resolvedTarget = newTarget;
        movementController = null;

        if (newIgnoreRoot != null)
        {
            ignoreRoot = newIgnoreRoot;
        }
        else if (ignoreRoot == null && newTarget != null)
        {
            ignoreRoot = newTarget.root;
        }

        _warnedMissingTarget = false;
        _hasSmoothedState = false;
        ResolveMovementControllerIfNeeded();
        SnapNow();
    }

    public void SetTouchLookRegion(TouchLookDragRegion newTouchLookRegion)
    {
        touchLookRegion = newTouchLookRegion;
    }

    public void SnapNow()
    {
        if (!ResolveTargetIfNeeded())
        {
            return;
        }

        SolveAndApply(0f, snap: true);
    }

    private void Awake()
    {
        ResolveTargetIfNeeded();
        ResolveMovementControllerIfNeeded();
        InitializeOrbitIfNeeded();
    }

    private void Start()
    {
        SnapNow();
    }

    private void LateUpdate()
    {
        if (!ResolveTargetIfNeeded())
        {
            return;
        }

        SolveAndApply(Time.deltaTime, snap: false);
    }

    private void OnValidate()
    {
        followDistance = Mathf.Max(0.1f, followDistance);
        pitchMin = Mathf.Clamp(pitchMin, -89f, 89f);
        pitchMax = Mathf.Clamp(pitchMax, pitchMin, 89f);
        initialPitch = Mathf.Clamp(initialPitch, pitchMin, pitchMax);
        autoFollowDelay = Mathf.Max(0f, autoFollowDelay);
        autoFollowYawSharpness = Mathf.Max(0.01f, autoFollowYawSharpness);
        autoFollowDirectionThreshold = Mathf.Clamp(autoFollowDirectionThreshold, 0.001f, 2f);
        manualLookThreshold = Mathf.Clamp(manualLookThreshold, 0.000001f, 1f);

        followSmoothTime = Mathf.Max(0.01f, followSmoothTime);
        rotationSharpness = Mathf.Max(0.01f, rotationSharpness);

        obstructionRadius = Mathf.Max(0.01f, obstructionRadius);
        obstructionBuffer = Mathf.Max(0f, obstructionBuffer);
        minObstructedDistance = Mathf.Clamp(minObstructedDistance, 0.05f, followDistance);
        obstructedDistanceSharpness = Mathf.Max(0.01f, obstructedDistanceSharpness);
        clearDistanceSharpness = Mathf.Max(0.01f, clearDistanceSharpness);
    }

    private void OnDrawGizmosSelected()
    {
        if (!TryGetEffectiveTarget(out Transform effectiveTarget))
        {
            return;
        }

        float gizmoYaw = Application.isPlaying ? _orbitYaw : initialYaw;
        float gizmoPitch = Application.isPlaying ? _orbitPitch : Mathf.Clamp(initialPitch, pitchMin, pitchMax);

        Quaternion yawRotation = Quaternion.Euler(0f, gizmoYaw, 0f);
        Quaternion orbitRotation = Quaternion.Euler(gizmoPitch, gizmoYaw, 0f);

        Vector3 lookPoint = effectiveTarget.position + lookAtOffset;
        Vector3 shoulderPivot =
            effectiveTarget.position +
            (Vector3.up * shoulderUpOffset) +
            (yawRotation * Vector3.right * shoulderRightOffset);
        Vector3 idealCameraPosition = shoulderPivot - (orbitRotation * Vector3.forward * followDistance);

        Gizmos.color = new Color(1f, 0.84f, 0f, 0.9f);
        Gizmos.DrawWireSphere(lookPoint, obstructionRadius);

        Gizmos.color = new Color(0f, 0.85f, 1f, 0.9f);
        Gizmos.DrawLine(lookPoint, idealCameraPosition);
        Gizmos.DrawWireSphere(idealCameraPosition, obstructionRadius);
    }

    private bool ResolveTargetIfNeeded()
    {
        if (target != null)
        {
            _resolvedTarget = target;
            if (ignoreRoot == null)
            {
                ignoreRoot = target.root;
            }

            ResolveMovementControllerIfNeeded();
            _warnedMissingTarget = false;
            return true;
        }

        if (_resolvedTarget != null)
        {
            ResolveMovementControllerIfNeeded();
            return true;
        }

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject == null)
        {
            if (!_warnedMissingTarget)
            {
                Debug.LogWarning("GameCameraController could not find a Player-tagged object to follow.", this);
                _warnedMissingTarget = true;
            }

            return false;
        }

        Transform autoTarget = FindChildByName(playerObject.transform, "CameraTarget");
        _resolvedTarget = autoTarget != null ? autoTarget : playerObject.transform;
        target = _resolvedTarget;
        if (ignoreRoot == null)
        {
            ignoreRoot = playerObject.transform;
        }

        ResolveMovementControllerIfNeeded();
        _warnedMissingTarget = false;
        return true;
    }

    private bool TryGetEffectiveTarget(out Transform effectiveTarget)
    {
        effectiveTarget = target != null ? target : _resolvedTarget;
        return effectiveTarget != null;
    }

    private void InitializeOrbitIfNeeded()
    {
        if (_orbitInitialized)
        {
            return;
        }

        _orbitYaw = initialYaw;
        _orbitPitch = Mathf.Clamp(initialPitch, pitchMin, pitchMax);
        _orbitInitialized = true;
    }

    private void SolveAndApply(float deltaTime, bool snap)
    {
        if (!TryGetEffectiveTarget(out Transform effectiveTarget))
        {
            return;
        }

        InitializeOrbitIfNeeded();

        if (!snap)
        {
            bool hadManualLook = ApplyOrbitInput(deltaTime);
            ApplyAutoFollow(deltaTime, hadManualLook);
        }

        Vector3 lookPoint = effectiveTarget.position + lookAtOffset;
        Quaternion yawRotation = Quaternion.Euler(0f, _orbitYaw, 0f);
        Quaternion orbitRotation = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f);

        Vector3 shoulderPivot =
            effectiveTarget.position +
            (Vector3.up * shoulderUpOffset) +
            (yawRotation * Vector3.right * shoulderRightOffset);

        Vector3 idealPosition = shoulderPivot - (orbitRotation * Vector3.forward * followDistance);
        Vector3 castVector = idealPosition - lookPoint;
        float castMagnitude = castVector.magnitude;
        float maxLookDistance = Mathf.Max(0.01f, castMagnitude);
        Vector3 castDirection = castMagnitude > 0.0001f ? (castVector / castMagnitude) : -transform.forward;

        float solvedLookDistance = SolveDistance(lookPoint, castDirection, maxLookDistance, out bool isObstructed);
        float distanceSharpness = isObstructed ? obstructedDistanceSharpness : clearDistanceSharpness;

        if (snap || !_hasSmoothedState)
        {
            _smoothedLookDistance = solvedLookDistance;
            _smoothedPosition = lookPoint + (castDirection * _smoothedLookDistance);
            _smoothedRotation = Quaternion.LookRotation(lookPoint - _smoothedPosition, Vector3.up);
            _positionVelocity = Vector3.zero;
            _hasSmoothedState = true;
        }
        else
        {
            float distanceLerp = 1f - Mathf.Exp(-distanceSharpness * deltaTime);
            _smoothedLookDistance = Mathf.Lerp(_smoothedLookDistance, solvedLookDistance, distanceLerp);

            Vector3 desiredPosition = lookPoint + (castDirection * _smoothedLookDistance);
            _smoothedPosition = Vector3.SmoothDamp(
                _smoothedPosition,
                desiredPosition,
                ref _positionVelocity,
                followSmoothTime,
                Mathf.Infinity,
                deltaTime);

            Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - _smoothedPosition, Vector3.up);
            float rotationLerp = 1f - Mathf.Exp(-rotationSharpness * deltaTime);
            _smoothedRotation = Quaternion.Slerp(_smoothedRotation, desiredRotation, rotationLerp);
        }

        transform.SetPositionAndRotation(_smoothedPosition, _smoothedRotation);
    }

    private bool ApplyOrbitInput(float deltaTime)
    {
        Vector2 lookDelta = ReadLookDelta(deltaTime);
        float manualLookThresholdSqr = manualLookThreshold * manualLookThreshold;
        bool hasManualLook = lookDelta.sqrMagnitude > manualLookThresholdSqr;
        if (hasManualLook)
        {
            _lastManualLookTime = Time.time;
        }

        _orbitYaw += lookDelta.x;
        _orbitPitch = Mathf.Clamp(_orbitPitch - lookDelta.y, pitchMin, pitchMax);
        return hasManualLook;
    }

    private void ApplyAutoFollow(float deltaTime, bool hadManualLook)
    {
        if (!autoFollowBehindMovement || hadManualLook)
        {
            return;
        }

        if ((Time.time - _lastManualLookTime) < autoFollowDelay)
        {
            return;
        }

        if (!TryResolveAutoFollowDirection(out Vector3 followDirection))
        {
            return;
        }

        float targetYaw = Mathf.Atan2(followDirection.x, followDirection.z) * Mathf.Rad2Deg;
        float yawLerp = 1f - Mathf.Exp(-autoFollowYawSharpness * deltaTime);
        _orbitYaw = Mathf.LerpAngle(_orbitYaw, targetYaw, yawLerp);
    }

    private bool TryResolveAutoFollowDirection(out Vector3 followDirection)
    {
        followDirection = Vector3.zero;
        float minDirectionSqr = autoFollowDirectionThreshold * autoFollowDirectionThreshold;

        ResolveMovementControllerIfNeeded();
        if (movementController != null)
        {
            Vector3 planarVelocity = movementController.CurrentPlanarVelocity;
            planarVelocity.y = 0f;
            if (planarVelocity.sqrMagnitude > minDirectionSqr)
            {
                followDirection = planarVelocity.normalized;
                return true;
            }

            Vector3 desiredMove = movementController.CurrentDesiredMove;
            desiredMove.y = 0f;
            if (desiredMove.sqrMagnitude > minDirectionSqr)
            {
                followDirection = desiredMove.normalized;
                return true;
            }

            Vector3 playerForward = movementController.transform.forward;
            playerForward.y = 0f;
            if (playerForward.sqrMagnitude > minDirectionSqr)
            {
                followDirection = playerForward.normalized;
                return true;
            }
        }

        if (ignoreRoot != null)
        {
            Vector3 ignoreForward = ignoreRoot.forward;
            ignoreForward.y = 0f;
            if (ignoreForward.sqrMagnitude > minDirectionSqr)
            {
                followDirection = ignoreForward.normalized;
                return true;
            }
        }

        if (!TryGetEffectiveTarget(out Transform effectiveTarget))
        {
            return false;
        }

        Vector3 targetForward = effectiveTarget.forward;
        targetForward.y = 0f;
        if (targetForward.sqrMagnitude > minDirectionSqr)
        {
            followDirection = targetForward.normalized;
            return true;
        }

        return false;
    }

    private void ResolveMovementControllerIfNeeded()
    {
        if (!TryGetEffectiveTarget(out Transform effectiveTarget))
        {
            return;
        }

        if (movementController != null)
        {
            Transform movementTransform = movementController.transform;
            if (movementTransform == effectiveTarget || effectiveTarget.IsChildOf(movementTransform))
            {
                return;
            }
        }

        movementController = effectiveTarget.GetComponentInParent<PlayerMovementController>();
    }

    private Vector2 ReadLookDelta(float deltaTime)
    {
        Vector2 lookDelta = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            Vector2 stick = gamepad.rightStick.ReadValue();
            lookDelta += new Vector2(stick.x * gamepadLookSensitivity.x, stick.y * gamepadLookSensitivity.y) * deltaTime;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && (!requireRightMouseButton || mouse.rightButton.isPressed))
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            lookDelta += new Vector2(mouseDelta.x * mouseLookSensitivity.x, mouseDelta.y * mouseLookSensitivity.y);
        }
#endif

        if (touchLookRegion != null)
        {
            Vector2 touchDelta = touchLookRegion.ConsumeDelta();
            lookDelta += new Vector2(touchDelta.x * touchLookSensitivity.x, touchDelta.y * touchLookSensitivity.y);
        }

        return lookDelta;
    }

    private float SolveDistance(Vector3 castOrigin, Vector3 castDirection, float maxDistance, out bool isObstructed)
    {
        float clampedMaxDistance = Mathf.Max(0.1f, maxDistance);
        float minDistance = Mathf.Clamp(minObstructedDistance, 0.05f, clampedMaxDistance);

        int hitCount = Physics.SphereCastNonAlloc(
            castOrigin,
            obstructionRadius,
            castDirection,
            _obstructionHits,
            clampedMaxDistance,
            obstructionMask,
            QueryTriggerInteraction.Ignore);

        float bestDistance = clampedMaxDistance;
        isObstructed = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = _obstructionHits[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            Transform hitTransform = hitCollider.transform;
            if (ignoreRoot != null && (hitTransform == ignoreRoot || hitTransform.IsChildOf(ignoreRoot)))
            {
                continue;
            }

            float candidateDistance = Mathf.Clamp(_obstructionHits[i].distance - obstructionBuffer, minDistance, clampedMaxDistance);
            if (candidateDistance < bestDistance)
            {
                bestDistance = candidateDistance;
                isObstructed = true;
            }
        }

        return bestDistance;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        int childCount = root.childCount;
        for (int index = 0; index < childCount; index++)
        {
            Transform match = FindChildByName(root.GetChild(index), childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
