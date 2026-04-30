using System.Collections.Generic;
using UnityEngine;

// ══════════════════════════════════════════════════════════
//  ShotgunPellet
//  On hit: finds 4-6 clear outward directions (excluding the
//  incoming direction) and spawns a ShotgunFragment in each.
// ══════════════════════════════════════════════════════════

/// <summary>
/// Shotgun pellet. Travels straight, splits into 4–6 fragments on impact.
///
/// Splitting logic:
///   - Determines the incoming direction of the pellet.
///   - Distributes candidate directions evenly around a circle.
///   - Discards any candidate that points back toward where the pellet came from
///     (within an exclusion cone of ±excludeAngle degrees).
///   - Picks a random count between minFragments and maxFragments from the
///     remaining candidates and spawns a ShotgunFragment in each direction.
/// </summary>
public class ShotgunPellet : ProjectileBase
{
    [Header("Split Settings")]
    [Tooltip("Minimum number of fragments spawned on impact.")]
    [Range(2, 8)]
    public int minFragments = 4;

    [Tooltip("Maximum number of fragments spawned on impact.")]
    [Range(2, 8)]
    public int maxFragments = 6;

    [Tooltip("Fragment prefab to instantiate. Must have a ShotgunFragment component.")]
    public GameObject fragmentPrefab;

    [Tooltip("Speed of the spawned fragments.")]
    public float fragmentSpeed = 8f;

    [Tooltip("Damage each fragment deals.")]
    public int fragmentDamage = 1;

    [Tooltip("Degrees on either side of the incoming direction to exclude from fragment spawning.")]
    [Range(10f, 90f)]
    public float excludeAngle = 45f;

    // ─────────────────────────────────────────────────────────────────────────

    protected override void OnHitEnemy(AlienHealth enemy)
    {
        enemy.TakeDamage(Damage);
        SpawnFragments();
        Destroy(gameObject);
    }

    protected override void OnHitAsteroid(Asteroid asteroid)
    {
        asteroid.TakeDamage(Damage);
        SpawnFragments();
        Destroy(gameObject);
    }

    private void SpawnFragments()
    {
        if (fragmentPrefab == null)
        {
            Debug.LogWarning("[ShotgunPellet] No fragmentPrefab assigned.", this);
            return;
        }

        // The direction the pellet came FROM — we exclude candidates near this
        Vector2 incomingDir = -direction.normalized;
        float   incomingAngle = Mathf.Atan2(incomingDir.y, incomingDir.x) * Mathf.Rad2Deg;

        // Build candidate directions evenly spaced around the full circle
        int candidateCount = 12;
        var candidates = new List<Vector2>(candidateCount);

        for (int i = 0; i < candidateCount; i++)
        {
            float angleDeg = (360f / candidateCount) * i;
            float rad      = angleDeg * Mathf.Deg2Rad;
            var   dir      = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            // Exclude directions within excludeAngle of the incoming direction
            float diff = Mathf.Abs(Mathf.DeltaAngle(angleDeg, incomingAngle));
            if (diff <= excludeAngle) continue;

            candidates.Add(dir);
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[ShotgunPellet] No valid fragment directions found.", this);
            return;
        }

        // Shuffle candidates so selection isn't always front-biased
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int count = Random.Range(
            Mathf.Min(minFragments, candidates.Count),
            Mathf.Min(maxFragments, candidates.Count) + 1);

        for (int i = 0; i < count; i++)
        {
            var go       = Instantiate(fragmentPrefab, transform.position, Quaternion.identity);
            var fragment = go.GetComponent<ShotgunFragment>();

            if (fragment == null)
            {
                Debug.LogWarning("[ShotgunPellet] fragmentPrefab missing ShotgunFragment component.", go);
                Destroy(go);
                continue;
            }

            fragment.direction = candidates[i];
            fragment.speed     = fragmentSpeed;
            fragment.Damage    = fragmentDamage;
        }
    }
}