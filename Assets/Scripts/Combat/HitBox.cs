using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HitBox : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private WeaponData equippedWeaponData;
    [SerializeField] private float baseDamage = 20f;
    [SerializeField] private DamageType damageType = DamageType.LightMelee;

    [Header("Filtering")]
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField] private Transform sourceRoot;

    [Header("References")]
    [SerializeField] private Collider hitCollider;
    [SerializeField] private GameObject sourceOverride;

    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();
    private bool _isHitBoxActive;

    public event Action<DamageInfo> OnHitConfirmed;

    public WeaponData EquippedWeaponData => equippedWeaponData;

    public void SetWeaponData(WeaponData weaponData)
    {
        equippedWeaponData = weaponData;
    }

    public void SetTargetLayers(LayerMask layers)
    {
        targetLayers = layers;
    }

    public void SetSourceRoot(Transform sourceRootTransform)
    {
        if (sourceRootTransform != null)
        {
            sourceRoot = sourceRootTransform;
        }
    }

    public void EnableHitBox()
    {
        ResolveReferences();
        _hitTargets.Clear();
        _isHitBoxActive = true;

        if (hitCollider != null)
        {
            hitCollider.isTrigger = true;
            hitCollider.enabled = true;
        }
    }

    public void DisableHitBox()
    {
        _isHitBoxActive = false;

        if (hitCollider != null)
        {
            hitCollider.enabled = false;
        }
    }

    private void Awake()
    {
        ResolveReferences();
        DisableHitBox();
    }

    private void Reset()
    {
        ResolveReferences();
        DisableHitBox();
    }

    private void OnValidate()
    {
        baseDamage = Mathf.Max(0f, baseDamage);
        if (!Application.isPlaying)
        {
            ResolveReferences();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isHitBoxActive || other == null || !IsInTargetLayer(other.gameObject.layer))
        {
            return;
        }

        if (sourceRoot != null && other.transform.IsChildOf(sourceRoot))
        {
            return;
        }

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null || !damageable.IsAlive || _hitTargets.Contains(damageable))
        {
            return;
        }

        Component damageableComponent = damageable as Component;
        if (sourceRoot != null && damageableComponent != null && damageableComponent.transform.IsChildOf(sourceRoot))
        {
            return;
        }

        _hitTargets.Add(damageable);

        DamageInfo damageInfo = BuildDamageInfo(other);
        damageable.TakeDamage(damageInfo);
        OnHitConfirmed?.Invoke(damageInfo);
    }

    private void ResolveReferences()
    {
        if (hitCollider == null)
        {
            hitCollider = GetComponent<Collider>();
        }

        if (sourceRoot == null)
        {
            sourceRoot = transform.root;
        }

        if (hitCollider != null)
        {
            hitCollider.isTrigger = true;
        }
    }

    private DamageInfo BuildDamageInfo(Collider targetCollider)
    {
        Transform resolvedSourceTransform = sourceRoot != null ? sourceRoot : transform.root;
        Vector3 sourcePosition = resolvedSourceTransform != null ? resolvedSourceTransform.position : transform.position;

        Vector3 hitDirection = targetCollider.transform.position - sourcePosition;
        if (hitDirection.sqrMagnitude < 0.0001f)
        {
            hitDirection = transform.forward;
        }

        hitDirection.Normalize();

        GameObject resolvedSourceObject = sourceOverride != null
            ? sourceOverride
            : (resolvedSourceTransform != null ? resolvedSourceTransform.gameObject : gameObject);

        return new DamageInfo
        {
            Amount = ResolveDamageAmount(),
            HitPoint = targetCollider.ClosestPoint(transform.position),
            HitDirection = hitDirection,
            Source = resolvedSourceObject,
            Type = ResolveDamageType()
        };
    }

    private float ResolveDamageAmount()
    {
        if (equippedWeaponData != null && equippedWeaponData.baseDamage > 0f)
        {
            return equippedWeaponData.baseDamage;
        }

        return baseDamage;
    }

    private DamageType ResolveDamageType()
    {
        if (equippedWeaponData != null)
        {
            return equippedWeaponData.damageType;
        }

        return damageType;
    }

    private bool IsInTargetLayer(int layer)
    {
        return (targetLayers.value & (1 << layer)) != 0;
    }
}
