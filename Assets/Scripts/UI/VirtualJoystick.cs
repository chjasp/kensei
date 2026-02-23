using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const float MinRegionSize = 0.001f;
    private const float MinRegionScale = 0.0001f;

    [SerializeField] private RectTransform inputRegion;
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;
    [SerializeField] private bool allowMouseFallback = true;
    [SerializeField] private float radiusPixels = 90f;
    [SerializeField] private float deadZone = 0.12f;
    [SerializeField] private float releaseLerpSharpness = 20f;
    [SerializeField] private bool snapBackOnRelease = true;

    public Vector2 Value => _value;

    private int _activePointerId = int.MinValue;
    private Vector2 _rawValue;
    private Vector2 _value;
    private PointerSource _pointerSource;
    private bool _warnedMissingReferences;
    private bool _warnedInvalidInputRegion;

    private enum PointerSource
    {
        None,
        UIEvent,
        MouseFallback
    }

    private void Awake()
    {
        TryResolveReferences();
        UpdateVisuals();
    }

    private void Update()
    {
#if (UNITY_EDITOR || UNITY_STANDALONE) && ENABLE_INPUT_SYSTEM
        ProcessMouseFallback();
#endif

        if (_pointerSource == PointerSource.None && snapBackOnRelease && _rawValue.sqrMagnitude > 0f)
        {
            float lerp = 1f - Mathf.Exp(-Mathf.Max(0.01f, releaseLerpSharpness) * Time.unscaledDeltaTime);
            _rawValue = Vector2.Lerp(_rawValue, Vector2.zero, lerp);

            if (_rawValue.sqrMagnitude < 0.0001f)
            {
                _rawValue = Vector2.zero;
            }

            _value = ApplyDeadZone(_rawValue);
            UpdateVisuals();
        }
    }

    public void SetVisualReferences(RectTransform newBackground, RectTransform newHandle)
    {
        background = newBackground;
        handle = newHandle;
        _warnedMissingReferences = false;
        _value = ApplyDeadZone(_rawValue);
        UpdateVisuals();
    }

    public void SetTuning(float newRadiusPixels, float newDeadZone, float newReleaseLerpSharpness, bool shouldSnapBackOnRelease)
    {
        radiusPixels = Mathf.Max(1f, newRadiusPixels);
        deadZone = Mathf.Clamp(newDeadZone, 0f, 0.99f);
        releaseLerpSharpness = Mathf.Max(0.01f, newReleaseLerpSharpness);
        snapBackOnRelease = shouldSnapBackOnRelease;
        _value = ApplyDeadZone(_rawValue);
        UpdateVisuals();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (ShouldIgnoreUiPointer(eventData))
        {
            return;
        }

        if (_pointerSource == PointerSource.MouseFallback)
        {
            return;
        }

        if (_pointerSource == PointerSource.UIEvent && eventData.pointerId != _activePointerId)
        {
            return;
        }

        _pointerSource = PointerSource.UIEvent;
        _activePointerId = eventData.pointerId;
        ApplyScreenPosition(eventData.position, eventData.pressEventCamera);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ShouldIgnoreUiPointer(eventData))
        {
            return;
        }

        if (_pointerSource != PointerSource.UIEvent || eventData.pointerId != _activePointerId)
        {
            return;
        }

        ApplyScreenPosition(eventData.position, eventData.pressEventCamera);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (ShouldIgnoreUiPointer(eventData))
        {
            return;
        }

        if (_pointerSource != PointerSource.UIEvent || eventData.pointerId != _activePointerId)
        {
            return;
        }

        ClearPointerSource(PointerSource.UIEvent);

        if (!Application.isPlaying && snapBackOnRelease)
        {
            _rawValue = Vector2.zero;
            _value = Vector2.zero;
            UpdateVisuals();
        }
    }

    private void ApplyScreenPosition(Vector2 screenPosition, Camera eventCamera)
    {
        if (!TryResolveReferences())
        {
            return;
        }

        Camera resolvedEventCamera = ResolveEventCamera(background, eventCamera);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background,
                screenPosition,
                resolvedEventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        _rawValue = Vector2.ClampMagnitude(localPoint / Mathf.Max(1f, radiusPixels), 1f);
        _value = ApplyDeadZone(_rawValue);
        UpdateVisuals();
    }

    private bool TryResolveReferences()
    {
        if (background == null)
        {
            Transform backgroundTransform = transform.Find("JoystickBackground");
            if (backgroundTransform != null)
            {
                background = backgroundTransform as RectTransform;
            }
        }

        if (handle == null && background != null)
        {
            Transform handleTransform = background.Find("JoystickHandle");
            if (handleTransform != null)
            {
                handle = handleTransform as RectTransform;
            }
        }

        bool resolved = background != null && handle != null;
        if (!resolved && !_warnedMissingReferences)
        {
            Debug.LogWarning("VirtualJoystick requires background and handle RectTransforms.", this);
            _warnedMissingReferences = true;
        }

        if (resolved)
        {
            _warnedMissingReferences = false;
        }

        return resolved;
    }

