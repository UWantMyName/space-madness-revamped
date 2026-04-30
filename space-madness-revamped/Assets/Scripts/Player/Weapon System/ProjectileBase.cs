using UnityEngine;

/// <summary>
/// Abstract base class for all player projectiles.
///
/// Subclasses implement:
///   OnHitEnemy(AlienHealth)   — called when the projectile hits an alien.
///   OnHitAsteroid(Asteroid)   — called when the projectile hits an asteroid.
///
/// Base class handles:
///   - Movement in the assigned direction each frame.
///   - Off-screen culling.
///   - Collision detection (trigger).
///   - Self-destruction after a max lifetime.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public abstract class ProjectileBase : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Movement")]
    [Tooltip("Travel speed in world units per second.")]
    public float speed = 12f;

    [Tooltip("Travel direction in world space. Set by WeaponController at spawn time.")]
    public Vector2 direction = Vector2.up;

    [Header("Lifetime")]
    [Tooltip("Seconds before the projectile destroys itself regardless of collision.")]
    public float maxLifetime = 5f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Public State
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Damage this projectile deals. Set by WeaponController after instantiation.</summary>
    public int Damage { get; set; } = 1;

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        var rb          = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType     = RigidbodyType2D.Kinematic;

        var col   = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    protected virtual void Start()
    {
        Destroy(gameObject, maxLifetime);
    }

    protected virtual void Update()
    {
        transform.position += (Vector3)(direction.normalized * speed * Time.deltaTime);

        if (IsOffScreen())
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore other projectiles and the player
        if (other.CompareTag("Player"))   return;
        if (other.CompareTag("Projectile")) return;

        var alienHealth = other.GetComponent<AlienHealth>();
        if (alienHealth != null && !alienHealth.IsDead)
        {
            OnHitEnemy(alienHealth);
            return;
        }

        var asteroid = other.GetComponent<Asteroid>();
        if (asteroid != null && !asteroid.IsDead)
        {
            OnHitAsteroid(asteroid);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Abstract
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Called when this projectile enters an alien's trigger collider.</summary>
    protected abstract void OnHitEnemy(AlienHealth enemy);

    /// <summary>Called when this projectile enters an asteroid's trigger collider.</summary>
    protected abstract void OnHitAsteroid(Asteroid asteroid);

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    protected bool IsOffScreen()
    {
        if (Camera.main == null) return false;
        float halfH  = Camera.main.orthographicSize;
        float halfW  = halfH * Camera.main.aspect;
        float margin = 1f;
        Vector3 pos  = transform.position;
        return pos.x < -halfW - margin || pos.x > halfW + margin ||
               pos.y < -halfH - margin || pos.y > halfH + margin;
    }

    /// <summary>Deals damage and destroys self. Call from subclasses.</summary>
    protected void HitAndDestroy(AlienHealth enemy)
    {
        enemy.TakeDamage(Damage);
        Destroy(gameObject);
    }

    protected void HitAndDestroy(Asteroid asteroid)
    {
        asteroid.TakeDamage(Damage);
        Destroy(gameObject);
    }
}