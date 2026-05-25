/*
 * Project:    SensinGlove – Box & Block Rehab Game
 * File:       SequenceChallengeManager.cs
 * Author:     Mari und Kiki (MCI – University of Applied Sciences)
 * Supervisor: Simon Winkler, BSc MSc
 * Year:       2025
 *
 * Attach to:  CompetitionGameManager
 *
 * Spawnt 3 Würfel mit Zahlen 1-2-3. Orb soll sie in richtiger Reihenfolge
 * übertragen. Überwacht Transfer-Reihenfolge und gibt Bonuspunkte.
 *
 * Bonuspunkte:
 *   1 richtig = +1, 2 richtig = +2, alle 3 = +4
 *   Falscher Würfel = -2
 *
 * Setup:
 *   - sequenceCubePrefab  ? SequenceCube Prefab (Tag: "Sequence")
 *   - leftSpawnZone       ? TargetZone (Ghost-Seite)
 *   - rightSpawnZone      ? StartZone  (Player-Seite)
 *   - firstChallengeMin/Max ? Wann erste Challenge startet
 *   - mistakeChance       ? 0=immer richtig, 1=immer falsch (zum Testen)
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SequenceChallengeManager : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Prefab")]
    public GameObject sequenceCubePrefab;

    [Header("Spawn-Zonen")]
    public Transform leftSpawnZone;
    public Transform rightSpawnZone;

    [Header("Timing")]
    public float firstChallengeMin = 30f;
    public float firstChallengeMax = 45f;

    [Header("Test-Einstellung")]
    [Tooltip("0 = Orb macht immer richtig, 1 = Orb macht immer falsch")]
    [Range(0f, 1f)]
    public float mistakeChance = 0.3f;

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private bool isActive = false;
    private float partitionX = 0f;

    private List<SequenceCube> spawnedCubes = new List<SequenceCube>();
    private int nextExpectedSequence = 1; // welche Zahl als nächstes kommen muss
    private int correctTransfers = 0;
    private int incorrectTransfers = 0;
    private bool challengeActive = false;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void StartChallengeSystem()
    {
        isActive = true;
        GameObject cp = GameObject.Find("CenterPartition");
        if (cp != null) partitionX = cp.transform.position.x;
        StartCoroutine(ChallengeRoutine());
    }

    public void StopChallengeSystem()
    {
        isActive = false;
        StopAllCoroutines();
        CleanUp();
    }

    // -----------------------------------------------------------------------
    // Challenge Routine
    // -----------------------------------------------------------------------

    private IEnumerator ChallengeRoutine()
    {
        yield return new WaitForSeconds(Random.Range(firstChallengeMin, firstChallengeMax));

        if (!isActive) yield break;

        // Warten bis keine anderen Challenges aktiv sind
        yield return new WaitUntil(() =>
            !OrbSharedState.playerFrozen &&
            !OrbSharedState.ghostFrozen &&
            !OrbSharedState.playerSideHasReaction &&
            !OrbSharedState.playerSideHasPeg
        );

        yield return StartCoroutine(RunChallenge());
    }

    private IEnumerator RunChallenge()
    {
        challengeActive = true;
        nextExpectedSequence = 1;
        correctTransfers = 0;
        incorrectTransfers = 0;

        Debug.Log("[SequenceChallenge] Challenge gestartet!");

        // Zufällig linke oder rechte Seite
        bool spawnLeft = Random.value < 0.5f;
        Transform spawnZone = spawnLeft ? leftSpawnZone : rightSpawnZone;

        SpawnSequenceCubes(spawnZone);

        // Orbs informieren
        if (spawnLeft)
        {
            GhostOrbController ghost = FindObjectOfType<GhostOrbController>();
            if (ghost != null) ghost.StartSequenceChallenge(spawnedCubes, mistakeChance);
        }
        else
        {
            PlayerOrbController player = FindObjectOfType<PlayerOrbController>();
            if (player != null) player.StartSequenceChallenge(spawnedCubes, mistakeChance);
        }

        // Warten bis alle 3 übertragen oder Challenge endet
        float timeout = 20f;
        float elapsed = 0f;
        while (challengeActive && elapsed < timeout && isActive)
        {
            elapsed += Time.deltaTime;
            CheckTransfers();
            yield return null;
        }

        // Challenge beenden
        if (spawnLeft)
        {
            GhostOrbController ghost = FindObjectOfType<GhostOrbController>();
            if (ghost != null) ghost.EndSequenceChallenge();
        }
        else
        {
            PlayerOrbController player = FindObjectOfType<PlayerOrbController>();
            if (player != null) player.EndSequenceChallenge();
        }

        AwardBonusPoints(spawnLeft);
        CleanUp();
        challengeActive = false;
    }

    // -----------------------------------------------------------------------
    // Spawn
    // -----------------------------------------------------------------------

    private void SpawnSequenceCubes(Transform zone)
    {
        if (sequenceCubePrefab == null || zone == null) return;

        Collider col = zone.GetComponent<Collider>();
        Bounds b = col != null ? col.bounds : new Bounds(zone.position, Vector3.one * 0.3f);

        float insetX = Mathf.Max(0.04f, b.size.x * 0.15f);
        float insetZ = Mathf.Max(0.04f, b.size.z * 0.15f);

        // Mische Reihenfolge der Positionen damit Zahlen zufällig verteilt sind
        List<int> order = new List<int> { 1, 2, 3 };
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
        }

        for (int i = 0; i < 3; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(b.min.x + insetX, b.max.x - insetX),
                b.max.y + 0.03f,
                Random.Range(b.min.z + insetZ, b.max.z - insetZ)
            );

            GameObject cubeGO = Instantiate(sequenceCubePrefab, pos, Quaternion.identity);
            SequenceCube sc = cubeGO.GetComponent<SequenceCube>();
            if (sc != null)
            {
                sc.sequenceNumber = order[i];
                spawnedCubes.Add(sc);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Transfer prüfen
    // -----------------------------------------------------------------------

    private void CheckTransfers()
    {
        foreach (SequenceCube sc in spawnedCubes)
        {
            if (sc == null || sc.IsTransferred) continue;

            // Hat der Würfel die Seite gewechselt?
            bool crossedPartition = sc.SpawnedOnGhostSide()
                ? sc.transform.position.x > partitionX
                : sc.transform.position.x < partitionX;

            if (!crossedPartition) continue;

            sc.IsTransferred = true;

            if (sc.sequenceNumber == nextExpectedSequence)
            {
                correctTransfers++;
                nextExpectedSequence++;
                Debug.Log($"[SequenceChallenge] Richtig! #{sc.sequenceNumber} übertragen. Correct={correctTransfers}");
            }
            else
            {
                incorrectTransfers++;
                Debug.Log($"[SequenceChallenge] Falsch! #{sc.sequenceNumber} erwartet #{nextExpectedSequence}");
            }

            if (correctTransfers + incorrectTransfers >= 3)
                challengeActive = false;
        }
    }

    // -----------------------------------------------------------------------
    // Bonuspunkte
    // -----------------------------------------------------------------------

    private void AwardBonusPoints(bool wasOnGhostSide)
    {
        int bonus = 0;
        if (correctTransfers == 1) bonus = 1;
        else if (correctTransfers == 2) bonus = 2;
        else if (correctTransfers == 3) bonus = 4;

        int penalty = incorrectTransfers * -2;
        int total = bonus + penalty;

        Debug.Log($"[SequenceChallenge] {correctTransfers}/3 richtig, {incorrectTransfers} falsch ? Bonus={total}");

        if (wasOnGhostSide)
        {
            CompetitionGameManager.ghostBonusPoints += total;
            Debug.Log($"[SequenceChallenge] BonusPoints Ghost = {CompetitionGameManager.ghostBonusPoints}");
        }
        else
        {
            CompetitionGameManager.playerBonusPoints += total;
            Debug.Log($"[SequenceChallenge] BonusPoints Player = {CompetitionGameManager.playerBonusPoints}");
        }
    }

    // -----------------------------------------------------------------------
    // Aufräumen
    // -----------------------------------------------------------------------

    private void CleanUp()
    {
        // Sequence Cubes bleiben liegen (werden normale Würfel)
        // Nur aus der internen Liste entfernen
        spawnedCubes.Clear();
    }
}