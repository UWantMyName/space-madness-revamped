using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────
//  Actor Profile
// ─────────────────────────────────────────────

/// <summary>
/// Maps an actor name (as it appears in the chapter file) to a sprite and accent color.
/// Add one entry per character in the DialogueAdapter inspector.
/// </summary>
[System.Serializable]
public class ActorProfile
{
    [Tooltip("Must match the name in the chapter file exactly — e.g. COMMAND, PLAYER, APOPHIS")]
    public string actorName;

    [Tooltip("Portrait sprite for this actor. Leave empty for no icon.")]
    public Sprite icon;

    [Tooltip("Accent color used for the actor name and cursor.")]
    public Color  accentColor = Color.white;
}

// ─────────────────────────────────────────────
//  Dialogue Adapter
// ─────────────────────────────────────────────

/// <summary>
/// Bridge between the ChapterParser and the DialogueSystem.
///
/// The parser reads raw lines like:
///     COMMAND: Apophis, do you copy?
///     PLAYER: Loud and clear, sir.
///
/// It collects them into a batch and calls Play() here.
/// The adapter looks up each actor's sprite and color from the registry,
/// builds the DialogueLine list, and hands it to DialogueSystem.
///
/// Setup:
///   1. Assign dialogueSystem in the Inspector.
///   2. Add one ActorProfile per character in actorProfiles.
///      The name must match exactly what appears in the chapter file.
///   3. ChapterParser calls dialogueAdapter.Play(rawLines) and subscribes
///      to OnSequenceComplete to know when to resume.
/// </summary>
public class DialogueAdapter : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The DialogueSystem in the scene.")]
    public DialogueSystem dialogueSystem;

    [Header("Actor Registry")]
    [Tooltip("One entry per character. Name must match the chapter file exactly.")]
    public List<ActorProfile> actorProfiles = new();

    [Header("Fallback")]
    [Tooltip("Accent color used for actors not found in the registry.")]
    public Color fallbackColor = Color.white;

    // ─────────────────────────────────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Forwarded from DialogueSystem.OnSequenceComplete.
    /// ChapterParser subscribes here to resume reading the chapter file.
    /// </summary>
    public event System.Action OnSequenceComplete;

    // ─────────────────────────────────────────────────────────────────────────
    //  Private
    // ─────────────────────────────────────────────────────────────────────────

    // Cached lookup built once from actorProfiles on Awake
    private Dictionary<string, ActorProfile> _registry;

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (dialogueSystem == null)
        {
            Debug.LogError("[DialogueAdapter] No DialogueSystem assigned.", this);
            enabled = false;
            return;
        }

        BuildRegistry();

        // Forward the completion event so the parser only needs to know about the adapter
        dialogueSystem.OnSequenceComplete += () => OnSequenceComplete?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Takes a batch of raw (actorName, text) pairs parsed from the chapter file,
    /// builds DialogueLines with the correct sprites and colors, and plays them.
    /// </summary>
    public void Play(List<(string actorName, string text)> rawLines)
    {
        if (rawLines == null || rawLines.Count == 0)
        {
            Debug.LogWarning("[DialogueAdapter] Play called with no lines.");
            OnSequenceComplete?.Invoke();
            return;
        }

        var lines = new List<DialogueLine>(rawLines.Count);

        foreach (var (actorName, text) in rawLines)
        {
            var profile = LookupProfile(actorName);

            lines.Add(new DialogueLine
            {
                actorName   = actorName,
                actorIcon   = profile?.icon,
                accentColor = profile != null ? profile.accentColor : fallbackColor,
                text        = text
            });
        }

        dialogueSystem.Play(lines);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildRegistry()
    {
        _registry = new Dictionary<string, ActorProfile>(System.StringComparer.OrdinalIgnoreCase);

        foreach (var profile in actorProfiles)
        {
            if (string.IsNullOrWhiteSpace(profile.actorName))
            {
                Debug.LogWarning("[DialogueAdapter] ActorProfile with empty name — skipping.", this);
                continue;
            }

            if (_registry.ContainsKey(profile.actorName))
            {
                Debug.LogWarning($"[DialogueAdapter] Duplicate actor name '{profile.actorName}' — keeping first.", this);
                continue;
            }

            _registry[profile.actorName] = profile;
        }
    }

    private ActorProfile LookupProfile(string actorName)
    {
        if (_registry.TryGetValue(actorName, out var profile))
            return profile;

        Debug.LogWarning($"[DialogueAdapter] No profile found for actor '{actorName}'. Using fallback color, no icon.", this);
        return null;
    }
}