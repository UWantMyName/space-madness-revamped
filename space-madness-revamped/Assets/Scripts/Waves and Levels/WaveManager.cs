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

    [Tooltip("Prefabs with AlienController on them. One is picked at random per spawn.")]
    public List<GameObject> alienPrefabs = new();

    [Tooltip("SlotManager in the scene. Its definition is swapped per wave.")]
    public SlotManager slotManager;

    [Header("Runtime Library")]
    [Tooltip("All AlienDefinition assets available to runtime levels.\n" +
             "The asset name must match the 'type:' value in the chapter file.")]
    public System.Collections.Generic.List<AlienDefinition> alienDefinitionLibrary = new();

    [Header("Behaviour")]
    [Tooltip("If true, starts the ScriptableObject-based level automatically on Play.\n" +
             "Set to false when the ChapterParser drives the level via StartLevel(RuntimeLevelData).")]
    public bool autoStartOnPlay = true;

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
        if (autoStartOnPlay)
        {
            if (!Validate()) return;
            StartCoroutine(RunLevel());
        }
    }

    private GameObject GetRandomPrefab()
    {
        return alienPrefabs[Random.Range(0, alienPrefabs.Count)];
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API — Runtime Entry Point
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts a level from runtime data built by the ChapterParser.
    /// Call this instead of relying on autoStartOnPlay when the chapter file drives the level.
    /// </summary>
    public void StartLevel(RuntimeLevelData data)
    {
        StartCoroutine(RunLevel(data));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Level Sequence
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator RunLevel()
    {
        for (int i = 0; i < levelDefinition.WaveCount; i++)
        {
            yield return RunWave(i);

            if (i < levelDefinition.WaveCount - 1)
                yield return new WaitForSeconds(levelDefinition.delayBetweenWaves);
        }

        LevelComplete = true;
        OnLevelComplete?.Invoke();
        Debug.Log("[WaveManager] Level complete.");
    }

    private IEnumerator RunLevel(RuntimeLevelData data)
    {
        // Count total aliens across all groups so the slot pool covers everyone
        int totalAliens = 0;
        foreach (var g in data.Groups) totalAliens += g.Count;

        Vector2[] slots = SlotGridGenerator.Generate(totalAliens);
        slotManager.SetRuntimeSlots(slots);          // set once — not per-group

        int remaining = data.Groups.Count;

        foreach (var group in data.Groups)
            StartCoroutine(RunGroup(group, () => remaining--));

        yield return new WaitUntil(() => remaining <= 0);
        yield return new WaitUntil(() =>
            FindObjectsByType<AlienController>(FindObjectsSortMode.None).Length == 0);

        LevelComplete = true;
        OnLevelComplete?.Invoke();
        Debug.Log("[WaveManager] Runtime level complete.");
    }

    private IEnumerator RunGroup(RuntimeGroupData group, System.Action onDone)
    {
        if (group.StartDelay > 0f)
            yield return new WaitForSeconds(group.StartDelay);

        // Resolve definition here — each group may use a different alien type
        AlienDefinition def = FindDefinition(group.AlienDefinitionName);
        if (def == null)
        {
            Debug.LogError($"[WaveManager] AlienDefinition '{group.AlienDefinitionName}' not found. Skipping group.");
            onDone?.Invoke();
            yield break;
        }

        if (group.DelayBeforeFirstBatch > 0f)
            yield return new WaitForSeconds(group.DelayBeforeFirstBatch);

        int spawned     = 0;
        int alienIndex  = 0;

        while (spawned < group.Count)
        {
            int batchCount = Mathf.Min(group.BatchSize, group.Count - spawned);

            for (int i = 0; i < batchCount; i++)
                SpawnRuntimeAlien(def, group, CurrentWaveIndex, alienIndex++);

            spawned += batchCount;

            if (spawned < group.Count)
                yield return new WaitForSeconds(group.DelayBetweenBatches);
        }

        onDone?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Runtime Level Sequence
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator RunRuntimeLevel(RuntimeLevelData data)
    {
        for (int i = 0; i < data.GroupCount; i++)
        {
            yield return RunRuntimeGroup(i, data.Groups[i]);

            if (i < data.GroupCount - 1)
                yield return new WaitForSeconds(data.DelayBetweenWaves);
        }

        LevelComplete = true;
        OnLevelComplete?.Invoke();
        Debug.Log("[WaveManager] Runtime level complete.");
    }

    private IEnumerator RunRuntimeGroup(int groupIndex, RuntimeGroupData group)
    {
        CurrentWaveIndex = groupIndex;

        Debug.Log($"[WaveManager] Starting group {groupIndex + 1} — {group.Count} aliens.");
        OnWaveStarted?.Invoke(groupIndex);

        // Resolve the AlienDefinition by name from the library
        AlienDefinition def = FindDefinition(group.AlienDefinitionName);
        if (def == null)
        {
            Debug.LogError($"[WaveManager] AlienDefinition '{group.AlienDefinitionName}' not found in library. Skipping group.");
            OnWaveCleared?.Invoke(groupIndex);
            yield break;
        }

        // Auto-generate slots for this group
        Vector2[] slots = SlotGridGenerator.Generate(group.Count);
        slotManager.SetRuntimeSlots(slots);

        _aliveAliens.Clear();

        yield return new WaitForSeconds(group.DelayBeforeFirstBatch);

        int spawned = 0;
        while (spawned < group.Count)
        {
            int batchEnd = Mathf.Min(spawned + group.BatchSize, group.Count);

            for (int i = spawned; i < batchEnd; i++)
                SpawnRuntimeAlien(def, group, groupIndex, i);

            spawned = batchEnd;

            if (spawned < group.Count)
                yield return new WaitForSeconds(group.DelayBetweenBatches);
        }

        yield return new WaitUntil(() => _aliveAliens.Count == 0);

        Debug.Log($"[WaveManager] Group {groupIndex + 1} cleared.");
        OnWaveCleared?.Invoke(groupIndex);
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

        GameObject go = Instantiate(GetRandomPrefab(), Vector3.zero, Quaternion.identity);
        go.name        = $"Alien_W{CurrentWaveIndex}_A{alienIndex}";

        var controller = go.GetComponent<AlienController>();
        if (controller == null)
        {
            Debug.LogError("[WaveManager] alienPrefab is missing an AlienController component.", go);
            Destroy(go);
            return;
        }

        controller.definition = def;
        // NOTE: RuntimePath must be set by the caller when using ScriptableObject-based levels.
        // For now the ScriptableObject path leaves RuntimePath null — this will be addressed
        // when ScriptableObject-based levels are deprecated in favour of the chapter file system.

        slotManager.AssignNextSlot(controller);
        _aliveAliens.Add(controller);

        var health = go.GetComponent<AlienHealth>();
        if (health != null)
            health.OnDeath += () => HandleAlienDeath(controller);
        else
            Debug.LogWarning($"[WaveManager] '{go.name}' has no AlienHealth component.");
    }

    private void SpawnRuntimeAlien(AlienDefinition def, RuntimeGroupData group, int groupIndex, int alienIndex)
    {
        GameObject go = Instantiate(GetRandomPrefab(), Vector3.zero, Quaternion.identity);
        go.name        = $"Alien_G{groupIndex}_A{alienIndex}";

        var controller = go.GetComponent<AlienController>();
        if (controller == null)
        {
            Debug.LogError("[WaveManager] alienPrefab is missing an AlienController component.", go);
            Destroy(go);
            return;
        }

        controller.definition  = def;
        controller.RuntimePath = group.Path;

        slotManager.AssignNextSlot(controller);
        _aliveAliens.Add(controller);

        // Apply health override if specified
        var health = go.GetComponent<AlienHealth>();
        if (health != null)
        {
            if (group.HealthOverride > 0)
                health.SetMaxHP(group.HealthOverride);

            health.OnDeath += () => HandleAlienDeath(controller);
        }
        else
        {
            Debug.LogWarning($"[WaveManager] '{go.name}' has no AlienHealth component.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Library Lookup
    // ─────────────────────────────────────────────────────────────────────────

    private AlienDefinition FindDefinition(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return null;

        foreach (var def in alienDefinitionLibrary)
        {
            if (def != null && def.name == typeName)
                return def;
        }

        return null;
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

        // Validate() and ValidateRuntime() — replace the alienPrefab null check with:
        if (alienPrefabs == null || alienPrefabs.Count == 0)
        {
            Debug.LogError("[WaveManager] No alien prefabs assigned.", this);
            return false;
        }

        if (slotManager == null)
        {
            Debug.LogError("[WaveManager] No SlotManager assigned.", this);
            return false;
        }

        return true;
    }

    private bool ValidateRuntime()
    {
        // Validate() and ValidateRuntime() — replace the alienPrefab null check with:
        if (alienPrefabs == null || alienPrefabs.Count == 0)
        {
            Debug.LogError("[WaveManager] No alien prefabs assigned.", this);
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