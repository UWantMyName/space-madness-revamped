using System.Collections;
using UnityEngine;

/// <summary>
/// Manages player hull HP and shield with the following rules:
///
/// SHIELD
///   - Absorbs incoming damage at 50% of face value (float precision).
///   - While the shield is up, hull HP cannot be damaged.
///   - If the player takes no damage for 5 seconds, the shield regenerates slowly.
///   - If the shield is broken (reaches 0), a 10-second recharge begins.
///   - After recharging, the shield boots back up at 50% of its max value.
///   - From that point it continues regenerating until full.
///
/// HULL
///   - Only takes damage when the shield is fully broken.
///   - Cannot regenerate on its own.
///   - Reaching 0 HP fires OnDeath.
///
/// EVENTS (subscribe to drive the HUD)
///   OnHullChanged(int current, int max)       — fired on any hull HP change.
///   OnShieldChanged(float current, float max) — fired on any shield value change.
///   OnShieldBroken                            — fired the moment the shield reaches 0.
///   OnShieldBooted                            — fired when the shield reboots at 50%.
///   OnDeath                                   — fired when hull HP reaches 0.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Hull")]
    [Tooltip("Maximum hull HP.")]
    [Min(1)]
    public int maxHP = 10;

    [Header("Shield")]
    [Tooltip("Maximum shield HP. Should be less than hull maxHP.")]
    [Min(1)]
    public int maxShield = 6;

    [Tooltip("Seconds with no damage before shield regen starts.")]
    [Min(0f)]
    public float regenDelay = 5f;

    [Tooltip("Shield HP restored per second during regeneration.")]
    [Min(0.01f)]
    public float regenRate = 1.5f;

    [Tooltip("Seconds before a broken shield reboots.")]
    [Min(0f)]
    public float rechargeDelay = 10f;

    [Header("Invincibility Frames")]
    [Tooltip("Seconds of invincibility after taking hull damage.")]
    [Min(0f)]
    public float invincibilityDuration = 1.5f;

    [Tooltip("Flashes per second during invincibility.")]
    [Min(1f)]
    public float flashRate = 10f;

    [Tooltip("Minimum alpha during each flash dip.")]
    [Range(0f, 1f)]
    public float flashMinAlpha = 0.15f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Fired on any hull HP change. PlayerHUD subscribes to update the health bar.</summary>
    public event System.Action<int, int>     OnHullChanged;

    /// <summary>Fired on any shield value change. PlayerHUD subscribes to update the shield bar.</summary>
    public event System.Action<float, float> OnShieldChanged;

    /// <summary>Fired the moment the shield breaks (reaches 0).</summary>
    public event System.Action               OnShieldBroken;

    /// <summary>Fired when the shield reboots after its recharge delay.</summary>
    public event System.Action               OnShieldBooted;

    /// <summary>Fired when hull HP reaches 0. Subscribe here to trigger Game Over.</summary>
    public event System.Action               OnDeath;

    // ─────────────────────────────────────────────────────────────────────────
    //  Public State
    // ─────────────────────────────────────────────────────────────────────────

    public int   CurrentHP     { get; private set; }
    public float CurrentShield { get; private set; }
    public bool  ShieldActive  { get; private set; }
    public bool  IsInvincible  { get; private set; }
    public bool  IsDead        { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private
    // ─────────────────────────────────────────────────────────────────────────

    private SpriteRenderer _renderer;
    private Color          _originalColor;

    private Coroutine      _invincibilityCoroutine;
    private Coroutine      _regenCoroutine;
    private Coroutine      _rechargeCoroutine;

    private float          _timeSinceLastDamage;
    private bool           _regenRunning;

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _renderer      = GetComponent<SpriteRenderer>();
        _originalColor = _renderer != null ? _renderer.color : Color.white;
    }

    private void Start()
    {
        ResetHealth();
    }

    private void Update()
    {
        if (IsDead) return;

        // Count up toward the regen delay threshold when no damage is incoming
        if (ShieldActive && CurrentShield < maxShield && !_regenRunning)
        {
            _timeSinceLastDamage += Time.deltaTime;

            if (_timeSinceLastDamage >= regenDelay)
                StartRegen();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Entry point for all damage sources (asteroids, enemy bullets, etc.).
    /// Shield absorbs hits first. Hull only takes damage when shield is broken.
    /// Ignored entirely if the player is dead.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        // Any hit interrupts regen and resets the regen delay timer
        StopRegen();
        _timeSinceLastDamage = 0f;

        if (ShieldActive)
            ApplyShieldDamage(amount);
        else
            ApplyHullDamage(amount);
    }

    /// <summary>Restores hull and shield to full. Safe to call at any time (restart, etc.).</summary>
    public void ResetHealth()
    {
        StopAllCoroutines();

        IsDead               = false;
        IsInvincible         = false;
        ShieldActive         = true;
        _regenRunning        = false;
        _timeSinceLastDamage = 0f;

        CurrentHP     = maxHP;
        CurrentShield = maxShield;

        if (_renderer != null)
            _renderer.color = _originalColor;

        // Notify HUD of starting state
        OnHullChanged?.Invoke(CurrentHP, maxHP);
        OnShieldChanged?.Invoke(CurrentShield, maxShield);

        enabled = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Shield Damage
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyShieldDamage(int incomingAmount)
    {
        // Shield absorbs at 50% of incoming damage (float precision)
        float shieldDamage = incomingAmount * 0.5f;
        CurrentShield = Mathf.Max(0f, CurrentShield - shieldDamage);

        OnShieldChanged?.Invoke(CurrentShield, maxShield);

        if (CurrentShield <= 0f)
            BreakShield();
    }

    private void BreakShield()
    {
        ShieldActive  = false;
        CurrentShield = 0f;

        OnShieldBroken?.Invoke();
        OnShieldChanged?.Invoke(0f, maxShield);

        Debug.Log("[PlayerHealth] Shield broken — recharge begins.");
        _rechargeCoroutine = StartCoroutine(RechargeRoutine());
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Hull Damage
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyHullDamage(int amount)
    {
        // Invincibility frames protect hull only (shield hits are always registered)
        if (IsInvincible) return;

        CurrentHP = Mathf.Max(0, CurrentHP - amount);
        OnHullChanged?.Invoke(CurrentHP, maxHP);

        if (CurrentHP <= 0)
            Die();
        else
            StartInvincibility();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Shield Regen
    // ─────────────────────────────────────────────────────────────────────────

    private void StartRegen()
    {
        if (_regenRunning) return;
        _regenCoroutine = StartCoroutine(RegenRoutine());
    }

    private void StopRegen()
    {
        if (_regenCoroutine != null)
        {
            StopCoroutine(_regenCoroutine);
            _regenCoroutine = null;
        }
        _regenRunning = false;
    }

    private IEnumerator RegenRoutine()
    {
        _regenRunning = true;

        while (ShieldActive && CurrentShield < maxShield)
        {
            CurrentShield = Mathf.Min(maxShield, CurrentShield + regenRate * Time.deltaTime);
            OnShieldChanged?.Invoke(CurrentShield, maxShield);
            yield return null;
        }

        _regenRunning   = false;
        _regenCoroutine = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Shield Recharge (after broken)
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator RechargeRoutine()
    {
        yield return new WaitForSeconds(rechargeDelay);

        // Boot the shield immediately at 50% — regen will carry it to full
        CurrentShield = maxShield * 0.5f;
        ShieldActive  = true;

        OnShieldBooted?.Invoke();
        OnShieldChanged?.Invoke(CurrentShield, maxShield);

        // Reset timer so regen doesn't fire instantly on boot
        _timeSinceLastDamage = 0f;

        Debug.Log("[PlayerHealth] Shield rebooted at 50%.");
        _rechargeCoroutine = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Invincibility Frames
    // ─────────────────────────────────────────────────────────────────────────

    private void StartInvincibility()
    {
        if (_invincibilityCoroutine != null)
            StopCoroutine(_invincibilityCoroutine);

        _invincibilityCoroutine = StartCoroutine(InvincibilityRoutine());
    }

    private IEnumerator InvincibilityRoutine()
    {
        IsInvincible = true;
        float elapsed    = 0f;
        float flashPeriod = 1f / flashRate;

        while (elapsed < invincibilityDuration)
        {
            float cyclePos = (elapsed % flashPeriod) / flashPeriod;
            float alpha    = Mathf.Lerp(flashMinAlpha, 1f, Mathf.PingPong(cyclePos * 2f, 1f));

            if (_renderer != null)
            {
                Color c = _originalColor;
                c.a = alpha;
                _renderer.color = c;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_renderer != null)
            _renderer.color = _originalColor;

        IsInvincible            = false;
        _invincibilityCoroutine = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Death
    // ─────────────────────────────────────────────────────────────────────────

    private void Die()
    {
        IsDead = true;
        StopAllCoroutines();

        if (_renderer != null)
            _renderer.color = _originalColor;

        OnDeath?.Invoke();
        enabled = false;
    }
}