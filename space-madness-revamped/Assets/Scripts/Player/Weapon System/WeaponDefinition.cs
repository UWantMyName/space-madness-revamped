using UnityEngine;

/// <summary>
/// Authored once per weapon type.
/// Assign to WeaponController.weaponSlots in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "WeaponDefinition", menuName = "Space Madness/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Display name shown in the HUD hex display.")]
    public string weaponName = "PULSE CANNON";

    [Tooltip("Icon shown in the weapon slot and hex display.")]
    public Sprite icon;

    [Header("Firing")]
    [Tooltip("Seconds between each shot. Lower = faster fire rate.")]
    [Min(0.05f)]
    public float fireRate = 0.2f;

    [Tooltip("Base damage per projectile.")]
    [Min(1)]
    public int damage = 1;

    [Tooltip("The projectile prefab to instantiate. Must have a ProjectileBase component.")]
    public GameObject projectilePrefab;

    [Header("Fire Points")]
    [Tooltip("Local offsets from the player's position where projectiles spawn.\n" +
             "One entry = centre fire. Two entries = dual wing fire. etc.")]
    public Vector2[] firePointOffsets = { Vector2.zero };

    [Header("Charged Shot (only used by ChargedShot projectile)")]
    [Tooltip("Seconds to reach full charge.")]
    [Min(0.1f)]
    public float chargeTime = 1.5f;

    [Tooltip("Damage multiplier at full charge.")]
    [Min(1f)]
    public float maxChargeDamageMultiplier = 3f;

    [Tooltip("Scale multiplier at full charge.")]
    [Min(1f)]
    public float maxChargeScaleMultiplier = 2.5f;
}