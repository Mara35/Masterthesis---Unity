/*
 * Summary
 * 
 * Attach to:  CompetitionGameManager GameObject (or your own GameObject)
 *
 * Similar structure to Timer.cs in BBT_Training_offline.
 * Triggered by BoxStartTrigger when Hand touches the box.
 * Calls CompetitionGameManager.EndGame() at the end.
 */

using UnityEngine;
using TMPro;
using System.Collections;

public class CompetitionTimer : MonoBehaviour
{
    [Header("Duration")]
    public float gameDuration = 60f;

    [Header("UI")]
    public TextMeshProUGUI timerText;

    [Header("Reference")]
    public CompetitionGameManager gameManager;

    [Header("Shake at 10s")]
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

        // Shake at exactly 10 seconds—just once
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
        Debug.Log("[CompetitionTimer] Timer started!");
        UpdateUI();
    }

    private void EndGame()
    {
        Debug.Log("[CompetitionTimer] Time!s up!");
        if (gameManager != null)
            gameManager.EndGame();
    }

    private void UpdateUI()
    {
        if (timerText == null) return;

        int seconds = Mathf.CeilToInt(timeRemaining);
        timerText.text = "Time: " + seconds;

        // Last 10 seconds: red
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

        // Back to the original position
        timerText.rectTransform.anchoredPosition = timerOriginalPos;
    }
}