using UnityEngine;

public class DifficultyApplier : MonoBehaviour
{
    public BonusCubeSpawner bonusCubeSpawner;
    public PegChallengeManager pegChallengeManager;
    public SequenceChallengeManager sequenceChallengeManager;
    private GameObject cachedFreezePrefab;
    private GameObject cachedReactionPrefab;
    private PegChallengeManager cachedPegManager;
    private SequenceChallengeManager cachedSeqManager;

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

        // Prefabs setzen
        bonusCubeSpawner.freezeCubePrefab = DifficultyManager.HasFreeze ? cachedFreezePrefab : null;
        bonusCubeSpawner.reactionCubePrefab = DifficultyManager.HasReaction ? cachedReactionPrefab : null;
        bonusCubeSpawner.pegChallengeManager = DifficultyManager.HasPeg ? cachedPegManager : null;
        bonusCubeSpawner.sequenceChallengeManager = DifficultyManager.HasSequence ? cachedSeqManager : null;

        // Scripts aktivieren/deaktivieren
        if (pegChallengeManager != null) pegChallengeManager.enabled = DifficultyManager.HasPeg;
        if (sequenceChallengeManager != null) sequenceChallengeManager.enabled = DifficultyManager.HasSequence;

        // Werte pro Level setzen
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
                // Fokus auf Peg – kein Reaction für klaren Dual-Task Fokus
                SetBonus(3, 4, 12, 18);
                SetFreeze(2, 3);
                SetReaction(0, 0, 0);
                SetPeg(true);
                SetSequence(false, 0);
                break;

            case DifficultyLevel.Memory:
                // Fokus auf Working Memory – kein Peg, kein Reaction
                SetBonus(3, 4, 12, 18);
                SetFreeze(2, 3);
                SetReaction(0, 0, 0);
                SetPeg(false);
                SetSequence(true, 2);
                break;

            case DifficultyLevel.Full:
                // Alles + Ghost schneller
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