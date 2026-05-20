/*
 * Project:    SensinGlove – Box & Block Rehab Game
 * File:       BonusCubeSpawner.cs
 * Author:     Mari und Kiki (MCI – University of Applied Sciences)
 * Supervisor: Simon Winkler, BSc MSc
 * Year:       2025
 *
 * Attach to:  CompetitionGameManager
 *
 * Spawnt alle 20-30 Sekunden einen Bonus-Würfel zufällig auf einer der
 * beiden Seiten. Maximal ein Bonus-Würfel pro Seite gleichzeitig.
 *
 * Setup im Inspector:
 *   - bonusCubePrefab ? BonusCube Prefab
 *   - leftZone        ? TargetZone  (Ghost-Seite, linke Box-Hälfte)
 *   - rightZone       ? StartZone   (XBot-Seite, rechte Box-Hälfte)
 */

using System.Collections;
using UnityEngine;

public class BonusCubeSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Grüner Bonus-Würfel: -5 Punkte  (wenn im eigenen Feld)")]
    public GameObject bonusCubePrefab;

    [Tooltip("Roter Malus-Würfel: +5 Punkte")]
    public GameObject malusCubePrefab;

    [Header("Spawn-Zonen")]
    public Transform leftZone;
    public Transform rightZone;

    [Header("Timing")]
    public float spawnIntervalMin = 20f;
    public float spawnIntervalMax = 30f;

    private bool isActive = false;

    // -----------------------------------------------------------------------
    // Unity Lifecycle – nur zum Testen, später von CompetitionGameManager steuern
    // -----------------------------------------------------------------------

    private void Start()
    {
        spawnIntervalMin = 12f; 
        spawnIntervalMax = 20f; 
        StartSpawning();
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void StartSpawning()
    {
        isActive = true;
        StartCoroutine(SpawnRoutine());
    }

    public void StopSpawning()
    {
        isActive = false;
        StopAllCoroutines();
    }

    // -----------------------------------------------------------------------
    // Spawn Routine
    // -----------------------------------------------------------------------

    private IEnumerator SpawnRoutine()
    {
        while (isActive)
        {
            yield return new WaitForSeconds(Random.Range(spawnIntervalMin, spawnIntervalMax));
            if (isActive) SpawnBonusCube();
        }
    }

    private void SpawnBonusCube()
    {
        if (bonusCubePrefab == null)
        {
            Debug.LogWarning("[BonusCubeSpawner] Kein Prefab zugewiesen!");
            return;
        }

        // Zufällig Bonus oder Malus wählen
        bool isMalus = (malusCubePrefab != null) && (Random.value < 0.5f);
        GameObject prefabToSpawn = isMalus ? malusCubePrefab : bonusCubePrefab;
        string label = isMalus ? "Malus (-5)" : "Bonus (+5)";

        bool spawnLeft = Random.value < 0.5f;
        Vector3 pos = GetRandomPositionInZone(spawnLeft ? leftZone : rightZone);
        if (pos == Vector3.zero) return;

        Instantiate(prefabToSpawn, pos, Quaternion.identity);
        Debug.Log($"[BonusCubeSpawner] {label} ? {(spawnLeft ? "Ghost" : "XBot")}-Seite");
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