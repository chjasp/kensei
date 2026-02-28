using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class TouchLookDragRegion : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private bool allowMouseInEditor;

    private int _activePointerId = int.MinValue;
    private Vector2 _lastPointerPosition;
    private Vector2 _accumulatedDelta;

    public Vector2 ConsumeDelta()
    {
        Vector2 delta = _accumulatedDelta;
        _accumulatedDelta = Vector2.zero;
        return delta;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsAcceptedPointer(eventData))
        {
            return;
        }

        if (_activePointerId != int.MinValue && _activePointerId != eventData.pointerId)
        {
            return;
        }

        _activePointerId = eventData.pointerId;
        _lastPointerPosition = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsAcceptedPointer(eventData) || eventData.pointerId != _activePointerId)
        {
            return;
        }

        Vector2 currentPosition = eventData.position;
        _accumulatedDelta += currentPosition - _lastPointerPosition;
        _lastPointerPosition = currentPosition;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData == null || eventData.pointerId != _activePointerId)
        {
            return;
        }

        _activePointerId = int.MinValue;
    }

    private void OnDisable()
    {
        _activePointerId = int.MinValue;
        _accumulatedDelta = Vector2.zero;
    }

    private bool IsAcceptedPointer(PointerEventData eventData)
    {
        if (eventData == null)
        {
            return false;
        }

        if (eventData.pointerId >= 0)
        {
            return true;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        if (allowMouseInEditor &&
            (eventData.pointerId == PointerInputModule.kMouseLeftId ||
             eventData.pointerId == PointerInputModule.kMouseRightId ||
             eventData.pointerId == PointerInputModule.kMouseMiddleId))
        {
            return true;
        }
#endif

        return false;
    }
}
