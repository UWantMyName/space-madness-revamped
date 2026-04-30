using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the player's weapon slots.
///
/// Responsibilities:
///   - Holds up to 4 WeaponDefinition slots.
///   - Spawns projectiles at the weapon's configured fire point offsets.
///   - Handles fire rate cooldown per weapon.
///   - Handles charged shot: spawns it at the fire point while held,
///     calls Release() when the button is lifted.
///   - Notifies WeaponHUDController when the active weapon changes.
///
/// Attach to the Player GameObject.
/// Assign weapon definitions in the Inspector.
/// </summary>
public class WeaponController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Weapon Slots (up to 4)")]
    public WeaponDefinition slot1;
    public WeaponDefinition slot2;
    public WeaponDefinition slot3;
    public WeaponDefinition slot4;

    [Header("References")]
    [Tooltip("WeaponHUDController on the Canvas HUD. Notified on weapon switch.")]
    public WeaponHUDController weaponHUD;

    [Header("Fire Input")]
    [Tooltip("Key used to fire / hold-to-charge.")]
    public KeyCode fireKey = KeyCode.Space;

    // ─────────────────────────────────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Fired when the active weapon changes. Parameter is the 0-based slot index.</summary>
    public event System.Action<int> OnWeaponChanged;

    // ─────────────────────────────────────────────────────────────────────────
    //  Public State
    // ─────────────────────────────────────────────────────────────────────────

    public WeaponDefinition ActiveWeapon => _slots[_activeIndex];
    public int              ActiveIndex  => _activeIndex;

    // ─────────────────────────────────────────────────────────────────────────
    //  Private
    // ─────────────────────────────────────────────────────────────────────────

    private WeaponDefinition[] _slots;
    private int   _activeIndex    = 0;
    private float _fireCooldown   = 0f;

    // Charged shot state
    private ChargedShot _chargeInstance = null;
    private float       _chargeTimer    = 0f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _slots = new WeaponDefinition[] { slot1, slot2, slot3, slot4 };

        // Register weapon switch callback from the HUD
        if (weaponHUD != null)
            weaponHUD.OnWeaponSwitched += SwitchToSlot;
    }

    private void Start()
    {
        SyncHUD();
    }

    private void OnDestroy()
    {
        if (weaponHUD != null)
            weaponHUD.OnWeaponSwitched -= SwitchToSlot;
    }

    private void Update()
    {
        HandleSwitchInput();
        HandleFireInput();

        if (_fireCooldown > 0f)
            _fireCooldown -= Time.deltaTime;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Weapon Switching
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleSwitchInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchToSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchToSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchToSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchToSlot(3);
    }

    public void SwitchToSlot(int index)
    {
        if (index < 0 || index >= _slots.Length) return;
        if (_slots[index] == null) return;
        if (index == _activeIndex) return;

        // Cancel any in-progress charge
        CancelCharge();

        _activeIndex  = index;
        _fireCooldown = 0f;

        SyncHUD();
        OnWeaponChanged?.Invoke(_activeIndex);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Fire Input
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleFireInput()
    {
        var weapon = ActiveWeapon;
        if (weapon == null || weapon.projectilePrefab == null) return;

        bool isChargedWeapon = IsChargedWeapon(weapon);

        if (isChargedWeapon)
        {
            HandleChargedFire(weapon);
        }
        else
        {
            HandleStandardFire(weapon);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Standard Fire
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleStandardFire(WeaponDefinition weapon)
    {
        if (!Input.GetKey(fireKey)) return;
        if (_fireCooldown > 0f) return;

        FireWeapon(weapon);
        _fireCooldown = weapon.fireRate;
    }

    private void FireWeapon(WeaponDefinition weapon)
    {
        foreach (var offset in weapon.firePointOffsets)
        {
            Vector3 spawnPos = transform.position + transform.TransformDirection(offset);
            var go           = Instantiate(weapon.projectilePrefab, spawnPos, Quaternion.identity);
            go.tag           = "Projectile";

            var projectile   = go.GetComponent<ProjectileBase>();
            if (projectile == null)
            {
                Debug.LogWarning($"[WeaponController] Projectile prefab '{weapon.projectilePrefab.name}' " +
                                  "is missing a ProjectileBase component.", go);
                Destroy(go);
                continue;
            }

            projectile.Damage    = weapon.damage;
            projectile.direction = Vector2.up;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Charged Fire
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleChargedFire(WeaponDefinition weapon)
    {
        if (Input.GetKeyDown(fireKey))
        {
            // Begin charging — spawn the projectile at the fire point and parent it
            StartCharge(weapon);
        }
        else if (Input.GetKey(fireKey) && _chargeInstance != null)
        {
            // Update charge progress
            _chargeTimer += Time.deltaTime;
            float ratio = Mathf.Clamp01(_chargeTimer / weapon.chargeTime);
            _chargeInstance.SetCharge(ratio);
        }
        else if (Input.GetKeyUp(fireKey) && _chargeInstance != null)
        {
            // Release
            float ratio = Mathf.Clamp01(_chargeTimer / weapon.chargeTime);
            _chargeInstance.Release(ratio);
            _chargeInstance = null;
            _chargeTimer    = 0f;
            _fireCooldown   = weapon.fireRate;
        }
    }

    private void StartCharge(WeaponDefinition weapon)
    {
        if (_chargeInstance != null) return;

        Vector2 offset   = weapon.firePointOffsets != null && weapon.firePointOffsets.Length > 0
            ? weapon.firePointOffsets[0]
            : Vector2.zero;

        Vector3 spawnPos = transform.position + transform.TransformDirection(offset);
        var go           = Instantiate(weapon.projectilePrefab, spawnPos, Quaternion.identity);
        go.tag           = "Projectile";

        // Parent to player so it follows movement
        go.transform.SetParent(transform);
        go.transform.localPosition = (Vector3)(Vector2)offset;

        _chargeInstance = go.GetComponent<ChargedShot>();

        if (_chargeInstance == null)
        {
            Debug.LogWarning("[WeaponController] Charged shot prefab missing ChargedShot component.", go);
            Destroy(go);
            return;
        }

        _chargeInstance.Damage               = weapon.damage;
        _chargeInstance.maxDamageMultiplier  = weapon.maxChargeDamageMultiplier;
        _chargeInstance.maxScaleMultiplier   = weapon.maxChargeScaleMultiplier;
        _chargeInstance.direction            = Vector2.up;
        _chargeTimer                         = 0f;

        _chargeInstance.SetCharge(0f);
    }

    private void CancelCharge()
    {
        if (_chargeInstance == null) return;
        Destroy(_chargeInstance.gameObject);
        _chargeInstance = null;
        _chargeTimer    = 0f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private bool IsChargedWeapon(WeaponDefinition weapon)
    {
        return weapon.projectilePrefab != null &&
               weapon.projectilePrefab.GetComponent<ChargedShot>() != null;
    }

    private void SyncHUD()
    {
        if (weaponHUD == null) return;

        for (int i = 0; i < _slots.Length; i++)
        {
            var def = _slots[i];
            if (def != null)
                weaponHUD.RegisterWeapon(i, def.weaponName, def.icon);
        }

        weaponHUD.SwitchToSlot(_activeIndex);
    }
}