using System.Collections.Generic;
using UnityEngine;

public class SlotManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Setup")]
    public SlotDefinition definition;

    // NEW: safe-zone margins in world units
    [Header("Safe Zone Margins (world units)")]
    [SerializeField] private float marginLeft   = 2f;
    [SerializeField] private float marginRight  = 2f;
    [SerializeField] private float marginTop    = 1.5f;
    [Tooltip("Keeps slots above this distance from screen centre — prevents aliens slotting near the player")]
    [SerializeField] private float marginBottom = 3f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Private State (unchanged)
    // ─────────────────────────────────────────────────────────────────────────

    private AlienController[] _occupants;
    private Queue<int>        _availableSlots;
    private Vector2[]         _runtimeSlots;

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity Lifecycle (unchanged)
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (definition != null)
            Initialise();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    public int  AvailableSlotCount => _availableSlots.Count;
    public bool AllSlotsAssigned   => _availableSlots.Count == 0;

    public void SetRuntimeSlots(Vector2[] positions)
    {
        if (positions == null || positions.Length == 0)
        {
            Debug.LogError("[SlotManager] SetRuntimeSlots called with null or empty positions.", this);
            return;
        }

        // NEW: clamp every incoming position to the camera safe zone
        Rect safe = CalcSafeZone();
        for (int i = 0; i < positions.Length; i++)
            positions[i] = ClampToRect(positions[i], safe);

        _runtimeSlots = positions;
        definition    = null;

        int count       = _runtimeSlots.Length;
        _occupants      = new AlienController[count];
        _availableSlots = new Queue<int>(count);

        for (int i = 0; i < count; i++)
            _availableSlots.Enqueue(i);
    }

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

        // NEW: clamp at assignment time as a second safety net
        //      (covers SlotDefinition assets that weren't authored with margins in mind)
        Rect safe            = CalcSafeZone();
        Vector2 rawPos       = _runtimeSlots != null ? _runtimeSlots[index] : definition.slots[index];
        alien.SlotPosition   = ClampToRect(rawPos, safe);

        return true;
    }

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

    public void ResetSlots() => Initialise();

    // ─────────────────────────────────────────────────────────────────────────
    //  NEW: Safe Zone Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the world-space rectangle within which slots are permitted.
    /// Derived from Camera.main's orthographic bounds minus inspector margins.
    /// </summary>
    private Rect CalcSafeZone()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[SlotManager] No Camera.main found — safe zone defaulting to ±8 / 0..6.");
            return new Rect(-8f, 0f, 16f, 6f);
        }

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 cp  = cam.transform.position;

        float left   = cp.x - halfW + marginLeft;
        float right  = cp.x + halfW - marginRight;
        float top    = cp.y + halfH - marginTop;
        float bottom = cp.y         + marginBottom;  // floor sits above screen centre

        // Prevent inverted rect if margins are too large
        if (left  >= right)  { float mid = (left + right)   * 0.5f; left   = mid - 0.5f; right = mid + 0.5f; }
        if (bottom >= top)   { float mid = (bottom + top)   * 0.5f; bottom = mid - 0.5f; top   = mid + 0.5f; }

        return Rect.MinMaxRect(left, bottom, right, top);
    }

    private static Vector2 ClampToRect(Vector2 pos, Rect rect) =>
        new Vector2(
            Mathf.Clamp(pos.x, rect.xMin, rect.xMax),
            Mathf.Clamp(pos.y, rect.yMin, rect.yMax));

    // ─────────────────────────────────────────────────────────────────────────
    //  Private (unchanged)
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
    //  Gizmos — now also draws the safe zone boundary
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // NEW: draw the safe zone as a cyan wireframe rectangle
        Rect safe = CalcSafeZone();
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            new Vector3(safe.center.x, safe.center.y, 0f),
            new Vector3(safe.width,    safe.height,   0f));

        // Existing slot spheres (unchanged logic)
        Vector2[] source = _runtimeSlots ?? definition?.slots;
        if (source == null) return;

        for (int i = 0; i < source.Length; i++)
        {
            Vector3 pos      = new Vector3(source[i].x, source[i].y, 0f);
            bool    occupied = _occupants != null && i < _occupants.Length && _occupants[i] != null;

            Gizmos.color = occupied ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(pos, 0.15f);

            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(pos + Vector3.up * 0.25f, $"S{i}");
        }
    }
#endif
}