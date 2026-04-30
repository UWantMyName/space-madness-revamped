using UnityEngine;

/// <summary>
/// Homing missile. Gradually rotates toward the closest living enemy each frame.
///
/// Tracking behaviour:
///   - Searches for the closest AlienHealth in the scene every scanInterval seconds.
///   - Rotates its travel direction toward the target at turnSpeed degrees/second.
///   - If no target is found, continues straight.
///   - Has a maxTurnAngle cap so it can't do impossible U-turns.
/// </summary>
public class HomingMissile : ProjectileBase
{
    [Header("Homing")]
    [Tooltip("Degrees per second the missile can turn toward its target.")]
    [Range(30f, 720f)]
    public float turnSpeed = 120f;

    [Tooltip("Maximum total degrees the missile is allowed to turn from its launch direction.\n" +
             "Prevents missiles from doing U-turns and chasing enemies behind the player.")]
    [Range(30f, 180f)]
    public float maxTurnAngle = 135f;

    [Tooltip("Seconds between target rescans. Lower = more responsive but more expensive.")]
    [Min(0.05f)]
    public float scanInterval = 0.15f;

    // ─────────────────────────────────────────────────────────────────────────

    private Transform _target;
    private float     _scanTimer;
    private Vector2   _launchDirection;
    private float     _totalTurnDegrees;

    protected override void Start()
    {
        base.Start();
        _launchDirection = direction.normalized;
        ScanForTarget();
    }

    protected override void Update()
    {
        // Rescan periodically
        _scanTimer += Time.deltaTime;
        if (_scanTimer >= scanInterval)
        {
            _scanTimer = 0f;
            ScanForTarget();
        }

        // Steer toward target if we have one
        if (_target != null)
            SteerTowardTarget();

        // Move
        base.Update();
    }

    private void SteerTowardTarget()
    {
        Vector2 toTarget     = ((Vector2)_target.position - (Vector2)transform.position).normalized;
        float   targetAngle  = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        float   currentAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        float   delta        = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnSpeed * Time.deltaTime);

        // Check we haven't exceeded maxTurnAngle from launch direction
        float launchAngle    = Mathf.Atan2(_launchDirection.y, _launchDirection.x) * Mathf.Rad2Deg;
        float turnedSoFar    = Mathf.Abs(Mathf.DeltaAngle(launchAngle, delta));

        if (turnedSoFar > maxTurnAngle)
            return; // Don't apply the turn — missile goes straight

        float rad = delta * Mathf.Deg2Rad;
        direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        // Face the travel direction visually
        transform.rotation = Quaternion.Euler(0f, 0f, delta - 90f);
    }

    private void ScanForTarget()
    {
        // Find all living aliens
        var aliens  = Object.FindObjectsByType<AlienHealth>(FindObjectsSortMode.None);
        float closest = float.MaxValue;
        _target       = null;

        foreach (var alien in aliens)
        {
            if (alien.IsDead) continue;

            float dist = Vector2.Distance(transform.position, alien.transform.position);
            if (dist < closest)
            {
                closest = dist;
                _target = alien.transform;
            }
        }
    }

    protected override void OnHitEnemy(AlienHealth enemy)
    {
        HitAndDestroy(enemy);
    }

    protected override void OnHitAsteroid(Asteroid asteroid)
    {
        HitAndDestroy(asteroid);
    }
}