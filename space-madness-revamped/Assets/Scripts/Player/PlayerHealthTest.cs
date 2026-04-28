using UnityEngine;

/// <summary>
/// Quick test for PlayerHealth.
///
/// Setup:
///   1. Attach this to any GameObject in the scene.
///   2. Assign the PlayerHealth reference in the Inspector.
///   3. Press Play and use the keys below.
///
/// Controls (Play mode):
///   1 — Deal 1 damage   (small hit — useful for chipping the shield)
///   2 — Deal 2 damage   (medium hit)
///   5 — Deal 5 damage   (heavy hit — breaks a 6-HP shield in two hits)
///   R — Reset health    (full HP + shield restored)
///   S — Print status    (logs current HP, shield, and state to Console)
/// </summary>
public class PlayerHealthTest : MonoBehaviour
{
    [Header("Reference")]
    public PlayerHealth playerHealth;

    private void Start()
    {
        if (playerHealth == null)
        {
            Debug.LogError("[PlayerHealthTest] No PlayerHealth assigned.", this);
            return;
        }

        playerHealth.OnHullChanged   += (cur, max) => Debug.Log($"[PlayerHealthTest] Hull changed  → {cur} / {max}");
        playerHealth.OnShieldChanged += (cur, max) => Debug.Log($"[PlayerHealthTest] Shield changed → {cur:F1} / {max}");
        playerHealth.OnShieldBroken  += ()          => Debug.LogWarning("[PlayerHealthTest] ⚠ Shield BROKEN — recharge started.");
        playerHealth.OnShieldBooted  += ()          => Debug.Log("[PlayerHealthTest] ✓ Shield REBOOTED at 50%.");
        playerHealth.OnDeath         += ()          => Debug.LogError("[PlayerHealthTest] ✕ Player DEAD — Game Over.");

        Debug.Log("[PlayerHealthTest] Ready. Keys: [1] dmg 1 | [2] dmg 2 | [5] dmg 5 | [R] reset | [S] status");
    }

    private void Update()
    {
        if (playerHealth == null) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) DealDamage(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) DealDamage(2);
        if (Input.GetKeyDown(KeyCode.Alpha5)) DealDamage(5);

        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("[PlayerHealthTest] Resetting health...");
            playerHealth.ResetHealth();
        }

        if (Input.GetKeyDown(KeyCode.S))
            PrintStatus();
    }

    private void DealDamage(int amount)
    {
        Debug.Log($"[PlayerHealthTest] Dealing {amount} damage...");
        playerHealth.TakeDamage(amount);
    }

    private void PrintStatus()
    {
        string shieldState = playerHealth.ShieldActive ? "ACTIVE" : "BROKEN/RECHARGING";

        Debug.Log(
            $"[PlayerHealthTest] STATUS\n" +
            $"  Hull:          {playerHealth.CurrentHP} / {playerHealth.maxHP}\n" +
            $"  Shield:        {playerHealth.CurrentShield:F1} / {playerHealth.maxShield}  [{shieldState}]\n" +
            $"  Invincible:    {playerHealth.IsInvincible}\n" +
            $"  Dead:          {playerHealth.IsDead}"
        );
    }
}