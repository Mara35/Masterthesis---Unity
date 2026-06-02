using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PegChallengeManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject pegPrefab;
    public GameObject zonePrefab;

    [Header("Spawn-Positions")]
    public Transform playerSpawnZone;
    public Transform tableInFrontOfBox;

    [Header("Timing")]
    public float challengeDuration = 8f;
    public float firstChallengeMin = 25f;
    public float firstChallengeMax = 35f;

    [Header("Distance")]
    public float zoneSpacing = 0.08f;

    private List<GameObject> spawnedPegs = new List<GameObject>();
    private List<PegChallengeZone> spawnedZones = new List<PegChallengeZone>();
    private bool isActive = false;

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

    private IEnumerator ChallengeRoutine()
    {
        yield return new WaitForSeconds(Random.Range(firstChallengeMin, firstChallengeMax));
        if (!isActive) yield break;

        yield return new WaitUntil(() =>
            !OrbSharedState.playerFrozen &&
            !OrbSharedState.ghostFrozen &&
            !OrbSharedState.playerSideHasReaction &&
            !FreezeCubeExistsInScene()
        );

        yield return new WaitForSeconds(1.0f);
        yield return StartCoroutine(RunChallenge());
    }

    private IEnumerator RunChallenge()
    {
        Debug.Log("[PegChallengeManager] Challenge started!");

        OrbSharedState.playerSideHasPeg = true;
        SpawnPegs();
        SpawnZones();

        // Mensch spielt selbst - keine PlayerOrb Steuerung nötig

        float remaining = challengeDuration;
        while (remaining > 0f && isActive)
        {
            remaining -= Time.deltaTime;
            yield return null;
        }

        int placed = CountPlacedPegs();
        AwardBonusPoints(placed);

        Debug.Log($"[PegChallengeManager] Challenge ended! {placed}/3 Pegs placed.");

        OrbSharedState.playerSideHasPeg = false;
        CleanUp();
    }

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

            PegChallengeCube pegComp = peg.GetComponent<PegChallengeCube>();
            if (pegComp != null) pegComp.colorId = i;

            spawnedPegs.Add(peg);
        }
    }

    private void SpawnZones()
    {
        if (zonePrefab == null || tableInFrontOfBox == null) return;

        int[] colorOrder = { 0, 1, 2 };
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
                zone.colorId = colorOrder[i];
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

            foreach (PegChallengeZone zone in spawnedZones)
            {
                if (zone == null) continue;
                if (zone.colorId != pegComp.colorId) continue;

                float dist = Vector3.Distance(peg.transform.position, zone.transform.position);
                if (dist < 0.1f)
                {
                    count++;
                    Debug.Log($"[PegChallengeManager] Peg {pegComp.colorId} properly placed.");
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

        if (bonus > 0)
        {
            CompetitionGameManager.playerBonusPoints += bonus;
            Debug.Log($"[PegChallengeManager] {placed}/3 Pegs - {bonus} Bonuspoints. Total={CompetitionGameManager.playerBonusPoints}");
        }
    }

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