using UnityEngine;

/// <summary>
/// Charged shot projectile.
///
/// This script is placed on the projectile prefab BUT it is spawned differently
/// from other weapons — WeaponController holds it at the fire point while the
/// player charges, then releases it.
///
/// While charging:
///   - The projectile sits at the fire point (kinematic, no movement).
///   - Its scale grows from 1× to maxChargeScaleMultiplier×.
///   - A charge value (0–1) is set by WeaponController each frame.
///
/// On release:
///   - WeaponController calls Release(chargeRatio).
///   - The projectile detaches from the player and begins moving.
///   - Damage and scale are locked in based on charge ratio.
/// </summary>
public class ChargedShot : ProjectileBase
{
    [Header("Charge Visuals")]
    [Tooltip("Base scale of the projectile at zero charge.")]
    public float baseScale = 1f;

    // Set by WeaponController from WeaponDefinition
    [HideInInspector] public float maxDamageMultiplier = 3f;
    [HideInInspector] public float maxScaleMultiplier  = 2.5f;

    // ─────────────────────────────────────────────────────────────────────────

    private bool  _released  = false;
    private float _chargeRatio = 0f;

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Start()
    {
        // Don't call base.Start() — we don't want auto-destroy until released
        // We start the lifetime timer only after release
    }

    protected override void Update()
    {
        if (!_released) return;
        base.Update(); // movement + off-screen cull only after release
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API  (called by WeaponController)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called every frame by WeaponController while the player holds the fire button.
    /// Updates visual scale to reflect current charge.
    /// </summary>
    public void SetCharge(float ratio)
    {
        if (_released) return;

        _chargeRatio = Mathf.Clamp01(ratio);
        float scale  = Mathf.Lerp(baseScale, baseScale * maxScaleMultiplier, _chargeRatio);
        transform.localScale = Vector3.one * scale;
    }

    /// <summary>
    /// Called by WeaponController when the player releases the fire button.
    /// Locks in damage, detaches from the player, and starts moving.
    /// </summary>
    public void Release(float chargeRatio)
    {
        if (_released) return;

        _released   = true;
        _chargeRatio = Mathf.Clamp01(chargeRatio);

        // Lock in damage
        Damage = Mathf.RoundToInt(Damage * Mathf.Lerp(1f, maxDamageMultiplier, _chargeRatio));

        // Lock in scale
        float scale = Mathf.Lerp(baseScale, baseScale * maxScaleMultiplier, _chargeRatio);
        transform.localScale = Vector3.one * scale;

        // Detach from player and start lifetime
        transform.SetParent(null);
        Destroy(gameObject, maxLifetime);
    }

    // ─────────────────────────────────────────────────────────────────────────

    protected override void OnHitEnemy(AlienHealth enemy)
    {
        if (!_released) return;
        HitAndDestroy(enemy);
    }

    protected override void OnHitAsteroid(Asteroid asteroid)
    {
        if (!_released) return;
        HitAndDestroy(asteroid);
    }
}