using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime slot manager.
///
/// Responsibilities:
///   - Tracks which slots are occupied and which are free.
///   - Assigns the next available slot to an alien on request.
///   - Frees a slot when an alien dies (slot stays empty — not re-claimed).
///   - Draws slot gizmos in the Scene view for authoring feedback.
///
/// Usage:
///   Call SlotManager.AssignNextSlot(alienController) when spawning each alien.
///   Call SlotManager.FreeSlot(alienController) when an alien dies.
/// </summary>
public class SlotManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Setup")]
    public SlotDefinition definition;

    // ─────────────────────────────────────────────────────────────────────────
    //  Private State
    // ─────────────────────────────────────────────────────────────────────────

    // Maps slot index → the alien currently occupying it (null = free)
    private AlienController[] _occupants;

    // Queue of slot indices not yet assigned, in order
    private Queue<int> _availableSlots;

    // Slot positions when driven at runtime (no SlotDefinition asset)
    private Vector2[] _runtimeSlots;

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Definition may be null if this SlotManager is driven at runtime via SetRuntimeSlots().
        // WaveManager will call SetRuntimeSlots() before any slots are needed in that case.
        if (definition != null)
            Initialise();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>How many slots are still unassigned.</summary>
    public int AvailableSlotCount => _availableSlots.Count;

    /// <summary>True if every slot has been assigned at least once.</summary>
    public bool AllSlotsAssigned => _availableSlots.Count == 0;

    /// <summary>
    /// Initialises the slot manager from a runtime-generated array of positions.
    /// Use this when running from RuntimeLevelData instead of a SlotDefinition asset.
    /// Replaces any previously assigned definition or runtime slots.
    /// </summary>
    public void SetRuntimeSlots(Vector2[] positions)
    {
        if (positions == null || positions.Length == 0)
        {
            Debug.LogError("[SlotManager] SetRuntimeSlots called with null or empty positions.", this);
            return;
        }

        _runtimeSlots = positions;
        definition    = null; // runtime mode — no ScriptableObject needed

        int count       = _runtimeSlots.Length;
        _occupants      = new AlienController[count];
        _availableSlots = new Queue<int>(count);

        for (int i = 0; i < count; i++)
            _availableSlots.Enqueue(i);
    }

    /// <summary>
    /// Assigns the next available slot to the given alien.
    /// Sets AlienController.SlotPosition and returns true on success.
    /// Returns false if no slots remain.
    /// </summary>
    public bool AssignNextSlot(AlienController alien)
    {
        if (alien == null)
        {
            Debug.LogWarning("[SlotManager] AssignNextSlot called with a null alien.");
            return false;
        }

        if (_availableSlots.Count == 0)
        {
            Debug.LogWarning("[SlotManager] No available slots left to assign.");
            return false;
        }

        int index         = _availableSlots.Dequeue();
        _occupants[index] = alien;
        alien.SlotPosition = _runtimeSlots != null
            ? _runtimeSlots[index]
            : definition.slots[index];

        return true;
    }

    /// <summary>
    /// Marks the slot occupied by this alien as empty.
    /// Called when an alien dies. The slot is NOT returned to the available queue.
    /// </summary>
    public void FreeSlot(AlienController alien)
    {
        if (alien == null) return;

        for (int i = 0; i < _occupants.Length; i++)
        {
            if (_occupants[i] == alien)
            {
                _occupants[i] = null;
                return;
            }
        }

        Debug.LogWarning($"[SlotManager] FreeSlot: alien '{alien.name}' was not found in any slot.");
    }

    /// <summary>
    /// Resets all slots back to unassigned.
    /// Call this at the start of a new wave or level.
    /// </summary>
    public void ResetSlots()
    {
        Initialise();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private
    // ─────────────────────────────────────────────────────────────────────────

    private void Initialise()
    {
        int count       = definition != null ? definition.SlotCount : 0;
        _occupants      = new AlienController[count];
        _availableSlots = new Queue<int>(count);

        for (int i = 0; i < count; i++)
            _availableSlots.Enqueue(i);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Gizmos
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (definition == null || definition.slots == null) return;

        for (int i = 0; i < definition.slots.Length; i++)
        {
            Vector3 pos = new Vector3(definition.slots[i].x, definition.slots[i].y, 0f);

            bool occupied = _occupants != null && i < _occupants.Length && _occupants[i] != null;

            // Yellow = free · Red = occupied
            Gizmos.color = occupied ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(pos, 0.15f);

            // Draw slot index label
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(pos + Vector3.up * 0.25f, $"S{i}");
        }
    }
#endif
}