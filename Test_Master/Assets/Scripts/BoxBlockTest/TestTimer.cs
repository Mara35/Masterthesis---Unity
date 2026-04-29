using UnityEngine;
using TMPro;

public class TestTimer : MonoBehaviour
{
    public float testDuration = 60f;

    [Header("UI")]
    public TextMeshProUGUI timerText;

    private HandProxyKeyboardControl hand;
    private StartZoneDetector startZone;

    private float timeRemaining;
    private bool isRunning = false;

    public bool IsRunning => isRunning;

    private void Start()
    {
        hand = FindObjectOfType<HandProxyKeyboardControl>();
        startZone = FindObjectOfType<StartZoneDetector>();

        timeRemaining = testDuration;
        UpdateUI();
    }

    private void Update()
    {
        if (!isRunning && hand != null && startZone != null && startZone.IsInStartZone && hand.HasMovedThisFrame)
        {
            StartTest();
        }

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

    public void StartTest()
    {
        timeRemaining = testDuration;
        isRunning = true;
        UpdateUI();
    }

    private void EndTest()
    {
        Debug.Log("Test finished");

        // Optional: Eingaben deaktivieren
        DisableInteraction();
    }

    private void UpdateUI()
    {
        if (timerText != null)
        {
            timerText.text = "Time: " + Mathf.CeilToInt(timeRemaining);
        }
    }

    private void DisableInteraction()
    {
        // Grabber deaktivieren
        SimpleGrabber grabber = FindObjectOfType<SimpleGrabber>();
        if (grabber != null)
        {
            grabber.enabled = false;
        }

        // Handsteuerung deaktivieren
        HandProxyKeyboardControl hand = FindObjectOfType<HandProxyKeyboardControl>();
        if (hand != null)
        {
            hand.enabled = false;
        }
    }
}