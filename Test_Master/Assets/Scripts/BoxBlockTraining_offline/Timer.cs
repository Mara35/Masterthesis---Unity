using UnityEngine;
using TMPro;

public class Timer : MonoBehaviour
{
    public float testDuration = 60f;

    [Header("UI")]
    public TextMeshProUGUI timerText;

    [Header("End Screen")]
    [SerializeField] private GameOverUI gameOverUI;

    private TargetZoneCounter targetZoneCounter;

    private float timeRemaining;
    private bool isRunning = false;

    public bool IsRunning => isRunning;

    private void Start()
    {
        targetZoneCounter = FindAnyObjectByType<TargetZoneCounter>();

        if (gameOverUI == null)
            gameOverUI = FindObjectOfType<GameOverUI>();

        timeRemaining = testDuration;
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
            EndTest();
        }

        UpdateUI();
    }

    // Wird von BoxBoundaryTrigger aufgerufen wenn Hand die Box betritt
    public void StartTimer()
    {
        if (isRunning) return; // Nicht doppelt starten
        timeRemaining = testDuration;
        isRunning = true;
        Debug.Log("[TestTimer] Timer gestartet!");
        UpdateUI();
    }

    public void StartTest()
    {
        StartTimer(); // Rückwärtskompatibilität
    }

    private void EndTest()
    {
        Debug.Log("[TestTimer] Test beendet!");

        int finalScore = (targetZoneCounter != null) ? targetZoneCounter.CurrentCount : 0;

        if (gameOverUI != null)
            gameOverUI.ShowEndScreen(finalScore);

        DisableInteraction();
    }

    private void UpdateUI()
    {
        if (timerText != null)
            timerText.text = "Time: " + Mathf.CeilToInt(timeRemaining);
    }

    private void DisableInteraction()
    {
        // GloveGrabber deaktivieren
        GloveGrabber gloveGrabber = FindObjectOfType<GloveGrabber>();
        if (gloveGrabber != null)
            gloveGrabber.enabled = false;

        // CSVReplayController pausieren
        CSVReplayController csv = FindObjectOfType<CSVReplayController>();
        if (csv != null)
            csv.enabled = false;

        // Alten SimpleGrabber falls noch aktiv
        SimpleGrabber simpleGrabber = FindObjectOfType<SimpleGrabber>();
        if (simpleGrabber != null)
            simpleGrabber.enabled = false;
    }
}