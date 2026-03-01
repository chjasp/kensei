using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class DebugCombatInput : MonoBehaviour
{
    [SerializeField] private MeleeAttackSystem meleeAttackSystem;

    public void SetAttackSystem(MeleeAttackSystem attackSystem)
    {
        if (attackSystem != null)
        {
            meleeAttackSystem = attackSystem;
        }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();
        if (meleeAttackSystem == null)
        {
            return;
        }

        if (!WasAttackPressedThisFrame())
        {
            return;
        }

        meleeAttackSystem.RequestAttack();
    }

    private bool WasAttackPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !IsPointerOverUi())
        {
            return true;
        }

        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
#else
        return (Input.GetMouseButtonDown(0) && !IsPointerOverUi()) || Input.GetKeyDown(KeyCode.Space);
#endif
    }

    private static bool IsPointerOverUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        return eventSystem.IsPointerOverGameObject()
               || eventSystem.IsPointerOverGameObject(PointerInputModule.kMouseLeftId);
    }

    private void ResolveReferences()
    {
        if (meleeAttackSystem == null)
        {
            meleeAttackSystem = GetComponent<MeleeAttackSystem>();
        }
    }
}
