/*
 * Project:    SensinGlove – Box & Block Rehab Game
 * File:       SequenceChallengeManager.cs
 * Author:     Mari und Kiki (MCI – University of Applied Sciences)
 * Supervisor: Simon Winkler, BSc MSc
 * Year:       2025
 *
 * Regeln:
 *   - Alle 3 richtig ? +5 Bonuspunkte, Würfel verschwinden nach 1s
 *   - Erster falscher ? -2 Bonuspunkte, alle Würfel sofort weg
 *   - Feedback-Text im Spiel: "Gut gemacht!" oder "Falsche Reihenfolge!"
 *   - Orb soll alle 3 hintereinander nehmen ohne Unterbrechung
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SequenceChallengeManager : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject sequenceCubePrefab;

    [Header("Spawn-Zonen")]
    public Transform leftSpawnZone;
    public Transform rightSpawnZone;

    [Header("Timing")]
    public float firstChallengeMin = 30f;
    public float firstChallengeMax = 45f;

    [Header("Feedback Text (optional)")]
    [Tooltip("TextMeshProUGUI für Feedback-Anzeige im Canvas")]
    public TextMeshProUGUI feedbackText;

    [Header("Test")]
    [Range(0f, 1f)]
    public float mistakeChance = 0.3f;

    // -----------------------------------------------------------------------
    private bool isActive = false;
    private float partitionX = 0f;
    private List<SequenceCube> spawnedCubes = new List<SequenceCube>();
    private int nextExpectedSequence = 1;
    private int correctTransfers = 0;
    private bool challengeActive = false;
    private bool spawnedOnLeft = false;

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
        DestroyAllCubes();
    }

    // -----------------------------------------------------------------------
    private IEnumerator ChallengeRoutine()
    {
        yield return new WaitForSeconds(Random.Range(firstChallengeMin, firstChallengeMax));
        if (!isActive) yield break;

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
        spawnedOnLeft = Random.value < 0.5f;

        Debug.Log($"[SequenceChallenge] Challenge gestartet auf {(spawnedOnLeft ? "Ghost" : "Player")}-Seite!");

        Transform zone = spawnedOnLeft ? leftSpawnZone : rightSpawnZone;
        SpawnSequenceCubes(zone);

        // Orb informieren – soll NUR Sequence-Würfel nehmen bis fertig
        if (spawnedOnLeft)
        {
            GhostOrbController ghost = FindObjectOfType<GhostOrbController>();
            if (ghost != null) ghost.StartSequenceChallenge(spawnedCubes, mistakeChance);
        }
        else
        {
            PlayerOrbController player = FindObjectOfType<PlayerOrbController>();
            if (player != null) player.StartSequenceChallenge(spawnedCubes, mistakeChance);
        }

        // Überwachen bis fertig oder Timeout
        float timeout = 25f;
        float elapsed = 0f;
        while (challengeActive && elapsed < timeout && isActive)
        {
            elapsed += Time.deltaTime;
            CheckTransfers();
            yield return null;
        }

        // Orb freigeben
        if (spawnedOnLeft)
        {
            GhostOrbController ghost = FindObjectOfType<GhostOrbController>();
            if (ghost != null) ghost.EndSequenceChallenge();
        }
        else
        {
            PlayerOrbController player = FindObjectOfType<PlayerOrbController>();
            if (player != null) player.EndSequenceChallenge();
        }

        challengeActive = false;
    }

    // -----------------------------------------------------------------------
    private void SpawnSequenceCubes(Transform zone)
    {
        if (sequenceCubePrefab == null || zone == null) return;

        Collider col = zone.GetComponent<Collider>();
        Bounds b = col != null ? col.bounds : new Bounds(zone.position, Vector3.one * 0.3f);

        float insetX = Mathf.Max(0.04f, b.size.x * 0.15f);
        float insetZ = Mathf.Max(0.04f, b.size.z * 0.15f);

        // Zahlen 1-2-3 zufällig verteilt
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
                sc.Init(order[i]);
                spawnedCubes.Add(sc);
            }
        }
    }

    // -----------------------------------------------------------------------
    private void CheckTransfers()
    {
        foreach (SequenceCube sc in spawnedCubes)
        {
            if (sc == null || sc.IsTransferred) continue;

            bool crossed = sc.SpawnedOnGhostSide()
                ? sc.transform.position.x > partitionX
                : sc.transform.position.x < partitionX;

            if (!crossed) continue;

            sc.IsTransferred = true;

            if (sc.sequenceNumber == nextExpectedSequence)
            {
                correctTransfers++;
                nextExpectedSequence++;
                Debug.Log($"[SequenceChallenge] Richtig! #{sc.sequenceNumber}. Correct={correctTransfers}");

                if (correctTransfers == 3)
                {
                    // Alle richtig!
                    OnAllCorrect();
                    return;
                }
            }
            else
            {
                // Falsche Reihenfolge
                OnWrongOrder(sc.sequenceNumber);
                return;
            }
        }
    }

    private void OnAllCorrect()
    {
        int bonus = 5;
        if (spawnedOnLeft)
        {
            CompetitionGameManager.ghostBonusPoints += bonus;
            Debug.Log($"[SequenceChallenge] Alle richtig! BonusPoints Ghost = {CompetitionGameManager.ghostBonusPoints}");
        }
        else
        {
            CompetitionGameManager.playerBonusPoints += bonus;
            Debug.Log($"[SequenceChallenge] Alle richtig! BonusPoints Player = {CompetitionGameManager.playerBonusPoints}");
        }

        ShowFeedback("Gut gemacht! +5", Color.green);

        // Würfel nach 1s verschwinden lassen
        foreach (SequenceCube sc in spawnedCubes)
            if (sc != null) StartCoroutine(sc.LingerAndDestroy());

        spawnedCubes.Clear();
        challengeActive = false;
    }

    private void OnWrongOrder(int wrongNumber)
    {
        int penalty = -2;
        if (spawnedOnLeft)
        {
            CompetitionGameManager.ghostBonusPoints += penalty;
            Debug.Log($"[SequenceChallenge] Falsche Reihenfolge! #{wrongNumber}. BonusPoints Ghost = {CompetitionGameManager.ghostBonusPoints}");
        }
        else
        {
            CompetitionGameManager.playerBonusPoints += penalty;
            Debug.Log($"[SequenceChallenge] Falsche Reihenfolge! #{wrongNumber}. BonusPoints Player = {CompetitionGameManager.playerBonusPoints}");
        }

        ShowFeedback("Falsche Reihenfolge! -2", Color.red);

        // Alle Würfel sofort zerstören
        DestroyAllCubes();
        challengeActive = false;
    }

    private void ShowFeedback(string message, Color color)
    {
        if (feedbackText == null) return;
        feedbackText.text = message;
        feedbackText.color = color;
        feedbackText.gameObject.SetActive(true);
        StartCoroutine(HideFeedbackAfterDelay(2f));
    }

    private IEnumerator HideFeedbackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }

    private void DestroyAllCubes()
    {
        foreach (SequenceCube sc in spawnedCubes)
            if (sc != null) Destroy(sc.gameObject);
        spawnedCubes.Clear();
    }
}