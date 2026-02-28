using UnityEngine;

[DisallowMultipleComponent]
public sealed class HurtBox : MonoBehaviour
{
    [SerializeField] private HealthSystem healthSystem;

    public HealthSystem HealthSystem => healthSystem;

    public void SetHealthSystem(HealthSystem healthSystemReference)
    {
        if (healthSystemReference != null)
        {
            healthSystem = healthSystemReference;
        }
    }

    private void Awake()
    {
        ResolveHealthSystem();
    }

    private void Reset()
    {
        ResolveHealthSystem();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            ResolveHealthSystem();
        }
    }

    private void ResolveHealthSystem()
    {
        if (healthSystem == null)
        {
            healthSystem = GetComponentInParent<HealthSystem>();
        }
    }
}
