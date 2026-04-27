using UnityEngine;

/// <summary>
/// Authored per level. Contains the list of world-space slot positions aliens
/// will move to after their entry path completes.
///
/// The number of slots determines how many aliens spawn in the wave —
/// one slot per alien, no more, no less.
/// </summary>
[CreateAssetMenu(fileName = "SlotDefinition", menuName = "Space Madness/Slot Definition")]
public class SlotDefinition : ScriptableObject
{
    [Tooltip("World-space positions of each slot.\n" +
             "One slot = one alien. The wave spawns exactly as many aliens as there are slots.")]
    public Vector2[] slots;

    /// <summary>How many aliens this level expects to spawn.</summary>
    public int SlotCount => slots != null ? slots.Length : 0;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (slots == null || slots.Length == 0)
            Debug.LogWarning($"[SlotDefinition] '{name}' has no slots defined.", this);
    }
#endif
}