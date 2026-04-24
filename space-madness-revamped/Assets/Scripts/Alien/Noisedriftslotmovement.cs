using UnityEngine;

public class NoiseDriftSlotMovement : MonoBehaviour
{
    [Header("Noise Parameters")]
    [Tooltip("How fast the Perlin noise evolves over time. Higher = more frantic drift.")]
    public float noiseSpeed = 1f;

    [Tooltip("How hard the noise pushes the alien around.")]
    public float noiseStrength = 3f;

    [Header("Spring Parameters")]
    [Tooltip("How strongly the alien is pulled back toward its slot when it drifts away.")]
    public float springStrength = 5f;

    [Tooltip("Reduces velocity over time to prevent endless oscillation.")]
    public float damping = 2f;

    [Tooltip("If the alien drifts beyond this radius, the spring force doubles to reel it back in.")]
    public float maxDriftRadius = 1.5f;

    // The center point this alien drifts around.
    // Set externally by the spawner, or defaults to spawn position.
    [HideInInspector] public Vector2 slotPosition;

    private Vector2 _velocity = Vector2.zero;
    private float _noiseOffsetX;
    private float _noiseOffsetY;
    private bool _slotAssigned = false;

    private void Start()
    {
        if (!_slotAssigned)
            slotPosition = transform.position;

        // Random offsets so each alien samples a different region of the noise field —
        // prevents all aliens from drifting in the same direction at the same time.
        _noiseOffsetX = Random.Range(0f, 999f);
        _noiseOffsetY = Random.Range(0f, 999f);
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
        float t = Time.time * noiseSpeed;

        // Sample Perlin noise on two independent axes and remap from [0,1] to [-1,1]
        float noiseX = (Mathf.PerlinNoise(t + _noiseOffsetX, 0f) - 0.5f) * 2f;
        float noiseY = (Mathf.PerlinNoise(0f, t + _noiseOffsetY) - 0.5f) * 2f;
        Vector2 noiseForce = new Vector2(noiseX, noiseY) * noiseStrength;

        // Spring force pulls the alien back toward its slot
        Vector2 toSlot = slotPosition - (Vector2)transform.position;
        float springMultiplier = toSlot.magnitude > maxDriftRadius ? 2f : 1f;
        Vector2 springForce = toSlot * springStrength * springMultiplier;

        // Damping bleeds off velocity so the alien doesn't oscillate forever
        Vector2 dampingForce = -_velocity * damping;

        // Integrate
        _velocity += (noiseForce + springForce + dampingForce) * Time.deltaTime;
        transform.position += (Vector3)(_velocity * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 origin = Application.isPlaying ? slotPosition : (Vector2)transform.position;

        // Draw the max drift boundary
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        DrawWireCircle(new Vector3(origin.x, origin.y, transform.position.z), maxDriftRadius);

        // Draw the slot center
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(new Vector3(origin.x, origin.y, transform.position.z), 0.1f);
    }

    private void DrawWireCircle(Vector3 center, float radius, int segments = 32)
    {
        float angleStep = 360f / segments;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}