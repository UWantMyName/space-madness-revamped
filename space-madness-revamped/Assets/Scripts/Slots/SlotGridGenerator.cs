using UnityEngine;

/// <summary>
/// Generates a grid of world-space slot positions for a given alien count.
///
/// Slots are distributed evenly across the upper portion of the screen.
/// Designed to be called at runtime by WaveManager when running from RuntimeLevelData.
///
/// Layout logic:
///   - Columns = ceil(sqrt(count × aspect)) — spread wider than tall to match the screen
///   - Rows    = ceil(count / columns)
///   - Slots fill left to right, top to bottom within the configured screen region
/// </summary>
public static class SlotGridGenerator
{
    /// <summary>
    /// Generates world-space slot positions for the given alien count.
    /// </summary>
    /// <param name="count">Number of slots to generate.</param>
    /// <param name="screenHeight">Screen height in world units (Camera.main.orthographicSize × 2).</param>
    /// <param name="aspect">Camera aspect ratio (width / height).</param>
    /// <param name="topFraction">Top of the slot region as a fraction of screen height from centre. Default 0.8 = 80% up.</param>
    /// <param name="bottomFraction">Bottom of the slot region as a fraction of screen height from centre. Default 0.2 = 20% up.</param>
    /// <param name="sidePadding">Horizontal padding from screen edge in world units.</param>
    public static Vector2[] Generate(
        int   count,
        float screenHeight,
        float aspect        = 16f / 9f,
        float topFraction   = 0.8f,
        float bottomFraction = 0.2f,
        float sidePadding   = 0.5f)
    {
        if (count <= 0) return System.Array.Empty<Vector2>();

        float halfH      = screenHeight * 0.5f;
        float halfW      = halfH * aspect;
        float usableW    = halfW * 2f - sidePadding * 2f;

        float yTop       = halfH * topFraction;
        float yBottom    = halfH * bottomFraction;
        float usableH    = yTop - yBottom;

        // Calculate grid dimensions — wider than tall to suit landscape screens
        int cols = Mathf.CeilToInt(Mathf.Sqrt(count * aspect));
        int rows = Mathf.CeilToInt((float)count / cols);

        float xStep = cols > 1 ? usableW / (cols - 1) : 0f;
        float yStep = rows > 1 ? usableH / (rows - 1) : 0f;

        float xStart = -halfW + sidePadding;
        float yStart = yTop;

        var positions = new Vector2[count];
        int filled    = 0;

        for (int row = 0; row < rows && filled < count; row++)
        {
            // How many columns fit on this row (last row may be shorter)
            int colsThisRow = Mathf.Min(cols, count - filled);

            // Centre the last (possibly shorter) row horizontally
            float rowWidth  = (colsThisRow - 1) * xStep;
            float rowStartX = -rowWidth * 0.5f;

            for (int col = 0; col < colsThisRow && filled < count; col++)
            {
                float x = rowStartX + col * xStep;
                float y = yStart - row * yStep;
                positions[filled++] = new Vector2(x, y);
            }
        }

        return positions;
    }

    /// <summary>
    /// Convenience overload that reads screen dimensions from Camera.main at runtime.
    /// </summary>
    public static Vector2[] Generate(
        int   count,
        float topFraction    = 0.8f,
        float bottomFraction = 0.2f,
        float sidePadding    = 0.5f)
    {
        if (Camera.main == null)
        {
            Debug.LogError("[SlotGridGenerator] Camera.main is null.");
            return System.Array.Empty<Vector2>();
        }

        float screenHeight = Camera.main.orthographicSize * 2f;
        float aspect       = Camera.main.aspect;

        return Generate(count, screenHeight, aspect, topFraction, bottomFraction, sidePadding);
    }
}