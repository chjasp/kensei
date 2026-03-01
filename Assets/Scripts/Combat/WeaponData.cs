using UnityEngine;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Kensei/WeaponData")]
public sealed class WeaponData : ScriptableObject
{
    public string weaponName;
    public float baseDamage = 20f;
    public float attackRange = 2f;
    public float comboResetTime = 0.8f;
    public float parryWindowDuration = 0.4f;
    public DamageType damageType = DamageType.LightMelee;
}
