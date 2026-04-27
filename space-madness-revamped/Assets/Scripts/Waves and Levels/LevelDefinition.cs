using UnityEngine;

/// <summary>
/// Defines a full level as an ordered sequence of waves.
/// Assign one of these to the WaveManager in your scene.
/// </summary>
[CreateAssetMenu(fileName = "LevelDefinition", menuName = "Space Madness/Level Definition")]
public class LevelDefinition : ScriptableObject
{
    [Tooltip("Waves played in order. Next wave starts only when all aliens of the current wave are dead.")]
    public WaveDefinition[] waves;

    [Tooltip("Seconds to wait between the last alien dying and the next wave spawning.")]
    [Min(0f)]
    public float delayBetweenWaves = 2f;

    public int WaveCount => waves != null ? waves.Length : 0;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (waves == null || waves.Length == 0)
            Debug.LogWarning($"[LevelDefinition] '{name}' has no waves defined.", this);
    }
#endif
}