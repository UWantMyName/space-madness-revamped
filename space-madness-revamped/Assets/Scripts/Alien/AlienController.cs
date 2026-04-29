using UnityEngine;

/// <summary>
/// Main alien state machine.
///
/// States:
///   Entry     — alien follows its authored segment chain onto the screen.
///   Slotting  — alien moves toward its assigned slot position.
///   Active    — alien oscillates via Lissajous and shoots periodically.
///
/// Active sub-states:
///   Lissajous — default idle oscillation.
///   Kamikaze  — dives at the player (stub, implemented in the abilities pass).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class AlienController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  State Enums
    // ─────────────────────────────────────────────────────────────────────────

    public enum AlienState      { Entry, Slotting, Active }
    public enum ActiveSubState  { Lissajous, Kamikaze }

    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Setup")]
    public AlienDefinition definition;
    public GameObject bulletPrefab;

    [Header("Slotting")]
    [Tooltip("Speed at which the alien moves from its entry end-point to the assigned slot.")]
    public float slottingSpeed = 5f;
    [Tooltip("Distance threshold at which the alien is considered to have arrived at the slot.")]
    public float slotArrivalThreshold = 0.05f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Public State
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Assigned externally by the wave/slot manager before the alien is activated.</summary>
    public Vector2 SlotPosition { get; set; }

    /// <summary>
    /// The entry path for this alien. Injected by WaveManager at spawn time.
    /// Built by the level factory from the chapter file's PATH block.
    /// </summary>
    public AlienRuntimePath RuntimePath { get; set; }

    public AlienState     CurrentState    => _state;
    public ActiveSubState CurrentSubState => _activeSubState;

    // ─────────────────────────────────────────────────────────────────────────
    //  Private — Entry
    // ─────────────────────────────────────────────────────────────────────────

    private AlienState     _state;
    private ActiveSubState _activeSubState;

    private int    _segmentIndex;
    private float  _segmentProgress;   // world units traveled (Linear) · degrees swept (Arc)
    private Vector2 _currentHeading;   // normalized direction the alien is currently facing

    // Arc-specific (recalculated each time an Arc segment begins)
    private float   _arcRadius;
    private Vector2 _arcCenter;
    private float   _arcStartAngle;
    private float   _arcSign;          // +1 = CCW (Left) · -1 = CW (Right)

    // ─────────────────────────────────────────────────────────────────────────
    //  Private — Active
    // ─────────────────────────────────────────────────────────────────────────

    private float _lissajousTimer;
    private float _shootTimer;
    private float _nextShootInterval;
    private Transform _playerTransform;

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        if (definition == null)
        {
            Debug.LogError($"[AlienController] {gameObject.name} has no AlienDefinition.", this);
            enabled = false;
            return;
        }

        if (RuntimePath == null)
        {
            Debug.LogError($"[AlienController] {gameObject.name} has no RuntimePath assigned.", this);
            enabled = false;
            return;
        }

        transform.position = ScreenUtils.GetSpawnPosition(RuntimePath.spawnEdge, RuntimePath.spawnPosition);
        _currentHeading    = AlienPathSimulator.GetInitialHeading(RuntimePath);
        _playerTransform   = GameObject.FindGameObjectWithTag("Player")?.transform;

        TransitionToEntry();
        ScheduleNextShot();
    }

    private void Update()
    {
        switch (_state)
        {
            case AlienState.Entry:    UpdateEntry();    break;
            case AlienState.Slotting: UpdateSlotting(); break;
            case AlienState.Active:   UpdateActive();   break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  State: Entry
    // ─────────────────────────────────────────────────────────────────────────

    private void TransitionToEntry()
    {
        _state         = AlienState.Entry;
        _segmentIndex  = 0;
        BeginSegment(0);
    }

    private void BeginSegment(int index)
    {
        if (index >= RuntimePath.segments.Length)
        {
            TransitionToSlotting();
            return;
        }

        _segmentIndex    = index;
        _segmentProgress = 0f;

        var seg = RuntimePath.segments[index];

        if (seg.type == SegmentType.Linear)
        {
            _currentHeading = ScreenUtils.AngleToDirection(seg.angleInDegrees);
        }
        else // Arc
        {
            _arcSign = seg.turnDirection == TurnDirection.Left ? 1f : -1f;

            // Perpendicular to current heading, in the turn direction
            Vector2 perp = new Vector2(-_currentHeading.y * _arcSign, _currentHeading.x * _arcSign);

            float arcLength = ScreenUtils.FractionToWorldUnits(seg.distanceFraction);
            float sweepRad  = seg.sweepDegrees * Mathf.Deg2Rad;
            _arcRadius      = arcLength / sweepRad;

            _arcCenter     = (Vector2)transform.position + perp * _arcRadius;
            _arcStartAngle = Mathf.Atan2(
                transform.position.y - _arcCenter.y,
                transform.position.x - _arcCenter.x
            );
        }
    }

    private void UpdateEntry()
    {
        var seg = RuntimePath.segments[_segmentIndex];

        if (seg.type == SegmentType.Linear)
        {
            float step       = seg.speed * Time.deltaTime;
            float targetDist = ScreenUtils.FractionToWorldUnits(seg.distanceFraction);

            transform.position  += (Vector3)(_currentHeading * step);
            _segmentProgress    += step;

            if (_segmentProgress >= targetDist)
                BeginSegment(_segmentIndex + 1);
        }
        else // Arc
        {
            // Angular speed from linear speed and radius
            float angularSpeedRad = seg.speed / _arcRadius;
            float dAngleDeg       = angularSpeedRad * Mathf.Rad2Deg * Time.deltaTime;
            _segmentProgress     += dAngleDeg;

            float progressRad = _segmentProgress * Mathf.Deg2Rad;
            float angle       = _arcStartAngle + progressRad * _arcSign;

            // Position on the arc
            transform.position = new Vector3(
                _arcCenter.x + Mathf.Cos(angle) * _arcRadius,
                _arcCenter.y + Mathf.Sin(angle) * _arcRadius,
                transform.position.z
            );

            // Keep heading tangent to the arc so the next segment chains correctly
            Vector2 radial  = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            _currentHeading = new Vector2(-radial.y * _arcSign, radial.x * _arcSign).normalized;

            if (_segmentProgress >= seg.sweepDegrees)
            {
                // Snap to exact end position to prevent floating-point drift
                float endAngle = _arcStartAngle + seg.sweepDegrees * Mathf.Deg2Rad * _arcSign;
                transform.position = new Vector3(
                    _arcCenter.x + Mathf.Cos(endAngle) * _arcRadius,
                    _arcCenter.y + Mathf.Sin(endAngle) * _arcRadius,
                    transform.position.z
                );
                Vector2 endRadial = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));
                _currentHeading   = new Vector2(-endRadial.y * _arcSign, endRadial.x * _arcSign).normalized;

                BeginSegment(_segmentIndex + 1);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  State: Slotting
    // ─────────────────────────────────────────────────────────────────────────

    private void TransitionToSlotting()
    {
        _state = AlienState.Slotting;
    }

    private void UpdateSlotting()
    {
        Vector3 target = new Vector3(SlotPosition.x, SlotPosition.y, transform.position.z);

        transform.position = Vector3.MoveTowards(
            transform.position, target, slottingSpeed * Time.deltaTime
        );

        if (Vector2.Distance(transform.position, SlotPosition) <= slotArrivalThreshold)
        {
            transform.position = target;
            TransitionToActive();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  State: Active
    // ─────────────────────────────────────────────────────────────────────────

    private void TransitionToActive()
    {
        _state          = AlienState.Active;
        _activeSubState = ActiveSubState.Lissajous;

        // Seed the timer with the phase offset so aliens don't all start in sync
        _lissajousTimer = definition.lissajousPhaseOffset;
    }

    private void UpdateActive()
    {
        switch (_activeSubState)
        {
            case ActiveSubState.Lissajous: UpdateLissajous(); break;
            case ActiveSubState.Kamikaze:  UpdateKamikaze();  break;
        }

        UpdateShooting();
    }

    private void UpdateLissajous()
    {
        _lissajousTimer += Time.deltaTime;

        float x = SlotPosition.x + Mathf.Sin(_lissajousTimer * definition.lissajousFreqX) * definition.lissajousAmplitudeX;
        float y = SlotPosition.y + Mathf.Sin(_lissajousTimer * definition.lissajousFreqY) * definition.lissajousAmplitudeY;

        transform.position = new Vector3(x, y, transform.position.z);
    }

    private void UpdateKamikaze()
    {
        // TODO: implemented in the abilities pass.
        // Will dive toward the player's current position, then either
        // explode on contact or return to slot if it misses.
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Shooting
    // ─────────────────────────────────────────────────────────────────────────

    private void ScheduleNextShot()
    {
        float variance     = Random.Range(-definition.shootIntervalVariance, definition.shootIntervalVariance);
        _nextShootInterval = Mathf.Max(0.1f, definition.shootInterval + variance);
        _shootTimer        = 0f;
    }

    private void UpdateShooting()
    {
        // Only shoot while Active
        if (_state != AlienState.Active) return;

        _shootTimer += Time.deltaTime;

        if (_shootTimer >= _nextShootInterval)
        {
            Shoot();
            ScheduleNextShot();
        }
    }

    private void Shoot()
    {
        if (bulletPrefab == null) return;
        // The bullet prefab is responsible for moving itself (downward by convention).
        Instantiate(bulletPrefab, transform.position, Quaternion.identity);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API  (wave manager, abilities)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Triggers Kamikaze mode. The alien abandons its slot and dives at the player.</summary>
    public void ActivateKamikaze()
    {
        if (_state != AlienState.Active) return;
        _activeSubState = ActiveSubState.Kamikaze;
    }

    /// <summary>Returns the alien to Lissajous mode (e.g. after a failed kamikaze dive).</summary>
    public void DeactivateKamikaze()
    {
        if (_activeSubState != ActiveSubState.Kamikaze) return;
        _activeSubState = ActiveSubState.Lissajous;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Gizmos
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (definition == null || RuntimePath == null) return;

        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;

        // Draw entry path
        var points = AlienPathSimulator.Simulate(RuntimePath, RuntimePath.previewScreenHeight, aspect);
        if (points.Count > 1)
        {
            UnityEditor.Handles.color = Color.cyan;
            for (int i = 0; i < points.Count - 1; i++)
                UnityEditor.Handles.DrawLine(points[i], points[i + 1]);

            // Mark the spawn point
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(points[0], 0.15f);

            // Mark the entry end-point (where Slotting begins)
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(points[^1], 0.15f);
        }

        // Draw the assigned slot
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(new Vector3(SlotPosition.x, SlotPosition.y, 0f), 0.15f);

        // Draw Lissajous bounds around the slot
        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
        Gizmos.DrawWireCube(
            new Vector3(SlotPosition.x, SlotPosition.y, 0f),
            new Vector3(definition.lissajousAmplitudeX * 2f, definition.lissajousAmplitudeY * 2f, 0f)
        );
    }
#endif
}