#if (UNITY_EDITOR || UNITY_STANDALONE) && ENABLE_INPUT_SYSTEM
    private void ProcessMouseFallback()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            if (_pointerSource == PointerSource.MouseFallback)
            {
                ClearPointerSource(PointerSource.MouseFallback);
            }

            return;
        }

        if (!allowMouseFallback || !Application.isPlaying)
        {
            if (_pointerSource == PointerSource.MouseFallback && !mouse.leftButton.isPressed)
            {
                ClearPointerSource(PointerSource.MouseFallback);
            }

            return;
        }

        if (_pointerSource == PointerSource.UIEvent && !ShouldYieldUiSourceToMouse(mouse))
        {
            return;
        }

        if (_pointerSource == PointerSource.UIEvent)
        {
            ClearPointerSource(PointerSource.UIEvent);
        }

        if (!TryResolveReferences())
        {
            return;
        }

        if (!TryResolveInputRegion(out RectTransform region, out Camera regionCamera))
        {
            return;
        }

        Vector2 mousePosition = mouse.position.ReadValue();

        if (_pointerSource != PointerSource.MouseFallback)
        {
            if (!mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (!RectTransformUtility.RectangleContainsScreenPoint(region, mousePosition, regionCamera))
            {
                return;
            }

            _pointerSource = PointerSource.MouseFallback;
            _activePointerId = int.MinValue;
            ApplyScreenPosition(mousePosition, regionCamera);
            return;
        }

        if (mouse.leftButton.isPressed)
        {
            ApplyScreenPosition(mousePosition, regionCamera);
            return;
        }

        if (mouse.leftButton.wasReleasedThisFrame || !mouse.leftButton.isPressed)
        {
            ClearPointerSource(PointerSource.MouseFallback);
        }
    }
#endif

    private bool TryResolveInputRegion(out RectTransform region, out Camera eventCamera)
    {
        region = ResolveInputRegion();
        eventCamera = null;

        if (region == null)
        {
            WarnInvalidInputRegion("VirtualJoystick could not resolve an input region RectTransform.");
            return false;
        }

        if (!IsRegionValid(region))
        {
            WarnInvalidInputRegion("VirtualJoystick input region is collapsed (zero size or zero scale).");
            return false;
        }

        _warnedInvalidInputRegion = false;
        eventCamera = ResolveEventCamera(region, null);
        return true;
    }

    private RectTransform ResolveInputRegion()
    {
        if (inputRegion != null)
        {
            return inputRegion;
        }

        RectTransform selfRect = transform as RectTransform;
        if (selfRect != null)
        {
            return selfRect;
        }

        return background;
    }

    private bool IsRegionValid(RectTransform region)
    {
        Rect rect = region.rect;
        if (rect.width <= MinRegionSize || rect.height <= MinRegionSize)
        {
            return false;
        }

        Vector3 lossyScale = region.lossyScale;
        return Mathf.Abs(lossyScale.x) > MinRegionScale && Mathf.Abs(lossyScale.y) > MinRegionScale;
    }

    private void WarnInvalidInputRegion(string message)
    {
        if (_warnedInvalidInputRegion)
        {
            return;
        }

        Debug.LogWarning(message, this);
        _warnedInvalidInputRegion = true;
    }

    private bool ShouldIgnoreUiPointer(PointerEventData eventData)
    {
#if (UNITY_EDITOR || UNITY_STANDALONE) && ENABLE_INPUT_SYSTEM
        if (!allowMouseFallback || eventData == null)
        {
            return false;
        }

        if (IsMousePointer(eventData.pointerId))
        {
            return true;
        }

        if (HasAnyActiveTouches())
        {
            return false;
        }

        return Mouse.current != null && eventData.button == PointerEventData.InputButton.Left;
#else
        return false;
#endif
    }

    private static bool IsMousePointer(int pointerId)
    {
        return pointerId == PointerInputModule.kMouseLeftId
            || pointerId == PointerInputModule.kMouseRightId
            || pointerId == PointerInputModule.kMouseMiddleId;
    }

#if (UNITY_EDITOR || UNITY_STANDALONE) && ENABLE_INPUT_SYSTEM
    private bool ShouldYieldUiSourceToMouse(Mouse mouse)
    {
        if (!allowMouseFallback || mouse == null)
        {
            return false;
        }

        if (HasAnyActiveTouches())
        {
            return false;
        }

        if (mouse.leftButton.wasPressedThisFrame || mouse.leftButton.isPressed)
        {
            return true;
        }

        return !mouse.leftButton.isPressed && IsMousePointer(_activePointerId);
    }

    private static bool HasAnyActiveTouches()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen == null)
        {
            return false;
        }

        var touches = touchscreen.touches;
        for (int index = 0; index < touches.Count; index++)
        {
            if (touches[index].press.isPressed)
            {
                return true;
            }
        }

        return false;
    }
#endif

    private static Camera ResolveEventCamera(RectTransform targetRect, Camera providedCamera)
    {
        if (providedCamera != null)
        {
            return providedCamera;
        }

        if (targetRect == null)
        {
            return null;
        }

        Canvas canvas = targetRect.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }

    private void ClearPointerSource(PointerSource source)
    {
        if (_pointerSource != source)
        {
            return;
        }

        _pointerSource = PointerSource.None;
        _activePointerId = int.MinValue;
    }

    private Vector2 ApplyDeadZone(Vector2 rawInput)
    {
        float clampedDeadZone = Mathf.Clamp(deadZone, 0f, 0.99f);
        float magnitude = rawInput.magnitude;

        if (magnitude <= clampedDeadZone)
        {
            return Vector2.zero;
        }

        float scaledMagnitude = Mathf.Clamp01((magnitude - clampedDeadZone) / (1f - clampedDeadZone));
        return rawInput.normalized * scaledMagnitude;
    }

    private void UpdateVisuals()
    {
        if (handle == null)
        {
            return;
        }

        handle.anchoredPosition = _rawValue * radiusPixels;
    }

    private void OnValidate()
    {
        radiusPixels = Mathf.Max(1f, radiusPixels);
        deadZone = Mathf.Clamp(deadZone, 0f, 0.99f);
        releaseLerpSharpness = Mathf.Max(0.01f, releaseLerpSharpness);

        if (!Application.isPlaying)
        {
            _value = ApplyDeadZone(_rawValue);
            UpdateVisuals();
        }
    }
}
