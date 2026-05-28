using UnityEngine;
using TMPro;

public class Timer : MonoBehaviour
{
    public float testDuration = 60f;

    [Header("UI")]
    public TextMeshProUGUI timerText;

    [Header("End Screen")]
    [SerializeField] private GameOverUI_off gameOverUI;

    private TargetZoneCounter targetZoneCounter;

    private float timeRemaining;
    private bool isRunning = false;

    public bool IsRunning => isRunning;

    private void Start()
    {
        targetZoneCounter = FindAnyObjectByType<TargetZoneCounter>();

        if (gameOverUI == null)
            gameOverUI = FindObjectOfType<GameOverUI_off>();

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

    // Called by BoxBoundaryTrigger when the hand enters the box
    public void StartTimer()
    {
        if (isRunning) return; 
        timeRemaining = testDuration;
        isRunning = true;
        Debug.Log("[TestTimer] Timer started!");
        UpdateUI();
    }

    public void StartTest()
    {
        StartTimer(); 
    }

    private void EndTest()
    {
        Debug.Log("[TestTimer] Test ended!");

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
        // GloveGrabber deactivated
        GloveGrabber gloveGrabber = FindObjectOfType<GloveGrabber>();
        if (gloveGrabber != null)
            gloveGrabber.enabled = false;

        // Pause CSVReplayController
        CSVReplayController csv = FindObjectOfType<CSVReplayController>();
        if (csv != null)
            csv.enabled = false;

        // Old SimpleGrabber, if still active
        SimpleGrabber simpleGrabber = FindObjectOfType<SimpleGrabber>();
        if (simpleGrabber != null)
            simpleGrabber.enabled = false;
    }
}