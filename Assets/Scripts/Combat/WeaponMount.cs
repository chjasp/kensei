using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponMount : MonoBehaviour
{
    [SerializeField] private Transform handBone;
    [SerializeField] private GameObject weaponPrefab;
    [SerializeField] private Vector3 positionOffset;
    [SerializeField] private Vector3 rotationOffset;
    [SerializeField] private Transform weaponTransform;

    public Transform WeaponTransform => weaponTransform;
    public Transform HandBone => handBone;
    public GameObject WeaponPrefab => weaponPrefab;

    public void SetMountConfiguration(
        Transform handBoneReference,
        GameObject weaponPrefabReference,
        Vector3 localPositionOffset,
        Vector3 localRotationOffset)
    {
        handBone = handBoneReference;
        weaponPrefab = weaponPrefabReference;
        positionOffset = localPositionOffset;
        rotationOffset = localRotationOffset;
    }

    public Transform EnsureWeaponMounted()
    {
        if (handBone == null || weaponPrefab == null)
        {
            return weaponTransform;
        }

        if (weaponTransform == null || weaponTransform.parent != handBone)
        {
            weaponTransform = ResolveExistingWeaponChild();
        }

        if (weaponTransform == null)
        {
            GameObject instance = Instantiate(weaponPrefab, handBone, false);
            instance.name = weaponPrefab.name;
            weaponTransform = instance.transform;
        }

        ApplyOffsets();
        return weaponTransform;
    }

    private void Awake()
    {
        EnsureWeaponMounted();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        if (weaponTransform != null && handBone != null && weaponTransform.parent == handBone)
        {
            ApplyOffsets();
        }
    }

    private Transform ResolveExistingWeaponChild()
    {
        if (handBone == null)
        {
            return null;
        }

        if (weaponTransform != null && weaponTransform.parent == handBone)
        {
            return weaponTransform;
        }

        string expectedName = weaponPrefab != null ? weaponPrefab.name : string.Empty;
        int childCount = handBone.childCount;
        for (int index = 0; index < childCount; index++)
        {
            Transform child = handBone.GetChild(index);
            if (child == null)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(expectedName) &&
                (child.name == expectedName || child.name.StartsWith(expectedName + " (")))
            {
                return child;
            }
        }

        return null;
    }

    private void ApplyOffsets()
    {
        if (weaponTransform == null)
        {
            return;
        }

        weaponTransform.localPosition = positionOffset;
        weaponTransform.localRotation = Quaternion.Euler(rotationOffset);
    }
}
