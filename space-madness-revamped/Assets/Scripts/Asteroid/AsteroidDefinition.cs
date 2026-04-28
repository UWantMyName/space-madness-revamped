using UnityEngine;

// ─────────────────────────────────────────────
//  Enum
// ─────────────────────────────────────────────

public enum AsteroidSize { Small, Medium, Large }

// ─────────────────────────────────────────────
//  Asteroid Definition  (ScriptableObject)
// ─────────────────────────────────────────────

/// <summary>
/// Authored once per size (Small / Medium / Large).
/// Assign three of these to the AsteroidSpawner in your scene.
///
/// Stats scale with size — larger asteroids are tankier, faster, and hit harder.
/// </summary>
[CreateAssetMenu(fileName = "AsteroidDefinition", menuName = "Space Madness/Asteroid Definition")]
public class AsteroidDefinition : ScriptableObject
{
    [Header("Identity")]
    public AsteroidSize size = AsteroidSize.Medium;

    [Header("Stats")]
    [Tooltip("How many bullet hits this asteroid can take before dying.")]
    [Min(1)]
    public int maxHP = 1;

    [Tooltip("How much damage this asteroid deals to the player on contact.")]
    [Min(0)]
    public int damageToPlayer = 1;

    [Tooltip("Travel speed in world units per second.")]
    [Range(0.5f, 20f)]
    public float speed = 3f;

    [Header("Visuals")]
    [Tooltip("Sprite used for this asteroid size.")]
    public Sprite sprite;

    [Tooltip("Rotation speed in degrees per second. Set 0 to disable.")]
    [Min(0f)]
    public float rotationSpeed = 45f;

    [Header("Spawn Direction")]
    [Tooltip("Minimum angle offset from straight down (degrees). Negative = left of down.")]
    [Range(-60f, 0f)]
    public float minAngleOffset = -25f;

    [Tooltip("Maximum angle offset from straight down (degrees). Positive = right of down.")]
    [Range(0f, 60f)]
    public float maxAngleOffset = 25f;
}