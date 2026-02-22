using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class GameCameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Transform ignoreRoot;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.55f, 0f);

    [Header("Framing")]
    [SerializeField] private float pitch = 50f;
    [SerializeField] private float yaw = 45f;
    [SerializeField] private float distance = 11.5f;

    [Header("Smoothing")]
    [SerializeField] private float followSharpness = 12f;
    [SerializeField] private float rotationSharpness = 14f;

    [Header("Obstruction")]
    [SerializeField] private LayerMask obstructionMask = ~0;
    [SerializeField] private float obstructionRadius = 0.35f;
    [SerializeField] private float obstructionBuffer = 0.2f;
    [SerializeField] private float minObstructedDistance = 3f;

    private readonly RaycastHit[] _obstructionHits = new RaycastHit[16];

    private Transform _resolvedTarget;
    private Vector3 _smoothedPosition;
    private Quaternion _smoothedRotation;
    private bool _hasSmoothedState;
    private bool _warnedMissingTarget;

    private void Awake()
    {
        ResolveTargetIfNeeded();
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

    public void SetTarget(Transform newTarget, Transform newIgnoreRoot = null)
    {
        target = newTarget;
        _resolvedTarget = newTarget;

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
        SnapNow();
    }

    public void SnapNow()
    {
        if (!ResolveTargetIfNeeded())
        {
            return;
        }

        SolveAndApply(0f, snap: true);
    }

    private void OnValidate()
    {
        distance = Mathf.Max(0.1f, distance);
        followSharpness = Mathf.Max(0.1f, followSharpness);
        rotationSharpness = Mathf.Max(0.1f, rotationSharpness);
        obstructionRadius = Mathf.Max(0.01f, obstructionRadius);
        obstructionBuffer = Mathf.Max(0f, obstructionBuffer);
        minObstructedDistance = Mathf.Max(0.1f, minObstructedDistance);
        minObstructedDistance = Mathf.Min(minObstructedDistance, distance);
    }

    private void OnDrawGizmosSelected()
    {
        if (!TryGetEffectiveTarget(out Transform effectiveTarget))
        {
            return;
        }

        Quaternion fixedRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = effectiveTarget.position + targetOffset;
        Vector3 idealCameraPosition = pivot - (fixedRotation * Vector3.forward * distance);

        Gizmos.color = new Color(1f, 0.84f, 0f, 0.9f);
        Gizmos.DrawWireSphere(pivot, obstructionRadius);

        Gizmos.color = new Color(0f, 0.85f, 1f, 0.9f);
        Gizmos.DrawLine(pivot, idealCameraPosition);
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
            _warnedMissingTarget = false;
            return true;
        }

        if (_resolvedTarget != null)
        {
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

        _warnedMissingTarget = false;
        return true;
    }

    private bool TryGetEffectiveTarget(out Transform effectiveTarget)
    {
        effectiveTarget = target != null ? target : _resolvedTarget;
        return effectiveTarget != null;
    }

    private void SolveAndApply(float deltaTime, bool snap)
    {
        if (!TryGetEffectiveTarget(out Transform effectiveTarget))
        {
            return;
        }

        Quaternion desiredRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = effectiveTarget.position + targetOffset;
        float solvedDistance = SolveDistance(pivot, desiredRotation);
        Vector3 desiredPosition = pivot - (desiredRotation * Vector3.forward * solvedDistance);

        if (snap || !_hasSmoothedState)
        {
            _smoothedPosition = desiredPosition;
            _smoothedRotation = desiredRotation;
            _hasSmoothedState = true;
        }
        else
        {
            float positionLerp = 1f - Mathf.Exp(-followSharpness * deltaTime);
            float rotationLerp = 1f - Mathf.Exp(-rotationSharpness * deltaTime);

            _smoothedPosition = Vector3.Lerp(_smoothedPosition, desiredPosition, positionLerp);
            _smoothedRotation = Quaternion.Slerp(_smoothedRotation, desiredRotation, rotationLerp);
        }

        transform.SetPositionAndRotation(_smoothedPosition, _smoothedRotation);
    }

    private float SolveDistance(Vector3 pivot, Quaternion rotation)
    {
        float maxDistance = Mathf.Max(0.1f, distance);
        float minDistance = Mathf.Clamp(minObstructedDistance, 0.1f, maxDistance);
        Vector3 castDirection = -(rotation * Vector3.forward);

        int hitCount = Physics.SphereCastNonAlloc(
            pivot,
            obstructionRadius,
            castDirection,
            _obstructionHits,
            maxDistance,
            obstructionMask,
            QueryTriggerInteraction.Ignore);

        float bestDistance = maxDistance;
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

            float candidateDistance = Mathf.Clamp(_obstructionHits[i].distance - obstructionBuffer, minDistance, maxDistance);
            if (candidateDistance < bestDistance)
            {
                bestDistance = candidateDistance;
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
        for (int i = 0; i < childCount; i++)
        {
            Transform match = FindChildByName(root.GetChild(i), childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
