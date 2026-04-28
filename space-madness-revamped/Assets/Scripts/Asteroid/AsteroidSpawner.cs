using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns asteroids for a level and tracks completion.
///
/// Flow:
///   1. ChapterManager / Parser calls StartLevel(nrSmall, nrMedium, nrLarge).
///   2. Asteroids are spawned in batches from the top of the screen over time.
///   3. Each asteroid fires OnDestroyed when it dies; the spawner counts them.
///   4. When destroyed == totalToSpawn, OnLevelComplete is fired.
///
/// Setup in Inspector:
///   - Assign smallDefinition, mediumDefinition, largeDefinition.
///   - Assign asteroidPrefab (must have Asteroid + SpriteRenderer + Collider2D).
/// </summary>
public class AsteroidSpawner : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Definitions")]
    [Tooltip("AsteroidDefinition ScriptableObject for small asteroids.")]
    public AsteroidDefinition smallDefinition;

    [Tooltip("AsteroidDefinition ScriptableObject for medium asteroids.")]
    public AsteroidDefinition mediumDefinition;

    [Tooltip("AsteroidDefinition ScriptableObject for large asteroids.")]
    public AsteroidDefinition largeDefinition;

    [Header("Prefab")]
    [Tooltip("Prefab with Asteroid, SpriteRenderer, and Collider2D components.")]
    public GameObject asteroidPrefab;

    [Header("Spawning")]
    [Tooltip("Seconds between each individual asteroid spawn.")]
    [Min(0f)]
    public float spawnInterval = 0.4f;

    [Tooltip("Seconds before the first asteroid spawns after StartLevel is called.")]
    [Min(0f)]
    public float delayBeforeFirstSpawn = 0.5f;

    [Tooltip("How far above the top of the screen asteroids spawn (world units).")]
    [Min(0f)]
    public float spawnHeightOffset = 0.5f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired when every asteroid in the level has been destroyed.
    /// ChapterManager subscribes to this to resume the chapter file.
    /// </summary>
    public event System.Action OnLevelComplete;

    // ─────────────────────────────────────────────────────────────────────────
    //  Public State
    // ─────────────────────────────────────────────────────────────────────────

    public int  TotalToSpawn  { get; private set; }
    public int  Spawned       { get; private set; }
    public int  Destroyed     { get; private set; }
    public bool LevelComplete { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private State
    // ─────────────────────────────────────────────────────────────────────────

    private readonly List<Asteroid> _aliveAsteroids = new();
    private Coroutine               _spawnCoroutine;

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts the asteroid level. Called by ChapterManager / Parser with the parsed values.
    /// Safe to call mid-run — stops any existing spawn routine first.
    /// </summary>
    public void StartLevel(int nrSmall, int nrMedium, int nrLarge)
    {
        if (!Validate()) return;

        // Reset state
        StopSpawning();
        _aliveAsteroids.Clear();

        TotalToSpawn  = nrSmall + nrMedium + nrLarge;
        Spawned       = 0;
        Destroyed     = 0;
        LevelComplete = false;

        if (TotalToSpawn == 0)
        {
            Debug.LogWarning("[AsteroidSpawner] StartLevel called with zero asteroids total.");
            CompleteLevelIfDone();
            return;
        }

        // Build the full spawn queue: large first (most imposing), then medium, then small
        var queue = BuildSpawnQueue(nrSmall, nrMedium, nrLarge);
        _spawnCoroutine = StartCoroutine(SpawnRoutine(queue));
    }

    /// <summary>Stops all spawning and clears alive asteroids. Use when aborting a level.</summary>
    public void StopSpawning()
    {
        if (_spawnCoroutine != null)
        {
            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Spawn Queue
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a flat list of definitions to spawn in order.
    /// Order: large → medium → small (gives the player the big threats first).
    /// Can be shuffled here if you prefer a random mix.
    /// </summary>
    private List<AsteroidDefinition> BuildSpawnQueue(int nrSmall, int nrMedium, int nrLarge)
    {
        var queue = new List<AsteroidDefinition>(TotalToSpawn);

        for (int i = 0; i < nrLarge;  i++) queue.Add(largeDefinition);
        for (int i = 0; i < nrMedium; i++) queue.Add(mediumDefinition);
        for (int i = 0; i < nrSmall;  i++) queue.Add(smallDefinition);

        return queue;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Spawn Coroutine
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator SpawnRoutine(List<AsteroidDefinition> queue)
    {
        yield return new WaitForSeconds(delayBeforeFirstSpawn);

        foreach (var definition in queue)
        {
            SpawnAsteroid(definition);
            yield return new WaitForSeconds(spawnInterval);
        }

        _spawnCoroutine = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Spawn Single Asteroid
    // ─────────────────────────────────────────────────────────────────────────

    private void SpawnAsteroid(AsteroidDefinition definition)
    {
        // Random X position across the top of the screen
        Vector3 spawnPos = GetRandomTopSpawnPosition();

        GameObject go = Instantiate(asteroidPrefab, spawnPos, Quaternion.identity);
        go.name       = $"Asteroid_{definition.size}_{Spawned}";

        var asteroid = go.GetComponent<Asteroid>();
        if (asteroid == null)
        {
            Debug.LogError("[AsteroidSpawner] asteroidPrefab is missing an Asteroid component.", go);
            Destroy(go);
            return;
        }

        // Pick a random downward angle within the definition's configured range
        float angleOffset = Random.Range(definition.minAngleOffset, definition.maxAngleOffset);
        Vector2 direction = AngleOffsetToDirection(angleOffset);

        asteroid.Initialise(definition, direction);
        asteroid.OnDestroyed += () => HandleAsteroidDestroyed(asteroid);

        _aliveAsteroids.Add(asteroid);
        Spawned++;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Destruction Tracking
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleAsteroidDestroyed(Asteroid asteroid)
    {
        _aliveAsteroids.Remove(asteroid);
        Destroyed++;

        CompleteLevelIfDone();
    }

    private void CompleteLevelIfDone()
    {
        // All asteroids must have been spawned AND all must be destroyed
        if (Spawned < TotalToSpawn) return;
        if (_aliveAsteroids.Count > 0) return;

        LevelComplete = true;
        OnLevelComplete?.Invoke();
        Debug.Log("[AsteroidSpawner] Level complete.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns a random world-space position just above the top of the screen.</summary>
    private Vector3 GetRandomTopSpawnPosition()
    {
        float halfH = Camera.main != null ? Camera.main.orthographicSize : 5f;
        float halfW = Camera.main != null ? halfH * Camera.main.aspect   : 8f;

        float x = Random.Range(-halfW, halfW);
        float y = halfH + spawnHeightOffset;

        return new Vector3(x, y, 0f);
    }

    /// <summary>
    /// Converts an angle offset (degrees) from straight down into a world-space direction.
    /// 0 = straight down · negative = left · positive = right.
    /// </summary>
    private Vector2 AngleOffsetToDirection(float offsetDegrees)
    {
        // Base direction is straight down (270°). Add the offset.
        float angleDeg = 270f + offsetDegrees;
        float angleRad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Validation
    // ─────────────────────────────────────────────────────────────────────────

    private bool Validate()
    {
        bool ok = true;

        if (asteroidPrefab  == null) { Debug.LogError("[AsteroidSpawner] No asteroidPrefab assigned.", this);   ok = false; }
        if (smallDefinition == null) { Debug.LogError("[AsteroidSpawner] No smallDefinition assigned.", this);  ok = false; }
        if (mediumDefinition== null) { Debug.LogError("[AsteroidSpawner] No mediumDefinition assigned.", this); ok = false; }
        if (largeDefinition == null) { Debug.LogError("[AsteroidSpawner] No largeDefinition assigned.", this);  ok = false; }

        return ok;
    }
}