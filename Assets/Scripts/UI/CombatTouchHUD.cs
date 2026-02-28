using System;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class CombatTouchHUD : MonoBehaviour
{
    [SerializeField] private MeleeAttackSystem meleeAttackSystem;
    [SerializeField] private CombatController combatController;
    [SerializeField] private CombatPointerDownButton attackButton;
    [SerializeField] private CombatPointerDownButton parryButton;

    private bool _isSubscribed;

    public void SetReferences(
        MeleeAttackSystem meleeAttackSystemReference,
        CombatController combatControllerReference,
        CombatPointerDownButton attackButtonReference,
        CombatPointerDownButton parryButtonReference)
    {
        if (meleeAttackSystemReference != null)
        {
            meleeAttackSystem = meleeAttackSystemReference;
        }

        if (combatControllerReference != null)
        {
            combatController = combatControllerReference;
        }

        if (attackButtonReference != null)
        {
            attackButton = attackButtonReference;
        }

        if (parryButtonReference != null)
        {
            parryButton = parryButtonReference;
        }

        Unsubscribe();
        Subscribe();
    }

    public void HandleAttackPointerDown()
    {
        ResolveReferences();
        meleeAttackSystem?.RequestAttack();
    }

    public void HandleParryPointerDown()
    {
        ResolveReferences();
        combatController?.RequestParry();
    }

    private void Awake()
    {
        ResolveReferences();
        Subscribe();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void ResolveReferences()
    {
        if (meleeAttackSystem == null || combatController == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                if (meleeAttackSystem == null)
                {
                    meleeAttackSystem = player.GetComponent<MeleeAttackSystem>();
                }

                if (combatController == null)
                {
                    combatController = player.GetComponent<CombatController>();
                }
            }
        }

        if (attackButton == null)
        {
            Transform attackTransform = transform.Find("AttackButton");
            if (attackTransform != null)
            {
                attackButton = attackTransform.GetComponent<CombatPointerDownButton>();
            }
        }

        if (parryButton == null)
        {
            Transform parryTransform = transform.Find("ParryButton");
            if (parryTransform != null)
            {
                parryButton = parryTransform.GetComponent<CombatPointerDownButton>();
            }
        }
    }

    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        bool subscribedAny = false;
        if (attackButton != null)
        {
            attackButton.OnPointerDownEvent += HandleAttackPointerDown;
            subscribedAny = true;
        }

        if (parryButton != null)
        {
            parryButton.OnPointerDownEvent += HandleParryPointerDown;
            subscribedAny = true;
        }

        _isSubscribed = subscribedAny;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        if (attackButton != null)
        {
            attackButton.OnPointerDownEvent -= HandleAttackPointerDown;
        }

        if (parryButton != null)
        {
            parryButton.OnPointerDownEvent -= HandleParryPointerDown;
        }

        _isSubscribed = false;
    }
}

[DisallowMultipleComponent]
public sealed class CombatPointerDownButton : MonoBehaviour, IPointerDownHandler
{
    public event Action OnPointerDownEvent;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null)
        {
            return;
        }

        OnPointerDownEvent?.Invoke();
    }
}
