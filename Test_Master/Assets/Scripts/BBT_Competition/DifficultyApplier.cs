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

        if (bonusCubeSpawner != null)
        {
            bonusCubeSpawner.freezeCubePrefab =
                DifficultyManager.HasFreeze ? cachedFreezePrefab : null;

            bonusCubeSpawner.reactionCubePrefab =
                DifficultyManager.HasReaction ? cachedReactionPrefab : null;

            bonusCubeSpawner.pegChallengeManager =
                DifficultyManager.HasPeg ? cachedPegManager : null;

            bonusCubeSpawner.sequenceChallengeManager =
                DifficultyManager.HasSequence ? cachedSeqManager : null;
        }

        if (pegChallengeManager != null)
            pegChallengeManager.enabled = DifficultyManager.HasPeg;

        if (sequenceChallengeManager != null)
            sequenceChallengeManager.enabled = DifficultyManager.HasSequence;
    }
}