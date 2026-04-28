using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Connects PlayerHealth events to the HUD bars built by DialogueHUDBuilder.
///
/// Inspector wiring (drag from your Canvas hierarchy):
///   healthFill    → HUD/BottomBar/StatsPanel/HealthRow/BarBG/Fill
///   healthText    → HUD/BottomBar/StatsPanel/HealthRow/ValueText
///   shieldFill    → HUD/BottomBar/StatsPanel/ShieldRow/BarBG/Fill
///   shieldText    → HUD/BottomBar/StatsPanel/ShieldRow/ValueText
///   playerHealth  → the PlayerHealth component on the player GameObject
///
/// This component purely reflects state — all logic lives in PlayerHealth.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Health Bar")]
    [Tooltip("The Fill image inside HealthRow/BarBG.")]
    public Image             healthFill;

    [Tooltip("The ValueText TMP inside HealthRow.")]
    public TextMeshProUGUI   healthText;

    [Header("Shield Bar")]
    [Tooltip("The Fill image inside ShieldRow/BarBG.")]
    public Image             shieldFill;

    [Tooltip("The ValueText TMP inside ShieldRow.")]
    public TextMeshProUGUI   shieldText;

    [Header("Player")]
    [Tooltip("The PlayerHealth component on the player GameObject.")]
    public PlayerHealth      playerHealth;

    [Header("Shield Broken Tint")]
    [Tooltip("Color the shield bar turns when the shield is broken and recharging.")]
    public Color             shieldBrokenColor = new Color(0.12f, 0.18f, 0.35f, 1f);

    [Tooltip("Normal shield bar color (matches DialogueHUDBuilder C_SHIELD = #3070C8).")]
    public Color             shieldActiveColor = new Color(0.19f, 0.44f, 0.78f, 1f);

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (playerHealth == null)
        {
            Debug.LogError("[PlayerHUD] No PlayerHealth assigned.", this);
            enabled = false;
            return;
        }

        // Subscribe to all PlayerHealth events
        playerHealth.OnHullChanged   += HandleHullChanged;
        playerHealth.OnShieldChanged += HandleShieldChanged;
        playerHealth.OnShieldBroken  += HandleShieldBroken;
        playerHealth.OnShieldBooted  += HandleShieldBooted;
    }

    private void OnDestroy()
    {
        if (playerHealth == null) return;

        playerHealth.OnHullChanged   -= HandleHullChanged;
        playerHealth.OnShieldChanged -= HandleShieldChanged;
        playerHealth.OnShieldBroken  -= HandleShieldBroken;
        playerHealth.OnShieldBooted  -= HandleShieldBooted;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleHullChanged(int current, int max)
    {
        if (healthFill != null)
        {
            float t = max > 0 ? (float)current / max : 0f;
            healthFill.fillAmount = t;  // works if Image Type is Filled
            healthFill.transform.localScale = new Vector3(t, 1f, 1f); // works for plain Images
        }

        if (healthText != null)
            healthText.text = $"{current} / {max}";
    }

    private void HandleShieldChanged(float current, float max)
    {
        if (shieldFill != null)
        {
            float t = max > 0 ? current / max : 0f;
            shieldFill.fillAmount = t;  // works if Image Type is Filled
            shieldFill.transform.localScale = new Vector3(t, 1f, 1f); // works for plain Images
        }

        if (shieldText != null)
        {
            // Display as whole numbers — the float precision is internal only
            int displayCurrent = Mathf.CeilToInt(current);
            int displayMax     = Mathf.RoundToInt(max);
            shieldText.text    = $"{displayCurrent} / {displayMax}";
        }
    }

    private void HandleShieldBroken()
    {
        // Dim the shield bar to signal it's offline
        if (shieldFill != null)
            shieldFill.color = shieldBrokenColor;

        if (shieldText != null)
        {
            var c = shieldText.color;
            c.a = 0.4f;
            shieldText.color = c;
        }
    }

    private void HandleShieldBooted()
    {
        // Restore shield bar to its active color
        if (shieldFill != null)
            shieldFill.color = shieldActiveColor;

        if (shieldText != null)
        {
            var c = shieldText.color;
            c.a = 1f;
            shieldText.color = c;
        }
    }
}