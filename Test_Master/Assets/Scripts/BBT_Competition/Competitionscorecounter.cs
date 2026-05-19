/*
 * Project:    SensinGlove – Box & Block Rehab Game
 * File:       CompetitionScoreCounter.cs
 * Author:     Mari und Kiki (MCI – University of Applied Sciences)
 * Supervisor: Simon Winkler, BSc MSc
 * Year:       2025
 *
 * Zählt Würfel per POSITION relativ zur CenterPartition.
 * Kein Collider-Overlap-Problem möglich.
 *
 * Setup:
 *   Zwei Instanzen dieses Scripts – eine für jeden Score:
 *
 *   GameObject (beliebig, z.B. CompetitionGameManager):
 *     CompetitionScoreCounter #1: side = RightSide  ? scoreText = ScoreText_Player
 *     CompetitionScoreCounter #2: side = LeftSide   ? scoreText = ScoreText_Ghost
 *
 *   ODER direkt auf StartZone / TargetZone legen (ohne BoxCollider-Abhängigkeit).
 */

using UnityEngine;
using TMPro;

public class CompetitionScoreCounter : MonoBehaviour
{
    public enum Side { RightSide, LeftSide }

    [Header("Welche Seite zählt dieses Script?")]
    [Tooltip("RightSide = Spieler (StartZone)\nLeftSide = Ghost (TargetZone)")]
    public Side countSide = Side.RightSide;

    [Header("UI")]
    public TextMeshProUGUI scoreText;
    public string prefix = "Score: ";

    [Header("Würfel")]
    [Tooltip("Tag der Würfel (z.B. 'Block'), oder leer für Name-Suche")]
    public string cubeTag = "Block";

    [Header("Update-Rate")]
    [Tooltip("Wie oft pro Sekunde gezählt wird (Performance)")]
    public float updateInterval = 0.1f;

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private float partitionX = 0f;
    private float nextUpdate = 0f;
    private int currentScore = 0;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        GameObject cp = GameObject.Find("CenterPartition");
        if (cp != null)
            partitionX = cp.transform.position.x;
        else
            Debug.LogWarning("[CompetitionScoreCounter] 'CenterPartition' nicht gefunden!");

        // Einen Frame warten bis alle GameObjects initialisiert sind
        StartCoroutine(InitialCount());
    }

    private System.Collections.IEnumerator InitialCount()
    {
        yield return null; // einen Frame warten
        CountAndUpdate();
    }

    private void Update()
    {
        if (Time.time < nextUpdate) return;
        nextUpdate = Time.time + updateInterval;
        CountAndUpdate();
    }

    // -----------------------------------------------------------------------
    // Zählen
    // -----------------------------------------------------------------------

    private void CountAndUpdate()
    {
        int count = 0;

        GameObject[] allCubes = GetAllCubes();
        foreach (GameObject cube in allCubes)
        {
            if (!cube.activeInHierarchy) continue;

            float x = cube.transform.position.x;

            if (countSide == Side.RightSide && x > partitionX) count++;
            if (countSide == Side.LeftSide && x < partitionX) count++;
        }

        if (count != currentScore)
        {
            currentScore = count;
            UpdateUI();
        }
    }

    private GameObject[] GetAllCubes()
    {
        try
        {
            if (!string.IsNullOrEmpty(cubeTag))
                return GameObject.FindGameObjectsWithTag(cubeTag);
        }
        catch { }

        // Fallback: alle GameObjects die mit "Block" beginnen
        var list = new System.Collections.Generic.List<GameObject>();
        foreach (GameObject go in FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("Block")) list.Add(go);
        return list.ToArray();
    }

    private void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = prefix + currentScore;
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public int GetScore() => currentScore;
}