using UnityEngine;

// ─────────────────────────────────────────────
//  Enums
// ─────────────────────────────────────────────

public enum SegmentType   { Linear, Arc }
public enum TurnDirection { Left, Right }
public enum SpawnEdge     { Top, Left, Right }

// ─────────────────────────────────────────────
//  Segment
// ─────────────────────────────────────────────

[System.Serializable]
public class AlienSegment
{
    public SegmentType type = SegmentType.Linear;

    [Tooltip("LINEAR ONLY — travel direction in world-space degrees.\n0 = right · 90 = up · 180 = left · 270 = down")]
    public float angleInDegrees = 270f;

    [Tooltip("ARC ONLY — which way to curve.")]
    public TurnDirection turnDirection = TurnDirection.Left;

    [Tooltip("ARC ONLY — how many degrees to sweep along the arc.")]
    [Range(1f, 360f)]
    public float sweepDegrees = 90f;

    [Tooltip("LINEAR: distance to travel as a fraction of screen height.\n" +
             "ARC: arc length as a fraction of screen height (radius is derived from this + sweep).")]
    [Range(0.01f, 3f)]
    public float distanceFraction = 0.3f;

    [Tooltip("Travel speed in world units per second.")]
    [Range(0.1f, 20f)]
    public float speed = 4f;
}

// ─────────────────────────────────────────────
//  Alien Definition  (ScriptableObject)
// ─────────────────────────────────────────────

[CreateAssetMenu(fileName = "AlienDefinition", menuName = "Space Madness/Alien Definition")]
public class AlienDefinition : ScriptableObject
{
    [Header("Spawn")]
    public SpawnEdge spawnEdge = SpawnEdge.Top;

    [Range(0f, 1f)]
    [Tooltip("Position along the spawn edge.\n0 = left / bottom end · 1 = right / top end")]
    public float spawnPosition = 0.5f;

    [Header("Entry Path")]
    public AlienSegment[] segments;

    [Header("Active — Lissajous")]
    [Tooltip("Oscillation frequency on the X axis.")]
    public float lissajousFreqX = 1f;

    [Tooltip("Oscillation frequency on the Y axis.\nA ratio different from FreqX (e.g. 1:1.5, 2:3) produces non-repeating figure-8-like paths.")]
    public float lissajousFreqY = 1.5f;

    [Tooltip("Max horizontal drift from the slot (world units).")]
    public float lissajousAmplitudeX = 0.5f;

    [Tooltip("Max vertical drift from the slot (world units).")]
    public float lissajousAmplitudeY = 0.3f;

    [Tooltip("Shifts the starting phase. Give each alien a different value so they don't all move in sync.")]
    public float lissajousPhaseOffset = 0f;

    [Header("Shooting")]
    [Tooltip("Average time in seconds between shots during Active state.")]
    public float shootInterval = 2f;

    [Tooltip("± seconds of randomness added to each shoot interval so not every alien fires at the same moment.")]
    public float shootIntervalVariance = 0.5f;

    [Header("Editor Preview")]
    [Tooltip("Screen height in world units used to draw the entry path gizmo in the Scene view.\nMatch your camera's orthographic size × 2.")]
    public float previewScreenHeight = 10f;
}
