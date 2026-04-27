using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure math path simulator.
/// No MonoBehaviour. No Camera.main dependency.
/// Safe to call from Editor code, OnDrawGizmos, and the future Editor Window.
/// </summary>
public static class AlienPathSimulator
{
    /// <summary>
    /// Simulates the full entry path for an AlienDefinition and returns a list of world-space positions.
    /// </summary>
    /// <param name="definition">The alien definition to simulate.</param>
    /// <param name="screenHeight">Screen height in world units (camera orthographicSize × 2).</param>
    /// <param name="aspect">Camera aspect ratio (width / height). Defaults to 16:9.</param>
    /// <param name="samplesPerSegment">How many points to sample per segment. More = smoother preview.</param>
    public static List<Vector2> Simulate(AlienDefinition definition,
                                          float screenHeight,
                                          float aspect = 16f / 9f,
                                          int samplesPerSegment = 60)
    {
        var points = new List<Vector2>();
        if (definition == null || definition.segments == null || definition.segments.Length == 0)
            return points;

        float halfH = screenHeight * 0.5f;
        float halfW = halfH * aspect;

        Vector2 pos     = ScreenUtils.GetSpawnPosition(definition.spawnEdge, definition.spawnPosition, halfH, halfW);
        Vector2 heading = GetInitialHeading(definition);

        points.Add(pos);

        foreach (var seg in definition.segments)
        {
            float worldDist = seg.distanceFraction * screenHeight;

            if (seg.type == SegmentType.Linear)
            {
                heading = ScreenUtils.AngleToDirection(seg.angleInDegrees);

                for (int i = 1; i <= samplesPerSegment; i++)
                {
                    float t = (float)i / samplesPerSegment;
                    points.Add(pos + heading * (worldDist * t));
                }

                pos += heading * worldDist;
            }
            else // Arc
            {
                float arcSign   = seg.turnDirection == TurnDirection.Left ? 1f : -1f;
                float sweepRad  = seg.sweepDegrees * Mathf.Deg2Rad;
                float radius    = worldDist / sweepRad;

                // Center is perpendicular to heading in the turn direction
                Vector2 perp   = new Vector2(-heading.y * arcSign, heading.x * arcSign);
                Vector2 center = pos + perp * radius;
                float startAngle = Mathf.Atan2(pos.y - center.y, pos.x - center.x);

                for (int i = 1; i <= samplesPerSegment; i++)
                {
                    float t     = (float)i / samplesPerSegment;
                    float angle = startAngle + sweepRad * t * arcSign;
                    points.Add(new Vector2(
                        center.x + Mathf.Cos(angle) * radius,
                        center.y + Mathf.Sin(angle) * radius
                    ));
                }

                // Advance pos and heading to end of arc
                float endAngle  = startAngle + sweepRad * arcSign;
                pos             = new Vector2(center.x + Mathf.Cos(endAngle) * radius,
                                              center.y + Mathf.Sin(endAngle) * radius);
                Vector2 radial  = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));
                heading         = new Vector2(-radial.y * arcSign, radial.x * arcSign).normalized;
            }
        }

        return points;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    public static Vector2 GetInitialHeading(AlienDefinition definition)
    {
        if (definition.segments == null || definition.segments.Length == 0)
            return Vector2.down;

        var first = definition.segments[0];
        return first.type == SegmentType.Linear
            ? ScreenUtils.AngleToDirection(first.angleInDegrees)
            : Vector2.down;
    }
}