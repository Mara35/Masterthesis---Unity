using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Counts a competition round down from gameDuration, updates the PC and VR timer labels, shakes the
/// timer near the end, and tells the CompetitionGameManager to end the game at zero.
/// </summary>
/// 
public class CompetitionTimer : MonoBehaviour
{
    [Header("Duration")]
    public float gameDuration = 60f;

    [Header("UI")]
    public TextMeshProUGUI timerText;

    [Header("VR UI (World Space)")]
    [Tooltip("Timer text on Canvas_VR")]
    public TextMeshProUGUI vrTimerText;

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

        // Shake at exactly 10 seconds, just once
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
        Debug.Log("[CompetitionTimer] Time's up!");
        if (gameManager != null)
            gameManager.EndGame();
    }

    private void UpdateUI()
    {
        int seconds = Mathf.CeilToInt(timeRemaining);
        string label = "Time: " + seconds;
        Color c = seconds <= 10 ? Color.red : Color.white;

        if (timerText != null)
        {
            timerText.text = label;
            // Last 10 seconds: red
            timerText.color = c;
            timerText.faceColor = c;
        }

        if (vrTimerText != null)
        {
            vrTimerText.text = label;
            vrTimerText.color = c;
            vrTimerText.faceColor = c;
        }
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