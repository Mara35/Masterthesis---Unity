/*
 * Project:    SensinGlove – Box & Block Rehab Game
 * File:       PegChallengeManager.cs
 * Author:     Mari und Kiki (MCI – University of Applied Sciences)
 * Supervisor: Simon Winkler, BSc MSc
 * Year:       2025
 *
 * Attach to:  CompetitionGameManager (oder eigenes GO)
 *
 * Nine Hole Peg Test inspirierte Challenge:
 *   - 3 Zylinder spawnen auf der Spieler-Seite (in der Box)
 *   - 3 Zielzonen spawnen vor der Box auf dem Tisch
 *   - PlayerOrb bringt Zylinder in Zonen innerhalb von 8s
 *   - 1 richtig = +2, 2 richtig = +3, alle 3 = +5 Bonuspunkte
 *
 * Setup im Inspector:
 *   - pegPrefab        ? Zylinder Prefab mit PegChallengeCube.cs
 *   - zonePrefab       ? Zone Prefab mit PegChallengeZone.cs
 *   - playerSpawnZone  ? StartZone (Spieler-Seite)
 *   - tableCenter      ? Transform vor der Box auf dem Tisch
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PegChallengeManager : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Prefabs")]
    [Tooltip("Zylinder Prefab (Nine Hole Peg Größe, Tag: Peg)")]
    public GameObject pegPrefab;

    [Tooltip("Zielzone Prefab (kleines Loch/Marker)")]
    public GameObject zonePrefab;

    [Header("Spawn-Positionen")]
    [Tooltip("StartZone des Spielers – hier spawnen die Zylinder")]
    public Transform playerSpawnZone;

    [Tooltip("Transform vor der Box – hier spawnen die Zielzonen")]
    public Transform tableInFrontOfBox;

    [Header("Timing")]
    public float challengeDuration = 8f;
    public float firstChallengeMin = 25f;
    public float firstChallengeMax = 35f;

    [Header("Abstände")]
    [Tooltip("Abstand zwischen den 3 Zonen")]
    public float zoneSpacing = 0.08f;

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private List<GameObject> spawnedPegs = new List<GameObject>();
    private List<PegChallengeZone> spawnedZones = new List<PegChallengeZone>();

    private bool isActive = false;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void StartChallengeSystem()
    {
        isActive = true;
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

        // Warten bis:
        // 1. Player nicht gefreezt
        // 2. Ghost nicht gefreezt (kein FreezeCube aktiv der Player einfrieren könnte)
        // 3. Kein FreezeCube in der Scene der noch landen könnte
        // 4. Keine andere Reaction Challenge aktiv
        yield return new WaitUntil(() =>
            !OrbSharedState.playerFrozen &&
            !OrbSharedState.ghostFrozen &&
            !OrbSharedState.playerSideHasReaction &&
            !FreezeCubeExistsInScene()
        );

        // Zusätzliche Sicherheitspause nach FreezeCube
        yield return new WaitForSeconds(1.0f);

        yield return StartCoroutine(RunChallenge());
    }

    private IEnumerator RunChallenge()
    {
        Debug.Log("[PegChallengeManager] Challenge gestartet!");

        // Pegs und Zonen spawnen
        OrbSharedState.playerSideHasPeg = true;
        SpawnPegs();
        SpawnZones();

        // PlayerOrbController informieren
        PlayerOrbController player = FindObjectOfType<PlayerOrbController>();
        if (player != null) player.StartPegChallenge(spawnedPegs, GetZonePositions());

        // 8s Countdown
        float remaining = challengeDuration;
        while (remaining > 0f && isActive)
        {
            remaining -= Time.deltaTime;
            yield return null;
        }

        // Auswerten
        int placed = CountPlacedPegs();
        AwardBonusPoints(placed);

        Debug.Log($"[PegChallengeManager] Challenge beendet! {placed}/3 Pegs platziert.");

        // PlayerOrb zurück zu normaler Logik
        if (player != null) player.EndPegChallenge();
        OrbSharedState.playerSideHasPeg = false;

        // Aufräumen
        CleanUp();
    }

    // -----------------------------------------------------------------------
    // Spawn
    // -----------------------------------------------------------------------

    private void SpawnPegs()
    {
        if (pegPrefab == null || playerSpawnZone == null) return;

        Collider col = playerSpawnZone.GetComponent<Collider>();
        Bounds b = col != null ? col.bounds : new Bounds(playerSpawnZone.position, Vector3.one * 0.3f);

        float insetX = b.size.x * 0.2f;
        float insetZ = b.size.z * 0.2f;

        for (int i = 0; i < 3; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(b.min.x + insetX, b.max.x - insetX),
                b.max.y + 0.04f,
                Random.Range(b.min.z + insetZ, b.max.z - insetZ)
            );

            GameObject peg = Instantiate(pegPrefab, pos, Quaternion.identity);

            // Farb-ID zuweisen (0=Rot, 1=Blau, 2=Gelb)
            PegChallengeCube pegComp = peg.GetComponent<PegChallengeCube>();
            if (pegComp != null) pegComp.colorId = i;

            spawnedPegs.Add(peg);
        }
    }

    private void SpawnZones()
    {
        if (zonePrefab == null || tableInFrontOfBox == null) return;

        // Zonen in zufälliger Reihenfolge nebeneinander vor der Box
        int[] colorOrder = { 0, 1, 2 };
        // Fisher-Yates shuffle
        for (int i = colorOrder.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = colorOrder[i]; colorOrder[i] = colorOrder[j]; colorOrder[j] = tmp;
        }

        for (int i = 0; i < 3; i++)
        {
            float offset = (i - 1) * zoneSpacing;
            Vector3 pos = tableInFrontOfBox.position + tableInFrontOfBox.right * offset;

            GameObject zoneGO = Instantiate(zonePrefab, pos, Quaternion.identity);
            PegChallengeZone zone = zoneGO.GetComponent<PegChallengeZone>();
            if (zone != null)
            {
                zone.colorId = colorOrder[i]; // zufällige Anordnung der Farben
                spawnedZones.Add(zone);
            }
        }
    }

    private List<Vector3> GetZonePositions()
    {
        List<Vector3> positions = new List<Vector3>();
        foreach (PegChallengeZone z in spawnedZones)
            if (z != null) positions.Add(z.transform.position);
        return positions;
    }

    // -----------------------------------------------------------------------
    // Auswertung
    // -----------------------------------------------------------------------

    private bool FreezeCubeExistsInScene()
    {
        try { return GameObject.FindGameObjectsWithTag("Freeze").Length > 0; }
        catch { return false; }
    }

    private int CountPlacedPegs()
    {
        int count = 0;
        foreach (GameObject peg in spawnedPegs)
        {
            if (peg == null) continue;
            PegChallengeCube pegComp = peg.GetComponent<PegChallengeCube>();
            if (pegComp == null) continue;

            // Prüfe ob Peg nahe einer Zone mit passender Farbe liegt
            foreach (PegChallengeZone zone in spawnedZones)
            {
                if (zone == null) continue;
                if (zone.colorId != pegComp.colorId) continue;

                float dist = Vector3.Distance(peg.transform.position, zone.transform.position);
                if (dist < 0.1f) // innerhalb 10cm = platziert
                {
                    count++;
                    Debug.Log($"[PegChallengeManager] Peg {pegComp.colorId} korrekt in Zone {zone.colorId} platziert.");
                    break;
                }
            }
        }
        return count;
    }

    private void AwardBonusPoints(int placed)
    {
        int bonus = 0;
        if (placed == 1) bonus = 2;
        else if (placed == 2) bonus = 3;
        else if (placed >= 3) bonus = 5;

        Debug.Log($"[PegChallengeManager] {placed}/3 Pegs ? {bonus} Bonuspunkte. PlayerBonusPoints vorher: {CompetitionGameManager.playerBonusPoints}");

        if (bonus > 0)
        {
            CompetitionGameManager.playerBonusPoints += bonus;
            Debug.Log($"[PegChallengeManager] Peg erfolgreich! BonusPoints Player = {CompetitionGameManager.playerBonusPoints}");
        }
    }

    // -----------------------------------------------------------------------
    // Aufräumen
    // -----------------------------------------------------------------------

    private void CleanUp()
    {
        foreach (GameObject peg in spawnedPegs)
            if (peg != null) Destroy(peg);
        spawnedPegs.Clear();

        foreach (PegChallengeZone zone in spawnedZones)
            if (zone != null) Destroy(zone.gameObject);
        spawnedZones.Clear();
    }
}