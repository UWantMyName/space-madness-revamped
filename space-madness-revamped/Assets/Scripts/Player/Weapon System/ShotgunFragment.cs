using UnityEngine;

/// <summary>
/// Fragment spawned by ShotgunPellet on impact. No further splitting.
/// </summary>
public class ShotgunFragment : ProjectileBase
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