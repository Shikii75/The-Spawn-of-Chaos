public interface IDamageable
{
    /// <summary>
    /// Apply the requested amount of damage to this object.
    /// </summary>
    /// <param name="amount">Damage to apply.</param>
    void TakeDamage(int amount);
}
