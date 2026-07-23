using UnityEngine;

/// <summary>
/// Applies the chosen DifficultyLevel to the round: enables/tunes the spawner, peg and sequence
/// challenge managers so each level exposes the right set of cube types and challenges.
/// </summary>
public class DifficultyApplier : MonoBehaviour
{
    public BonusCubeSpawner bonusCubeSpawner;
    public PegChallengeManager pegChallengeManager;
    public SequenceChallengeManager sequenceChallengeManager;
    private GameObject cachedFreezePrefab;
    private GameObject cachedReactionPrefab;
    private PegChallengeManager cachedPegManager;
    private SequenceChallengeManager cachedSeqManager;

    // Cache the "full" set of prefabs/managers BEFORE any level nulls them out, so a level switch
    // can restore exactly the ones its difficulty allows (see ApplyDifficulty).
    private void Awake()
    {
        if (bonusCubeSpawner != null)
        {
            cachedFreezePrefab = bonusCubeSpawner.freezeCubePrefab;
            cachedReactionPrefab = bonusCubeSpawner.reactionCubePrefab;
            cachedPegManager = bonusCubeSpawner.pegChallengeManager;
            cachedSeqManager = bonusCubeSpawner.sequenceChallengeManager;
        }
        ApplyDifficulty();
    }

    private void ApplyDifficulty()
    {
        DifficultyLevel level = DifficultyManager.SelectedLevel;
        Debug.Log($"[DifficultyApplier] Level: {level}");

        if (bonusCubeSpawner == null) return;

        // Restore or null each prefab/manager depending on what this level unlocks (DifficultyManager).
        bonusCubeSpawner.freezeCubePrefab = DifficultyManager.HasFreeze ? cachedFreezePrefab : null;
        bonusCubeSpawner.reactionCubePrefab = DifficultyManager.HasReaction ? cachedReactionPrefab : null;
        bonusCubeSpawner.pegChallengeManager = DifficultyManager.HasPeg ? cachedPegManager : null;
        bonusCubeSpawner.sequenceChallengeManager = DifficultyManager.HasSequence ? cachedSeqManager : null;

        // Enable/disable scripts
        if (pegChallengeManager != null) pegChallengeManager.enabled = DifficultyManager.HasPeg;
        if (sequenceChallengeManager != null) sequenceChallengeManager.enabled = DifficultyManager.HasSequence;

        // Set values by level
        switch (level)
        {
            case DifficultyLevel.Basic:
                SetBonus(5, 6, 10, 15);
                SetFreeze(0, 0);
                SetReaction(0, 0, 0);
                SetPeg(false);
                SetSequence(false, 0);
                break;

            case DifficultyLevel.Motor:
                SetBonus(3, 4, 12, 18);
                SetFreeze(3, 3);
                SetReaction(0, 0, 0);
                SetPeg(false);
                SetSequence(false, 0);
                break;

            case DifficultyLevel.Reaction:
                SetBonus(3, 4, 12, 18);
                SetFreeze(3, 3);
                SetReaction(3, 20, 35);
                SetPeg(false);
                SetSequence(false, 0);
                break;

            case DifficultyLevel.Cognitive:
                // focus on Peg, no reaction for clear dual-task focus
                SetBonus(3, 4, 12, 18);
                SetFreeze(2, 3);
                SetReaction(0, 0, 0);
                SetPeg(true);
                SetSequence(false, 0);
                break;

            case DifficultyLevel.Memory:
                // Focus on working memory, no Peg, no Reaction
                SetBonus(3, 4, 12, 18);
                SetFreeze(2, 3);
                SetReaction(0, 0, 0);
                SetPeg(false);
                SetSequence(true, 2);
                break;

            case DifficultyLevel.Full:
                // Everything
                SetBonus(3, 4, 12, 18);
                SetFreeze(3, 3);
                SetReaction(2, 20, 35);
                SetPeg(true);
                SetSequence(true, 2);
                break;
        }
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    private void SetBonus(int min, int max, float intervalMin, float intervalMax)
    {
        bonusCubeSpawner.maxBonusCubesTotal = max;
        bonusCubeSpawner.spawnIntervalMin = intervalMin;
        bonusCubeSpawner.spawnIntervalMax = intervalMax;
    }

    private void SetFreeze(int max, int maxTotal)
    {
        bonusCubeSpawner.maxFreezeCubesTotal = maxTotal;
    }

    private void SetReaction(int max, float spawnMin, float spawnMax)
    {
        bonusCubeSpawner.maxReactionCubesTotal = max;
        if (max > 0)
        {
            bonusCubeSpawner.reactionSpawnMin = spawnMin;
            bonusCubeSpawner.reactionSpawnMax = spawnMax;
        }
    }

    private void SetPeg(bool active)
    {
        if (pegChallengeManager != null)
            pegChallengeManager.enabled = active && DifficultyManager.HasPeg;
        bonusCubeSpawner.pegChallengeManager = active ? cachedPegManager : null;
    }

    private void SetSequence(bool active, int maxTimes)
    {
        if (sequenceChallengeManager != null)
        {
            sequenceChallengeManager.enabled = active && DifficultyManager.HasSequence;
        }
        bonusCubeSpawner.sequenceChallengeManager = active ? cachedSeqManager : null;
    }

}