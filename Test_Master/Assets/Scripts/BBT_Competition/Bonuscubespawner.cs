/*
 * Project:    SensinGlove – Box & Block Rehab Game
 * File:       BonusCubeSpawner.cs
 * Author:     Mari und Kiki (MCI – University of Applied Sciences)
 * Supervisor: Simon Winkler, BSc MSc
 * Year:       2025
 *
 * Attach to:  CompetitionGameManager
 *
 * Limits:
 *   - Max 5 BonusCubes (grün/rot) in 60s
 *   - Max 2-3 FreezeCubes in 60s, max 1 gleichzeitig im Feld
 *   - Erster Spawn nach 8-15s nach Spielstart
 */

using System.Collections;
using UnityEngine;

public class BonusCubeSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject bonusCubePrefab;
    public GameObject malusCubePrefab;
    public GameObject freezeCubePrefab;

    [Header("Spawn-Zonen")]
    public Transform leftZone;
    public Transform rightZone;

    [Header("Bonus/Malus Timing")]
    [Tooltip("Erster Spawn nach X Sekunden (nach Spielstart)")]
    public float firstSpawnMin = 8f;
    public float firstSpawnMax = 15f;
    [Tooltip("Intervall zwischen weiteren Spawns")]
    public float spawnIntervalMin = 10f;
    public float spawnIntervalMax = 15f;
    [Tooltip("Maximale Anzahl Bonus/Malus Cubes in 60s")]
    public int maxBonusCubesTotal = 5;

    [Header("Reaction Cube")]
    public GameObject reactionCubePrefab;
    [Tooltip("Maximale Anzahl ReactionCubes in 60s")]
    public int maxReactionCubesTotal = 3;
    public float reactionSpawnMin = 20f;
    public float reactionSpawnMax = 35f;

    [Header("Freeze Timing")]
    [Tooltip("Maximale Anzahl FreezeCubes in 60s")]
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
    }

    public void StopSpawning()
    {
        isActive = false;
        StopAllCoroutines();
    }

    // -----------------------------------------------------------------------
    // Bonus/Malus Spawn Routine
    // -----------------------------------------------------------------------

    private IEnumerator SpawnRoutine()
    {
        // Erster Spawn nach 8-15s
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
        Debug.Log($"[BonusCubeSpawner] {label} gespawnt ({totalBonusSpawned}/{maxBonusCubesTotal})");
    }

    // -----------------------------------------------------------------------
    // Freeze Spawn Routine
    // -----------------------------------------------------------------------

    private IEnumerator FreezeSpawnRoutine()
    {
        // Erster Freeze: zwischen 15-25s
        yield return new WaitForSeconds(Random.Range(15f, 25f));

        while (isActive && totalFreezeSpawned < maxFreezeCubesTotal)
        {
            // Warten bis kein FreezeCube mehr im Feld ist
            yield return new WaitUntil(() => !FreezeCubeExistsInScene());

            if (!isActive) yield break;

            SpawnFreezeCube();

            // Pause zwischen Freeze-Spawns
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
        Debug.Log($"[BonusCubeSpawner] FreezeCube gespawnt ({totalFreezeSpawned}/{maxFreezeCubesTotal})");
    }

    // -----------------------------------------------------------------------
    // Hilfsmethoden
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

        // Seite wählen – nicht auf gefreezte Seite spawnen
        bool leftFrozen = OrbSharedState.ghostFrozen;   // Ghost ist links
        bool rightFrozen = OrbSharedState.playerFrozen;  // Player ist rechts

        bool canSpawnLeft = !leftFrozen;
        bool canSpawnRight = !rightFrozen;

        if (!canSpawnLeft && !canSpawnRight)
        {
            Debug.Log("[BonusCubeSpawner] Beide Seiten gefreezt – ReactionCube übersprungen.");
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
        Debug.Log($"[BonusCubeSpawner] ReactionCube gespawnt ({totalReactionSpawned}/{maxReactionCubesTotal}) auf {(spawnLeft ? "Ghost" : "Player")}-Seite");
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