using System.Collections;
using UnityEngine;

/// <summary>
/// Handles a single asteroid's movement, health, and collision.
///
/// Lifecycle:
///   1. AsteroidSpawner instantiates the prefab and calls Initialise().
///   2. The asteroid drifts in a straight line until it leaves the screen or is destroyed.
///   3. On bullet hit  → TakeDamage(1). When HP reaches 0, fire OnDestroyed and destroy.
///   4. On player hit  → deal damageToPlayer to the player, then destroy immediately.
///
/// Notes:
///   - Requires a PlayerHealth component on the Player GameObject (tagged "Player").
///   - Requires a Bullet component on bullet GameObjects (same pattern as AlienHealth).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class Asteroid : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired just before the asteroid is destroyed (by bullets or player contact).
    /// AsteroidSpawner subscribes to this to track level completion.
    /// </summary>
    public event System.Action OnDestroyed;

    // ─────────────────────────────────────────────────────────────────────────
    //  Public State
    // ─────────────────────────────────────────────────────────────────────────

    public AsteroidDefinition Definition  { get; private set; }
    public int                CurrentHP   { get; private set; }
    public bool               IsDead      { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private
    // ─────────────────────────────────────────────────────────────────────────

    private Vector2         _direction;
    private SpriteRenderer  _renderer;
    private float           _rotationDirection; // +1 or -1

    // ─────────────────────────────────────────────────────────────────────────
    //  Initialisation  (called by AsteroidSpawner right after Instantiate)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets up the asteroid with a definition and a travel direction.
    /// Must be called before the first Update tick.
    /// </summary>
    public void Initialise(AsteroidDefinition definition, Vector2 direction)
    {
        Definition  = definition;
        CurrentHP   = definition.maxHP;
        _direction  = direction.normalized;

        _renderer        = GetComponent<SpriteRenderer>();
        _renderer.sprite = definition.sprite;

        // Randomise rotation direction so asteroids don't all spin the same way
        _rotationDirection = Random.value > 0.5f ? 1f : -1f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (IsDead) return;

        // Straight-line movement
        transform.position += (Vector3)(_direction * Definition.speed * Time.deltaTime);

        // Visual rotation
        if (Definition.rotationSpeed > 0f)
            transform.Rotate(0f, 0f, _rotationDirection * Definition.rotationSpeed * Time.deltaTime);

        // Destroy if off-screen (failsafe — level shouldn't complete until all are spawned + dead,
        // but this prevents orphaned asteroids if the level ends another way)
        if (IsOffScreen())
            DestroyAsteroid();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsDead) return;

        // Hit by player bullet
        if (other.GetComponent<Bullet>() != null)
        {
            Destroy(other.gameObject);
            TakeDamage(1);
            return;
        }

        // Hit the player
        if (other.CompareTag("Player"))
        {
            var playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null)
                playerHealth.TakeDamage(Definition.damageToPlayer);
            else
                Debug.LogWarning("[Asteroid] Player is missing a PlayerHealth component.", this);

            DestroyAsteroid();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Damage
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Deals damage to the asteroid. Safe to call multiple times — ignores hits after death.</summary>
    public void TakeDamage(int amount = 1)
    {
        if (IsDead) return;

        CurrentHP -= amount;

        if (CurrentHP <= 0)
        {
            CurrentHP = 0;
            DestroyAsteroid();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Destruction
    // ─────────────────────────────────────────────────────────────────────────

    private void DestroyAsteroid()
    {
        if (IsDead) return;

        IsDead = true;
        OnDestroyed?.Invoke();
        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private bool IsOffScreen()
    {
        if (Camera.main == null) return false;

        float halfH  = Camera.main.orthographicSize;
        float halfW  = halfH * Camera.main.aspect;
        float margin = 1f; // extra world units of tolerance before culling

        Vector3 pos = transform.position;
        return pos.x < -halfW - margin ||
               pos.x >  halfW + margin ||
               pos.y < -halfH - margin ||
               pos.y >  halfH + margin;
    }
}