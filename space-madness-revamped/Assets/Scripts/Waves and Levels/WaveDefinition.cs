using UnityEngine;

/// <summary>
/// Defines a single wave inside a level.
///
/// A wave has:
///   - Its own SlotDefinition  (slot count also determines alien count)
///   - One AlienDefinition per alien (can be different types)
///   - Group spawning settings (how many aliens per group, delay between groups)
/// </summary>
[CreateAssetMenu(fileName = "WaveDefinition", menuName = "Space Madness/Wave Definition")]
public class WaveDefinition : ScriptableObject
{
    [Header("Slot Layout")]
    [Tooltip("The slot layout for this wave. Slot count = alien count.")]
    public SlotDefinition slotDefinition;

    [Header("Aliens")]
    [Tooltip("One entry per alien. Must match the number of slots in the SlotDefinition.\n" +
             "Aliens are spawned in index order and assigned slots in the same order.")]
    public AlienDefinition[] alienDefinitions;

    [Header("Group Spawning")]
    [Tooltip("How many aliens spawn together as a group.")]
    [Min(1)]
    public int groupSize = 3;

    [Tooltip("Seconds to wait before the first group spawns.")]
    [Min(0f)]
    public float delayBeforeFirstGroup = 0.5f;

    [Tooltip("Seconds between each group spawning.")]
    [Min(0f)]
    public float delayBetweenGroups = 1.5f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (slotDefinition == null)
        {
            Debug.LogWarning($"[WaveDefinition] '{name}' has no SlotDefinition assigned.", this);
            return;
        }

        int slotCount  = slotDefinition.SlotCount;
        int alienCount = alienDefinitions != null ? alienDefinitions.Length : 0;

        if (alienCount != slotCount)
            Debug.LogWarning(
                $"[WaveDefinition] '{name}' has {alienCount} AlienDefinitions but {slotCount} slots. " +
                $"These must match.", this);
    }
#endif
}