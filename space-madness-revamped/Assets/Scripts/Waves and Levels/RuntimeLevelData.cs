using System.Collections.Generic;

// ============================================================
//  RuntimeLevelData.cs
//
//  Plain C# data classes that the ChapterParser populates
//  from a chapter file's level block. No ScriptableObjects needed.
//
//  Chapter file format these classes represent:
//
//  [LEVEL_START | delay_between_waves: 2.0]
//
//  [GROUP | count: 20 | batch_size: 5 | delay_first: 0.5 | delay_between: 1.5]
//  ALIEN | type: FastAlien | health: 10
//  PATH
//      [SEG | type: linear | angle: 270 | distance: 0.3 | speed: 6]
//      [SEG | type: arc    | turn: left | sweep: 90     | distance: 0.4 | speed: 5]
//  PATH_END
//  GROUP_END
//
//  [LEVEL_END]
//
//  Notes:
//    - Each GROUP becomes one wave. Next wave starts when all aliens in the current wave are dead.
//    - ALIEN type maps to an AlienDefinition asset name in WaveManager's library.
//    - health: -1 means "use the definition's own maxHP value".
//    - PATH segments mirror the AlienSegment / AlienRuntimePath structure exactly.
//    - Slots are auto-generated from the group count — never hand-authored.
// ============================================================

/// <summary>
/// Runtime representation of a full level.
/// Passed to WaveManager.StartLevel() by the ChapterParser.
/// </summary>
public class RuntimeLevelData
{
    /// <summary>Seconds to wait between waves (after all aliens die, before next group spawns).</summary>
    public float DelayBetweenWaves = 2f;

    /// <summary>Ordered list of groups. Each group = one wave.</summary>
    public List<RuntimeGroupData> Groups = new();

    public int GroupCount => Groups != null ? Groups.Count : 0;
}

/// <summary>
/// Runtime representation of a single group (wave).
///
/// All aliens in a group share the same AlienDefinition and entry path.
/// They are spawned in batches of BatchSize with DelayBetweenBatches seconds between each batch.
/// </summary>
public class RuntimeGroupData
{
    /// <summary>Total number of aliens in this group. Also determines slot count.</summary>
    public int Count = 1;

    /// <summary>
    /// Name of the AlienDefinition asset to use for all aliens in this group.
    /// Must match an entry in WaveManager.alienDefinitionLibrary.
    /// </summary>
    public string AlienDefinitionName = "";

    /// <summary>
    /// HP override for this group. Set to -1 to use the AlienDefinition's own maxHP.
    /// NOTE: HP overriding requires AlienHealth to read from AlienController at runtime
    ///       rather than from the definition directly — handled in SpawnAlien.
    /// </summary>
    public int HealthOverride = -1;

    /// <summary>How many aliens spawn simultaneously per batch.</summary>
    public int BatchSize = 5;

    /// <summary>Seconds before the first batch spawns.</summary>
    public float DelayBeforeFirstBatch = 0.5f;

    /// <summary>Seconds between each batch spawning.</summary>
    public float DelayBetweenBatches = 1.5f;

    /// <summary>
    /// The entry path all aliens in this group follow.
    /// Built by the ChapterParser from the PATH block.
    /// </summary>
    public AlienRuntimePath Path;
}