using UnityEngine;

public static class ScreenUtils
{
    // ─── Runtime versions (require Camera.main) ───────────────────────────────

    /// <summary>Converts a fraction of the screen height into world units at runtime.</summary>
    public static float FractionToWorldUnits(float fraction)
    {
        if (Camera.main == null)
        {
            Debug.LogWarning("ScreenUtils: Camera.main is null. Returning raw fraction.");
            return fraction;
        }
        return fraction * Camera.main.orthographicSize * 2f;
    }

    /// <summary>
    /// Returns a world-space spawn position along the given edge at runtime.
    /// t=0 → left/bottom of the edge · t=1 → right/top of the edge.
    /// padding pushes the point just off-screen.
    /// </summary>
    public static Vector2 GetSpawnPosition(SpawnEdge edge, float t, float padding = 0.5f)
    {
        if (Camera.main == null) return Vector2.zero;

        float halfH = Camera.main.orthographicSize;
        float halfW = halfH * Camera.main.aspect;
        return GetSpawnPosition(edge, t, halfH, halfW, padding);
    }

    // ─── Pure versions (no Camera.main dependency, safe in Editor) ────────────

    /// <summary>
    /// Returns a world-space spawn position using explicit screen dimensions.
    /// Used by AlienPathSimulator so it works in edit mode without Camera.main.
    /// </summary>
    public static Vector2 GetSpawnPosition(SpawnEdge edge, float t,
                                            float halfH, float halfW,
                                            float padding = 0.5f)
    {
        return edge switch
        {
            SpawnEdge.Top   => new Vector2(Mathf.Lerp(-halfW, halfW, t), halfH + padding),
            SpawnEdge.Left  => new Vector2(-halfW - padding, Mathf.Lerp(-halfH, halfH, t)),
            SpawnEdge.Right => new Vector2( halfW + padding, Mathf.Lerp(-halfH, halfH, t)),
            _               => Vector2.zero
        };
    }

    /// <summary>Converts degrees to a normalized direction vector.</summary>
    public static Vector2 AngleToDirection(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }
}
