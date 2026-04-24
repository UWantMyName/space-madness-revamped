using UnityEngine;

public class LissajousSlotMovement : MonoBehaviour
{
    [Header("Lissajous Parameters")]
    [Tooltip("How fast the alien oscillates on the X axis.")]
    public float freqX = 1f;

    [Tooltip("How fast the alien oscillates on the Y axis. " +
             "A different ratio from freqX (e.g. 1:1.5, 2:3) produces complex non-repeating paths.")]
    public float freqY = 1.5f;

    [Tooltip("Max horizontal distance from the slot.")]
    public float amplitudeX = 1f;

    [Tooltip("Max vertical distance from the slot.")]
    public float amplitudeY = 0.5f;

    [Tooltip("Shifts the starting angle of the oscillation. " +
             "Give each alien a different value so they don't all move in sync.")]
    public float phaseOffset = 0f;

    // The center point this alien orbits around.
    // Set externally by the spawner, or defaults to spawn position.
    [HideInInspector] public Vector2 slotPosition;

    private float _timer = 0f;
    private bool _slotAssigned = false;

    private void Start()
    {
        // If no slot was assigned externally, treat current position as the slot.
        if (!_slotAssigned)
            slotPosition = transform.position;
    }

    /// <summary>
    /// Call this from your spawner/slot system to assign a slot before the alien activates.
    /// </summary>
    public void AssignSlot(Vector2 slot)
    {
        slotPosition = slot;
        _slotAssigned = true;
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        float x = slotPosition.x + Mathf.Sin(_timer * freqX + phaseOffset) * amplitudeX;
        float y = slotPosition.y + Mathf.Sin(_timer * freqY + phaseOffset) * amplitudeY;

        transform.position = new Vector3(x, y, transform.position.z);
    }

    // Draws the full Lissajous path in the Scene view so you can preview it without playing.
    private void OnDrawGizmosSelected()
    {
        Vector2 origin = Application.isPlaying ? slotPosition : (Vector2)transform.position;

        Gizmos.color = Color.cyan;
        Vector3 prev = Vector3.zero;
        int steps = 300;
        float duration = 10f; // simulate 10 seconds of movement

        for (int i = 0; i <= steps; i++)
        {
            float t = (i / (float)steps) * duration;
            float x = origin.x + Mathf.Sin(t * freqX + phaseOffset) * amplitudeX;
            float y = origin.y + Mathf.Sin(t * freqY + phaseOffset) * amplitudeY;
            Vector3 point = new Vector3(x, y, transform.position.z);

            if (i > 0)
                Gizmos.DrawLine(prev, point);

            prev = point;
        }

        // Draw the slot center
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(new Vector3(origin.x, origin.y, transform.position.z), 0.1f);
    }
}