using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class CombatFoundationTest : MonoBehaviour
{
    [SerializeField] private HealthSystem playerHealth;
    [SerializeField] private float testDamageAmount = 15f;
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private Key triggerKey = Key.T;
#else
    [SerializeField] private KeyCode triggerKey = KeyCode.T;
#endif

    public void SetPlayerHealthForTesting(HealthSystem healthSystem)
    {
        if (healthSystem != null)
        {
            playerHealth = healthSystem;
        }
    }

    private void OnEnable()
    {
        ResolvePlayerHealthIfNeeded();
        SubscribeToHealthEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromHealthEvents();
    }

    private void Update()
    {
        if (!WasTriggerPressedThisFrame())
        {
            return;
        }

        ResolvePlayerHealthIfNeeded();
        if (playerHealth == null)
        {
            Debug.LogWarning("CombatFoundationTest could not resolve a player HealthSystem.", this);
            return;
        }

        DamageInfo damage = new DamageInfo
        {
            Amount = testDamageAmount,
            HitPoint = playerHealth.transform.position + (Vector3.up * 1f),
            HitDirection = -playerHealth.transform.forward,
            Source = gameObject,
            Type = DamageType.LightMelee
        };

        playerHealth.TakeDamage(damage);
        Debug.Log($"CombatFoundationTest applied {testDamageAmount:0.##} damage.", this);
    }

    private bool WasTriggerPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        return keyboard[triggerKey].wasPressedThisFrame;
#else
        return Input.GetKeyDown(triggerKey);
#endif
    }

    private void ResolvePlayerHealthIfNeeded()
    {
        if (playerHealth != null)
        {
            return;
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponent<HealthSystem>();
        }
    }

    private void SubscribeToHealthEvents()
    {
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.OnDamaged -= HandleDamaged;
        playerHealth.OnHealthChanged -= HandleHealthChanged;
        playerHealth.OnDamaged += HandleDamaged;
        playerHealth.OnHealthChanged += HandleHealthChanged;
    }

    private void UnsubscribeFromHealthEvents()
    {
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.OnDamaged -= HandleDamaged;
        playerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleDamaged(DamageInfo damage)
    {
        Debug.Log(
            $"CombatFoundationTest OnDamaged -> amount: {damage.Amount:0.##}, type: {damage.Type}",
            this);
    }

    private void HandleHealthChanged(float current, float max)
    {
        Debug.Log($"CombatFoundationTest OnHealthChanged -> {current:0.##}/{max:0.##}", this);
    }
}
