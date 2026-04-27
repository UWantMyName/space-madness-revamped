using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sequences waves for a level.
///
/// Flow per wave:
///   1. Reset SlotManager with the wave's SlotDefinition.
///   2. Spawn aliens in groups, with a configurable delay between each group.
///   3. Assign a slot to each alien as it spawns.
///   4. Wait until every alien in the wave is dead.
///   5. Wait delayBetweenWaves seconds, then start the next wave.
///   6. When all waves are done, fire OnLevelComplete.
/// </summary>
public class WaveManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Setup")]
    public LevelDefinition levelDefinition;

    [Tooltip("Prefab that has AlienController (and SpriteRenderer) on it.")]
    public GameObject alienPrefab;

    [Tooltip("SlotManager in the scene. Its definition is swapped per wave.")]
    public SlotManager slotManager;

    // ─────────────────────────────────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Fired when a new wave starts. Parameter is the 0-based wave index.</summary>
    public event System.Action<int> OnWaveStarted;

    /// <summary>Fired when all aliens in a wave are dead.</summary>
    public event System.Action<int> OnWaveCleared;

    /// <summary>Fired when all waves in the level are complete.</summary>
    public event System.Action OnLevelComplete;

    // ─────────────────────────────────────────────────────────────────────────
    //  Public State
    // ─────────────────────────────────────────────────────────────────────────

    public int  CurrentWaveIndex { get; private set; } = -1;
    public bool LevelComplete    { get; private set; } = false;

    // ─────────────────────────────────────────────────────────────────────────
    //  Private State
    // ─────────────────────────────────────────────────────────────────────────

    // All aliens alive in the current wave
    private readonly List<AlienController> _aliveAliens = new();

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        if (!Validate()) return;
        StartCoroutine(RunLevel());
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Level Sequence
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator RunLevel()
    {
        for (int i = 0; i < levelDefinition.WaveCount; i++)
        {
            yield return RunWave(i);
            yield return new WaitForSeconds(levelDefinition.delayBetweenWaves);
        }

        LevelComplete = true;
        OnLevelComplete?.Invoke();
        Debug.Log("[WaveManager] Level complete.");
    }

    private IEnumerator RunWave(int waveIndex)
    {
        CurrentWaveIndex = waveIndex;
        var waveDef = levelDefinition.waves[waveIndex];

        Debug.Log($"[WaveManager] Starting wave {waveIndex + 1} / {levelDefinition.WaveCount}.");
        OnWaveStarted?.Invoke(waveIndex);

        // Swap the SlotManager's definition to this wave's layout
        slotManager.definition = waveDef.slotDefinition;
        slotManager.ResetSlots();

        _aliveAliens.Clear();

        yield return new WaitForSeconds(waveDef.delayBeforeFirstGroup);

        // Spawn aliens in groups
        int totalAliens = waveDef.alienDefinitions.Length;
        int spawned     = 0;

        while (spawned < totalAliens)
        {
            int groupEnd = Mathf.Min(spawned + waveDef.groupSize, totalAliens);

            for (int i = spawned; i < groupEnd; i++)
                SpawnAlien(waveDef, i);

            spawned = groupEnd;

            if (spawned < totalAliens)
                yield return new WaitForSeconds(waveDef.delayBetweenGroups);
        }

        // Wait until every alien is dead
        yield return new WaitUntil(() => _aliveAliens.Count == 0);

        Debug.Log($"[WaveManager] Wave {waveIndex + 1} cleared.");
        OnWaveCleared?.Invoke(waveIndex);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Spawning
    // ─────────────────────────────────────────────────────────────────────────

    private void SpawnAlien(WaveDefinition waveDef, int alienIndex)
    {
        var def = waveDef.alienDefinitions[alienIndex];
        if (def == null)
        {
            Debug.LogWarning($"[WaveManager] AlienDefinition at index {alienIndex} is null. Skipping.");
            return;
        }

        // Spawn off-screen — AlienController.Start() will place it at the correct spawn edge
        GameObject go      = Instantiate(alienPrefab, Vector3.zero, Quaternion.identity);
        go.name            = $"Alien_W{CurrentWaveIndex}_A{alienIndex}";

        var controller     = go.GetComponent<AlienController>();
        if (controller == null)
        {
            Debug.LogError("[WaveManager] alienPrefab is missing an AlienController component.", go);
            Destroy(go);
            return;
        }

        controller.definition = def;

        // Assign slot before Start() runs (Start is called on the first frame after Instantiate)
        slotManager.AssignNextSlot(controller);

        _aliveAliens.Add(controller);

        // Listen for death so we can track wave completion
        var health = go.GetComponent<AlienHealth>();
        if (health != null)
            health.OnDeath += () => HandleAlienDeath(controller);
        else
            Debug.LogWarning($"[WaveManager] '{go.name}' has no AlienHealth component. " +
                              "Wave completion tracking won't work for this alien.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Death Tracking
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleAlienDeath(AlienController alien)
    {
        slotManager.FreeSlot(alien);
        _aliveAliens.Remove(alien);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Validation
    // ─────────────────────────────────────────────────────────────────────────

    private bool Validate()
    {
        if (levelDefinition == null)
        {
            Debug.LogError("[WaveManager] No LevelDefinition assigned.", this);
            return false;
        }

        if (alienPrefab == null)
        {
            Debug.LogError("[WaveManager] No alien prefab assigned.", this);
            return false;
        }

        if (slotManager == null)
        {
            Debug.LogError("[WaveManager] No SlotManager assigned.", this);
            return false;
        }

        return true;
    }
}