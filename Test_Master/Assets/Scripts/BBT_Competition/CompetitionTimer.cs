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
using System.Collections;

public class CompetitionTimer : MonoBehaviour
{
    [Header("Dauer")]
    public float gameDuration = 60f;

    [Header("UI")]
    public TextMeshProUGUI timerText;

    [Header("Referenz")]
    public CompetitionGameManager gameManager;

    [Header("Shake bei 10s")]
    public float shakeDuration = 0.5f;
    public float shakeMagnitude = 8f;

    private float timeRemaining;
    private bool isRunning = false;
    private bool shakeTriggered = false;
    private Vector3 timerOriginalPos;

    public bool IsRunning => isRunning;

    private void Start()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<CompetitionGameManager>();

        timeRemaining = gameDuration;
        if (timerText != null)
            timerOriginalPos = timerText.rectTransform.anchoredPosition;
        UpdateUI();
    }

    private void Update()
    {
        if (!isRunning) return;

        timeRemaining -= Time.deltaTime;

        // Schütteln bei genau 10s – nur einmal
        if (!shakeTriggered && timeRemaining <= 10f)
        {
            shakeTriggered = true;
            StartCoroutine(ShakeTimer());
        }

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            isRunning = false;
            UpdateUI();
            EndGame();
        }

        UpdateUI();
    }

    public void StartTimer()
    {
        if (isRunning) return;
        timeRemaining = gameDuration;
        isRunning = true;
        shakeTriggered = false;
        Debug.Log("[CompetitionTimer] Timer gestartet!");
        UpdateUI();
    }

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

        // Letzte 10s: rot
        timerText.color = seconds <= 10 ? Color.red : Color.white;
        timerText.faceColor = seconds <= 10 ? Color.red : Color.white;
    }

    private IEnumerator ShakeTimer()
    {
        if (timerText == null) yield break;

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            float offsetX = Random.Range(-shakeMagnitude, shakeMagnitude);
            float offsetY = Random.Range(-shakeMagnitude, shakeMagnitude);
            timerText.rectTransform.anchoredPosition = timerOriginalPos + new Vector3(offsetX, offsetY, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Zurück zur Originalposition
        timerText.rectTransform.anchoredPosition = timerOriginalPos;
    }
}