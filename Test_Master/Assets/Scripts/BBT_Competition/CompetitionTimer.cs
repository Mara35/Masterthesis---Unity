/*
 * Project:    SensinGlove – Box & Block Rehab Game
 * File:       CompetitionTimer.cs
 * Author:     Mari und Kiki (MCI – University of Applied Sciences)
 * Supervisor: Simon Winkler, BSc MSc
 * Year:       2025
 *
 * Attach to:  CompetitionGameManager GameObject (oder eigenes GO)
 *
 * Ähnlicher Aufbau wie Timer.cs in BBT_Training_offline.
 * Wird von BoxStartTrigger gestartet wenn PlayerOrb die Box berührt.
 * Ruft am Ende CompetitionGameManager.EndGame() auf.
 */

using UnityEngine;
using TMPro;

public class CompetitionTimer : MonoBehaviour
{
    [Header("Dauer")]
    public float gameDuration = 60f;

    [Header("UI")]
    public TextMeshProUGUI timerText;

    [Header("Referenz")]
    public CompetitionGameManager gameManager;

    private float timeRemaining;
    private bool isRunning = false;

    public bool IsRunning => isRunning;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<CompetitionGameManager>();

        timeRemaining = gameDuration;
        UpdateUI();
    }

    private void Update()
    {
        if (!isRunning) return;

        timeRemaining -= Time.deltaTime;

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            isRunning = false;
            UpdateUI();
            EndGame();
        }

        UpdateUI();
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Wird von BoxStartTrigger aufgerufen wenn PlayerOrb die Box berührt.
    /// </summary>
    public void StartTimer()
    {
        if (isRunning) return;
        timeRemaining = gameDuration;
        isRunning = true;
        Debug.Log("[CompetitionTimer] Timer gestartet!");
        UpdateUI();
    }

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private void EndGame()
    {
        Debug.Log("[CompetitionTimer] Zeit abgelaufen!");

        if (gameManager != null)
            gameManager.EndGame();
    }

    private void UpdateUI()
    {
        if (timerText == null) return;

        int seconds = Mathf.CeilToInt(timeRemaining);
        timerText.text = "Time: " + seconds;

        // Letzte 10s: Text rot
        timerText.color = seconds <= 10 ? Color.red : Color.white;
    }
}