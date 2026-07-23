using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns the timed extras during a competition round: bonus/penalty/freeze cubes and, if enabled,
/// reaction cubes, into the left/right zones at randomized intervals with per-round caps. Also owns
/// references to the peg and sequence challenge managers. Start/StopSpawning gate the whole system.
/// </summary>

public class BonusCubeSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject bonusCubePrefab;
    public GameObject malusCubePrefab;
    public GameObject freezeCubePrefab;

    [Header("Spawn-Zones")]
    public Transform leftZone;
    public Transform rightZone;

    [Header("Bonus/penalty Timing")]
    [Tooltip("First spawn after X seconds (after the game starts)")]
    public float firstSpawnMin = 8f;
    public float firstSpawnMax = 15f;
    [Tooltip("Interval between further spawns")]
    public float spawnIntervalMin = 10f;
    public float spawnIntervalMax = 15f;
    [Tooltip("Maximum number of bonus/penalty cubes in 60 seconds")]
    public int maxBonusCubesTotal = 5;

    [Header("Sequence Challenge")]
    public SequenceChallengeManager sequenceChallengeManager;

    [Header("Peg Challenge")]
    public PegChallengeManager pegChallengeManager;

    [Header("Reaction Cube")]
    public GameObject reactionCubePrefab;
    [Tooltip("Maximum number of ReactionCubes in 60 seconds")]
    public int maxReactionCubesTotal = 3;
    public float reactionSpawnMin = 20f;
    public float reactionSpawnMax = 35f;

    [Header("Freeze Timing")]
    [Tooltip("Maximum number of FreezeCubes in 60 seconds")]
    public int maxFreezeCubesTotal = 3;

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private bool isActive = false;
    private int spawnedBonus = 0;
    private int spawnedMalus = 0;
    private int totalBonusSpawned = 0;
    private int totalFreezeSpawned = 0;
    private int totalReactionSpawned = 0;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void StartSpawning()
    {
        isActive = true;
        totalBonusSpawned = 0;
        totalFreezeSpawned = 0;
        totalReactionSpawned = 0;
        spawnedBonus = 0;
        spawnedMalus = 0;
        StartCoroutine(SpawnRoutine());
        if (freezeCubePrefab != null)
            StartCoroutine(FreezeSpawnRoutine());
        if (reactionCubePrefab != null)
            StartCoroutine(ReactionSpawnRoutine());
        if (pegChallengeManager != null)
            pegChallengeManager.StartChallengeSystem();
        if (sequenceChallengeManager != null)
            sequenceChallengeManager.StartChallengeSystem();
    }

    public void StopSpawning()
    {
        isActive = false;
        StopAllCoroutines();
        if (pegChallengeManager != null)
            pegChallengeManager.StopChallengeSystem();
        if (sequenceChallengeManager != null)
            sequenceChallengeManager.StopChallengeSystem();
    }

    // -----------------------------------------------------------------------
    // Bonus/Penalty Spawn Routine
    // -----------------------------------------------------------------------

    private IEnumerator SpawnRoutine()
    {
        // First spawn after 8-15 seconds
        yield return new WaitForSeconds(Random.Range(firstSpawnMin, firstSpawnMax));

        while (isActive && totalBonusSpawned < maxBonusCubesTotal)
        {
            if (isActive) SpawnBonusCube();
            yield return new WaitForSeconds(Random.Range(spawnIntervalMin, spawnIntervalMax));
        }
    }

    private void SpawnBonusCube()
    {
        if (bonusCubePrefab == null) return;

        // Keep green (bonus) and red (malus) roughly balanced: force the under-represented type
        // once the difference exceeds 1, otherwise pick randomly.
        bool isMalus;
        if (malusCubePrefab == null)
            isMalus = false;
        else if (spawnedBonus - spawnedMalus > 1)
            isMalus = true;
        else if (spawnedMalus - spawnedBonus > 1)
            isMalus = false;
        else
            isMalus = Random.value < 0.5f;

        GameObject prefab = isMalus ? malusCubePrefab : bonusCubePrefab;
        bool spawnLeft = Random.value < 0.5f;
        Vector3 pos = GetRandomPositionInZone(spawnLeft ? leftZone : rightZone);
        if (pos == Vector3.zero) return;

        GameObject spawned = Instantiate(prefab, pos, Quaternion.identity);
        Rigidbody rb = spawned.GetComponent<Rigidbody>();
        if (rb != null) { rb.velocity = Vector3.zero; rb.isKinematic = false; rb.useGravity = true; }

        if (isMalus) spawnedMalus++; else spawnedBonus++;
        totalBonusSpawned++;

        string label = isMalus ? "Malus" : "Bonus";
        Debug.Log($"[BonusCubeSpawner] {label} spawned ({totalBonusSpawned}/{maxBonusCubesTotal})");
    }

    // -----------------------------------------------------------------------
    // Freeze Spawn Routine
    // -----------------------------------------------------------------------

    private IEnumerator FreezeSpawnRoutine()
    {
        // First Freeze: between 15-25s
        yield return new WaitForSeconds(Random.Range(15f, 25f));

        while (isActive && totalFreezeSpawned < maxFreezeCubesTotal)
        {
            // Only spawn a freeze cube when the field is clear: none present, nobody frozen, no peg running.
            yield return new WaitUntil(() =>
                !FreezeCubeExistsInScene() &&
                !OrbSharedState.playerFrozen &&
                !OrbSharedState.ghostFrozen &&
                !OrbSharedState.playerSideHasPeg
            );

            if (!isActive) yield break;

            SpawnFreezeCube();

            // Pause between freeze spawns
            yield return new WaitForSeconds(Random.Range(15f, 25f));
        }
    }

    private bool FreezeCubeExistsInScene()
    {
        try { return GameObject.FindGameObjectsWithTag("Freeze").Length > 0; }
        catch { return false; }
    }

    private void SpawnFreezeCube()
    {
        if (freezeCubePrefab == null) return;

        bool spawnLeft = Random.value < 0.5f;
        Vector3 pos = GetRandomPositionInZone(spawnLeft ? leftZone : rightZone);
        if (pos == Vector3.zero) return;

        GameObject spawned = Instantiate(freezeCubePrefab, pos, Quaternion.identity);
        Rigidbody rb = spawned.GetComponent<Rigidbody>();
        if (rb != null) { rb.velocity = Vector3.zero; rb.isKinematic = false; rb.useGravity = true; }

        totalFreezeSpawned++;
        Debug.Log($"[BonusCubeSpawner] FreezeCube spawned ({totalFreezeSpawned}/{maxFreezeCubesTotal})");
    }

    // -----------------------------------------------------------------------
    // Supporting methods
    // -----------------------------------------------------------------------

    private IEnumerator ReactionSpawnRoutine()
    {
        yield return new WaitForSeconds(Random.Range(reactionSpawnMin, reactionSpawnMax));

        while (isActive && totalReactionSpawned < maxReactionCubesTotal)
        {
            if (isActive) SpawnReactionCube();
            yield return new WaitForSeconds(Random.Range(15f, 25f));
        }
    }

    private void SpawnReactionCube()
    {
        if (reactionCubePrefab == null) return;

        // Side checks:
        // - Not on a frozen side (player cannot react)
        // - Not on a side that already has a Reaction or Peg
        bool canSpawnLeft = !OrbSharedState.ghostFrozen
                          && !OrbSharedState.ghostSideHasReaction;

        bool canSpawnRight = !OrbSharedState.playerFrozen
                          && !OrbSharedState.playerSideHasReaction
                          && !OrbSharedState.playerSideHasPeg; // Peg and Reaction not at the same time on one side

        if (!canSpawnLeft && !canSpawnRight)
        {
            Debug.Log("[BonusCubeSpawner] No free space for ReactionCube");
            return;
        }

        bool spawnLeft;
        if (!canSpawnLeft) spawnLeft = false;
        else if (!canSpawnRight) spawnLeft = true;
        else spawnLeft = Random.value < 0.5f;

        Vector3 pos = GetRandomPositionInZone(spawnLeft ? leftZone : rightZone);
        if (pos == Vector3.zero) return;

        Instantiate(reactionCubePrefab, pos, Quaternion.identity);
        totalReactionSpawned++;
        Debug.Log($"[BonusCubeSpawner] ReactionCube spawned ({totalReactionSpawned}/{maxReactionCubesTotal}) on {(spawnLeft ? "Ghost" : "Player")}-Side");
    }

    private Vector3 GetRandomPositionInZone(Transform zone)
    {
        if (zone == null) return Vector3.zero;
        Collider col = zone.GetComponent<Collider>();
        if (col == null) return Vector3.zero;

        Bounds b = col.bounds;
        float insetX = Mathf.Max(0.05f, b.size.x * 0.2f);
        float insetZ = Mathf.Max(0.05f, b.size.z * 0.2f);

        return new Vector3(
            Random.Range(b.min.x + insetX, b.max.x - insetX),
            b.max.y + 0.02f,
            Random.Range(b.min.z + insetZ, b.max.z - insetZ)
        );
    }
}