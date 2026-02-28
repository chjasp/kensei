public interface IDamageable
{
    void TakeDamage(DamageInfo damage);
    bool IsAlive { get; }
}
