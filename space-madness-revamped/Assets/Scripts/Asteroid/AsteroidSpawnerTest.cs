using UnityEngine;

/// <summary>
/// Quick test for AsteroidSpawner.
///
/// Setup:
///   1. Attach this to any GameObject in the scene.
///   2. Assign the AsteroidSpawner reference in the Inspector.
///   3. Tweak nrSmall / nrMedium / nrLarge as needed.
///   4. Press Play — asteroids spawn automatically.
///
/// Controls (Play mode):
///   R — restart the level (calls StartLevel again)
/// </summary>
public class AsteroidSpawnerTest : MonoBehaviour
{
    [Header("Reference")]
    public AsteroidSpawner asteroidSpawner;

    [Header("Test Config")]
    public int nrSmall  = 5;
    public int nrMedium = 5;
    public int nrLarge  = 5;

    private void Start()
    {
        if (asteroidSpawner == null)
        {
            Debug.LogError("[AsteroidSpawnerTest] No AsteroidSpawner assigned.", this);
            return;
        }

        asteroidSpawner.OnLevelComplete += HandleLevelComplete;

        StartTest();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("[AsteroidSpawnerTest] Restarting level...");
            StartTest();
        }
    }

    private void StartTest()
    {
        Debug.Log($"[AsteroidSpawnerTest] Starting level — Small: {nrSmall} | Medium: {nrMedium} | Large: {nrLarge}");
        asteroidSpawner.StartLevel(nrSmall, nrMedium, nrLarge);
    }

    private void HandleLevelComplete()
    {
        Debug.Log("[AsteroidSpawnerTest] ✓ Level complete! All asteroids destroyed.");
    }
}