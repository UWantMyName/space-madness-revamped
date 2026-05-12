using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Reads a chapter text file line by line and orchestrates the DialogueAdapter,
/// WaveManager, and AsteroidSpawner in strict sequence.
///
/// Flow:
///   - Consecutive dialogue lines are batched and played together as one sequence.
///   - When a non-dialogue line is hit, any pending dialogue batch is flushed first.
///   - [LEVEL_START] ... [LEVEL_END] blocks are parsed into RuntimeLevelData and
///     handed to WaveManager.StartLevel().
///   - [LEVEL: asteroids | ...] lines are handed to AsteroidSpawner.StartLevel().
///   - Each system fires a completion event; the parser waits before advancing.
///
/// Chapter file format:
///
///   // This is a comment
///   COMMAND: Apophis, do you copy?
///   PLAYER: Loud and clear, sir.
///
///   [LEVEL_START | delay_between_waves: 2.0]
///       [GROUP | count: 20 | batch_size: 5 | delay_first: 0.5 | delay_between: 1.5]
///           ALIEN | type: FastAlien | health: 10
///           PATH
///               [SEG | type: linear | angle: 270 | distance: 0.3 | speed: 6]
///               [SEG | type: arc    | turn: left  | sweep: 90    | distance: 0.4 | speed: 5]
///           PATH_END
///       GROUP_END
///   [LEVEL_END]
///
///   [LEVEL: asteroids | nr_small: 10 | nr_medium: 30 | nr_large: 50]
/// </summary>
public class ChapterParser : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Systems")]
    [Tooltip("DialogueAdapter in the scene.")]
    public DialogueAdapter dialogueAdapter;

    [Tooltip("WaveManager in the scene.")]
    public WaveManager waveManager;

    [Tooltip("AsteroidSpawner in the scene.")]
    public AsteroidSpawner asteroidSpawner;

    [Header("Chapter File")]
    [Tooltip("Drag your chapter .txt asset here.")]
    public TextAsset chapterFile;

    [Header("Debug")]
    [Tooltip("Log every line as it is processed.")]
    public bool verboseLogging = false;

    // ─────────────────────────────────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Fired when the last line of the chapter has been processed.</summary>
    public event System.Action OnChapterComplete;

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Starts reading and executing the chapter file.</summary>
    public void StartChapter()
    {
        if (chapterFile == null)
        {
            Debug.LogError("[ChapterParser] No chapter file assigned.", this);
            return;
        }

        if (!ValidateReferences()) return;

        string[] lines = chapterFile.text.Split('\n');
        print("Started chapter.");
        StartCoroutine(RunChapter(lines));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Main Coroutine
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator RunChapter(string[] lines)
    {
        var dialogueBatch = new List<(string actor, string text)>();
        int i = 0;

        while (i < lines.Length)
        {
            string line = lines[i].Trim();

            // Skip empty lines and comments
            if (string.IsNullOrEmpty(line) || line.StartsWith("//"))
            {
                i++;
                continue;
            }

            if (verboseLogging)
                Debug.Log($"[ChapterParser] Line {i}: {line}");

            // ── Dialogue line ─────────────────────────────────────────────
            if (IsDialogueLine(line))
            {
                dialogueBatch.Add(ParseDialogueLine(line));
                i++;
                continue;
            }

            // ── Non-dialogue: flush any pending dialogue first ─────────────
            if (dialogueBatch.Count > 0)
            {
                yield return PlayDialogue(dialogueBatch);
                dialogueBatch.Clear();
            }

            // ── Alien level block ─────────────────────────────────────────
            if (line.StartsWith("[LEVEL_START"))
            {
                int blockEnd = FindClosingLine(lines, i + 1, "LEVEL_END");
                var levelData = ParseLevelBlock(lines, i, blockEnd);
                i = blockEnd + 1;

                if (levelData != null)
                    yield return RunAlienLevel(levelData);
            }
            // ── Asteroid level line ───────────────────────────────────────
            else if (line.StartsWith("[LEVEL:"))
            {
                var kv      = ParseKV(line);
                string sub  = ParseString(kv, "_subtype", "");

                if (sub == "asteroids")
                {
                    int nrSmall  = ParseInt(kv, "nr_small",  0);
                    int nrMedium = ParseInt(kv, "nr_medium", 0);
                    int nrLarge  = ParseInt(kv, "nr_large",  0);
                    i++;
                    yield return RunAsteroidLevel(nrSmall, nrMedium, nrLarge);
                }
                else
                {
                    Debug.LogWarning($"[ChapterParser] Unknown LEVEL subtype '{sub}' on line {i}. Skipping.");
                    i++;
                }
            }
            else
            {
                Debug.LogWarning($"[ChapterParser] Unrecognised line {i}: '{line}' — skipping.");
                i++;
            }
        }

        // Flush any trailing dialogue
        if (dialogueBatch.Count > 0)
            yield return PlayDialogue(dialogueBatch);

        Debug.Log("[ChapterParser] Chapter complete.");
        OnChapterComplete?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  System Runners
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator PlayDialogue(List<(string actor, string text)> batch)
    {
        bool done = false;
        System.Action onComplete = () => done = true;

        dialogueAdapter.OnSequenceComplete += onComplete;
        dialogueAdapter.Play(new List<(string, string)>(batch));

        yield return new WaitUntil(() => done);

        dialogueAdapter.OnSequenceComplete -= onComplete;
    }

    private IEnumerator RunAlienLevel(RuntimeLevelData data)
    {
        bool done = false;
        System.Action onComplete = () => done = true;

        waveManager.OnLevelComplete += onComplete;
        waveManager.StartLevel(data);

        yield return new WaitUntil(() => done);

        waveManager.OnLevelComplete -= onComplete;
    }

    private IEnumerator RunAsteroidLevel(int nrSmall, int nrMedium, int nrLarge)
    {
        bool done = false;
        System.Action onComplete = () => done = true;

        asteroidSpawner.OnLevelComplete += onComplete;
        asteroidSpawner.StartLevel(nrSmall, nrMedium, nrLarge);

        yield return new WaitUntil(() => done);

        asteroidSpawner.OnLevelComplete -= onComplete;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Level Block Parser
    // ─────────────────────────────────────────────────────────────────────────

    private RuntimeLevelData ParseLevelBlock(string[] lines, int startLine, int endLine)
    {
        var data = new RuntimeLevelData();

        var kv = ParseKV(lines[startLine].Trim());
        data.DelayBetweenWaves = ParseFloat(kv, "delay_between_waves", 2f);

        int i = startLine + 1;
        while (i < endLine)
        {
            string line = lines[i].Trim();

            if (string.IsNullOrEmpty(line) || line.StartsWith("//")) { i++; continue; }

            if (line.StartsWith("[GROUP") || line.StartsWith("GROUP"))
            {
                // Skip the opening GROUP line itself only if it's already a tag with no colon issue
                int groupEnd  = FindClosingLine(lines, i + 1, "GROUP_END");
                var groupData = ParseGroupBlock(lines, i, groupEnd);

                if (groupData != null)
                    data.Groups.Add(groupData);

                i = groupEnd + 1;
            }
            else
            {
                i++;
            }
        }

        if (data.Groups.Count == 0)
            Debug.LogWarning("[ChapterParser] LEVEL_START block parsed with no valid GROUP entries.");

        return data;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Group Block Parser
    // ─────────────────────────────────────────────────────────────────────────

    private RuntimeGroupData ParseGroupBlock(string[] lines, int startLine, int endLine)
    {
        var group = new RuntimeGroupData();

        var kv = ParseKV(lines[startLine].Trim());
        group.Count                 = ParseInt  (kv, "count",         1);
        group.BatchSize             = ParseInt  (kv, "batch_size",    5);
        group.DelayBeforeFirstBatch = ParseFloat(kv, "delay_first",   0.5f);
        group.DelayBetweenBatches   = ParseFloat(kv, "delay_between", 1.5f);
        group.StartDelay            = ParseFloat(kv, "start_delay",   0f);

        int i = startLine + 1;
        while (i < endLine)
        {
            string line = lines[i].Trim();

            if (string.IsNullOrEmpty(line) || line.StartsWith("//")) { i++; continue; }

            // ALIEN line
            if (line.StartsWith("ALIEN"))
            {
                var alienKV               = ParseKV(line);
                group.AlienDefinitionName = ParseString(alienKV, "type",   "");
                group.HealthOverride      = ParseInt   (alienKV, "health", -1);
                i++;
            }
            // PATH block
            else if (line == "PATH")
            {
                int pathEnd  = FindClosingLine(lines, i + 1, "PATH_END");
                group.Path   = ParsePathBlock(lines, i + 1, pathEnd);
                i            = pathEnd + 1;
            }
            else
            {
                i++;
            }
        }

        // Validation
        if (string.IsNullOrEmpty(group.AlienDefinitionName))
        {
            Debug.LogWarning("[ChapterParser] GROUP block is missing an ALIEN line — group skipped.");
            return null;
        }

        if (group.Path == null || group.Path.segments == null || group.Path.segments.Length == 0)
        {
            Debug.LogWarning($"[ChapterParser] GROUP '{group.AlienDefinitionName}' has no PATH segments — group skipped.");
            return null;
        }

        return group;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Path Block Parser
    // ─────────────────────────────────────────────────────────────────────────

    private AlienRuntimePath ParsePathBlock(string[] lines, int startLine, int endLine)
    {
        var path     = new AlienRuntimePath();
        var segments = new List<AlienSegment>();

        // Defaults — can be extended later with a SPAWN line inside the PATH block
        path.spawnEdge           = SpawnEdge.Top;
        path.spawnPosition       = 0.5f;
        path.previewScreenHeight = 10f;

        for (int i = startLine; i < endLine; i++)
        {
            string line = lines[i].Trim();

            if (string.IsNullOrEmpty(line) || line.StartsWith("//")) continue;

            // SPAWN override: SPAWN | edge: top | position: 0.3
            if (line.StartsWith("SPAWN"))
            {
                var spawnKV      = ParseKV(line);
                string edgeStr   = ParseString(spawnKV, "edge", "top");
                path.spawnEdge   = edgeStr switch
                {
                    "left"  => SpawnEdge.Left,
                    "right" => SpawnEdge.Right,
                    _       => SpawnEdge.Top
                };
                path.spawnPosition = ParseFloat(spawnKV, "position", 0.5f);
                continue;
            }

            if (!line.StartsWith("[SEG")) continue;

            var kv  = ParseKV(line);
            var seg = new AlienSegment();

            string type = ParseString(kv, "type", "linear");
            seg.type    = type == "arc" ? SegmentType.Arc : SegmentType.Linear;

            if (seg.type == SegmentType.Linear)
            {
                seg.angleInDegrees   = ParseFloat(kv, "angle",    270f);
                seg.distanceFraction = ParseFloat(kv, "distance", 0.3f);
                seg.speed            = ParseFloat(kv, "speed",    4f);
            }
            else // Arc
            {
                string turn        = ParseString(kv, "turn", "left");
                seg.turnDirection  = turn == "right" ? TurnDirection.Right : TurnDirection.Left;
                seg.sweepDegrees   = ParseFloat(kv, "sweep",    90f);
                seg.distanceFraction = ParseFloat(kv, "distance", 0.3f);
                seg.speed          = ParseFloat(kv, "speed",    4f);
            }

            segments.Add(seg);
        }

        path.segments = segments.ToArray();
        return path;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Dialogue Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly Regex _dialogueRegex = new Regex(@"^([A-Za-z]+):\s(.+)$", RegexOptions.Compiled);

    /// <summary>
    /// A dialogue line is: WORD: text
    /// It must not start with '[' and must not contain '|'.
    /// </summary>
    private bool IsDialogueLine(string line)
    {
        if (line.StartsWith("[") || line.Contains("|")) return false;
        return _dialogueRegex.IsMatch(line);
    }

    private (string actor, string text) ParseDialogueLine(string line)
    {
        var match = _dialogueRegex.Match(line);
        return match.Success
            ? (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim())
            : ("UNKNOWN", line);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Line Search Helper
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the index of the first line at or after startLine that contains keyword.
    /// Handles both [KEYWORD] and plain KEYWORD forms.
    /// </summary>
    private int FindClosingLine(string[] lines, int startLine, string keyword)
    {
        for (int i = startLine; i < lines.Length; i++)
        {
            if (lines[i].Contains(keyword))
                return i;
        }

        Debug.LogWarning($"[ChapterParser] Closing tag '{keyword}' not found from line {startLine}.");
        return lines.Length - 1;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  KV Parser
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a pipe-separated line into a string dictionary.
    ///
    /// Handles:
    ///   [LEVEL_START | key: value]     → _tag = "LEVEL_START"
    ///   [LEVEL: asteroids | key: value] → _tag = "LEVEL", _subtype = "asteroids"
    ///   ALIEN | type: FastAlien        → _tag = "ALIEN"
    ///   [SEG | type: linear | ...]     → _tag = "SEG"
    /// </summary>
    private Dictionary<string, string> ParseKV(string line)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Strip surrounding brackets
        line = line.TrimStart('[').TrimEnd(']');

        string[] tokens = line.Split('|');

        for (int t = 0; t < tokens.Length; t++)
        {
            string token = tokens[t].Trim();
            if (string.IsNullOrEmpty(token)) continue;

            int colon = token.IndexOf(':');

            if (colon < 0)
            {
                // No colon — this is a bare tag name (e.g. "LEVEL_START", "GROUP_END")
                result["_tag"] = token;
            }
            else if (t == 0)
            {
                // First token with a colon — tag name is before colon, value is subtype
                // e.g. "LEVEL: asteroids" → _tag = LEVEL, _subtype = asteroids
                result["_tag"]     = token[..colon].Trim();
                result["_subtype"] = token[(colon + 1)..].Trim();
            }
            else
            {
                result[token[..colon].Trim()] = token[(colon + 1)..].Trim();
            }
        }

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Typed Parse Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private int ParseInt(Dictionary<string, string> kv, string key, int fallback)
    {
        return kv.TryGetValue(key, out string v) && int.TryParse(v, out int r) ? r : fallback;
    }

    private float ParseFloat(Dictionary<string, string> kv, string key, float fallback)
    {
        return kv.TryGetValue(key, out string v) &&
               float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float r)
            ? r : fallback;
    }

    private string ParseString(Dictionary<string, string> kv, string key, string fallback)
    {
        return kv.TryGetValue(key, out string v) ? v : fallback;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Validation
    // ─────────────────────────────────────────────────────────────────────────

    private bool ValidateReferences()
    {
        bool ok = true;

        if (dialogueAdapter == null) { Debug.LogError("[ChapterParser] DialogueAdapter not assigned.", this); ok = false; }
        if (waveManager     == null) { Debug.LogError("[ChapterParser] WaveManager not assigned.",     this); ok = false; }
        if (asteroidSpawner == null) { Debug.LogError("[ChapterParser] AsteroidSpawner not assigned.", this); ok = false; }

        return ok;
    }
}