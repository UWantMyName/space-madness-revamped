using UnityEngine;

/// <summary>
/// Standard bullet. Travels in a straight line, deals damage on hit, destroys itself.
/// No special behaviour beyond the base class.
/// </summary>
public class BulletProjectile : ProjectileBase
{
    protected override void OnHitEnemy(AlienHealth enemy)
    {
        HitAndDestroy(enemy);
    }

    protected override void OnHitAsteroid(Asteroid asteroid)
    {
        HitAndDestroy(asteroid);
    }
}