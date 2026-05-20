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
    [Tooltip("Roter Bonus-Würfel: +5 Punkte")]
    public GameObject bonusCubePrefab;

    [Tooltip("Grüner Malus-Würfel: -5 Punkte (wenn im eigenen Feld)")]
    public GameObject malusCubePrefab;

    [Tooltip("Blauer Freeze-Würfel: friert den Gegner für 5s ein")]
    public GameObject freezeCubePrefab;


    [Header("Spawn-Zonen")]
    public Transform leftZone;
    public Transform rightZone;

    [Header("Timing")]
    public float spawnIntervalMin = 20f;
    public float spawnIntervalMax = 30f;

    private bool isActive = false;
    private int spawnedBonus = 0;
    private int spawnedMalus = 0;

    // -----------------------------------------------------------------------
    // Unity Lifecycle – nur zum Testen, später von CompetitionGameManager steuern
    // -----------------------------------------------------------------------

    private void Start()
    {
        spawnIntervalMin = 5f; // TODO: auf 20f zurücksetzen
        spawnIntervalMax = 8f; // TODO: auf 30f zurücksetzen
        StartSpawning();
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void StartSpawning()
    {
        isActive = true;
        StartCoroutine(SpawnRoutine());
        if (freezeCubePrefab != null)
            StartCoroutine(FreezeSpawnRoutine());
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

        // Ausgeglichene Auswahl: wenn Differenz > 1, bevorzuge die seltenere Farbe
        bool isMalus;
        if (malusCubePrefab == null)
        {
            isMalus = false;
        }
        else if (spawnedBonus - spawnedMalus > 1)
        {
            isMalus = true;  // zu viele grüne ? roter kommt als nächstes
        }
        else if (spawnedMalus - spawnedBonus > 1)
        {
            isMalus = false; // zu viele rote ? grüner kommt als nächstes
        }
        else
        {
            isMalus = Random.value < 0.5f; // ausgeglichen ? zufällig
        }
        GameObject prefabToSpawn = isMalus ? malusCubePrefab : bonusCubePrefab;
        string label = isMalus ? "Malus (-5)" : "Bonus (+5)";

        bool spawnLeft = Random.value < 0.5f;
        Vector3 pos = GetRandomPositionInZone(spawnLeft ? leftZone : rightZone);
        if (pos == Vector3.zero) return;

        GameObject spawned = Instantiate(prefabToSpawn, pos, Quaternion.identity);
        if (isMalus) spawnedMalus++; else spawnedBonus++;

        // Sicherstellen dass der Würfel nicht durch den Tisch fällt
        Rigidbody rb = spawned.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.velocity = Vector3.zero;
        }
        Debug.Log($"[BonusCubeSpawner] {label} ? {(spawnLeft ? "Ghost" : "XBot")}-Seite");
    }



    private IEnumerator FreezeSpawnRoutine()
    {
        // Erster Spawn: zufällig zwischen 5-15s
        yield return new WaitForSeconds(Random.Range(8f, 20f));
        if (isActive) SpawnFreezeCube();

        // Zweiter Spawn: zufällig zwischen 30-40s nach Spielstart
        // Warte die verbleibende Zeit bis in das 30-40s Fenster
        float secondSpawnAt = Random.Range(30f, 40f);
        float waitRemaining = secondSpawnAt - Time.timeSinceLevelLoad;
        if (waitRemaining > 0)
            yield return new WaitForSeconds(waitRemaining);

        if (isActive) SpawnFreezeCube();
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

        Debug.Log($"[BonusCubeSpawner] Freeze-Würfel gespawnt auf {(spawnLeft ? "Ghost" : "XBot")}-Seite.");
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