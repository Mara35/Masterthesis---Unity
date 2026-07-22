using UnityEngine;
using TMPro;


/// <summary>
/// Countdown timer for a training run. Started by BoxBoundaryTrigger when the hand enters the box,
/// counts down from <see cref="testDuration"/>, updates the PC and VR timer labels, and on expiry
/// reads the final score from ScoreCounter, shows the end screen and disables further interaction.
/// </summary>

public class Timer : MonoBehaviour
{
    public float testDuration = 60f;

    [Header("UI")]
    public TextMeshProUGUI timerText;

    [Header("VR UI (World Space)")]
    [Tooltip("Timer text on Canvas_VR")]
    public TextMeshProUGUI vrTimerText;

    [Header("End Screen")]
    [SerializeField] private GameOverUI gameOverUI;

    private ScoreCounter scoreCounter;

    private float timeRemaining;
    private bool isRunning = false;

    public bool IsRunning => isRunning;

    private void Start()
    {
        scoreCounter = FindAnyObjectByType<ScoreCounter>();

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

        int finalScore = (scoreCounter != null) ? scoreCounter.CurrentCount : 0;

        if (gameOverUI != null)
            gameOverUI.ShowEndScreen(finalScore);

        DisableInteraction();
    }

    private void UpdateUI()
    {
        string label = "Time: " + Mathf.CeilToInt(timeRemaining);
        if (timerText != null) timerText.text = label;
        if (vrTimerText != null) vrTimerText.text = label;
    }

    // On timeout: switch off whatever input paths might still move blocks, so the run is frozen.
    private void DisableInteraction()
    {
        GloveGrabber gloveGrabber = FindObjectOfType<GloveGrabber>();
        if (gloveGrabber != null)
            gloveGrabber.enabled = false;

        CSVReplayController csv = FindObjectOfType<CSVReplayController>();
        if (csv != null)
            csv.enabled = false;

        SimpleGrabber simpleGrabber = FindObjectOfType<SimpleGrabber>();
        if (simpleGrabber != null)
            simpleGrabber.enabled = false;
    }
}