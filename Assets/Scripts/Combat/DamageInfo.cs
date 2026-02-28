using UnityEngine;

public struct DamageInfo
{
    public float Amount;
    public Vector3 HitPoint;
    public Vector3 HitDirection;
    public GameObject Source;
    public DamageType Type;
}

public enum DamageType
{
    LightMelee,
    HeavyMelee,
    StealthKill,
    Environmental
}
