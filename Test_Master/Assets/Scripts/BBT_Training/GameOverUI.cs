using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// End-of-run screen for BBT training. Shows a final-score panel on both the PC canvas and the
/// world-space VR canvas, hides the live HUD, pauses the game (Time.timeScale = 0) and unlocks the
/// cursor. The buttons restart the training scene or return to the main menu, restoring timescale.
/// </summary>

public class GameOverUI : MonoBehaviour
{
    [Header("Panel (disabled by default!)")]
    [SerializeField] private GameObject gameOverPanel;

    [Header("UI Texts")]
    [SerializeField] private TextMeshProUGUI finalScoreText;

    [Header("Hide HUD on the end screen")]
    [SerializeField] private GameObject hudScoreText;
    [SerializeField] private GameObject hudTimerText;

    [Header("Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button mainMenuButton;

    [Header("VR (World Space - display only)")]
    [Tooltip("Copy of gameOverPanel on Canvas_VR (buttons disabled)")]
    [SerializeField] private GameObject vrGameOverPanel;
    [Tooltip("Final score text on Canvas_VR")]
    [SerializeField] private TextMeshProUGUI vrFinalScoreText;
    [Tooltip("HUD score text on Canvas_VR (hidden on end screen)")]
    [SerializeField] private GameObject vrHudScoreText;
    [Tooltip("HUD timer text on Canvas_VR (hidden on end screen)")]
    [SerializeField] private GameObject vrHudTimerText;

    [Header("Scene Names")]
    [SerializeField] private string trainingSceneName = "BoxBlock_Training_offline";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void Start()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        if (vrGameOverPanel != null)
            vrGameOverPanel.SetActive(false);

        if (newGameButton != null) newGameButton.onClick.AddListener(LoadNewGame);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(LoadMainMenu);
    }

    private void OnDestroy()
    {
        if (newGameButton != null) newGameButton.onClick.RemoveListener(LoadNewGame);
        if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(LoadMainMenu);
    }

    public void ShowEndScreen(int finalScore)
    {
        // Show panel (PC + VR)
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (vrGameOverPanel != null) vrGameOverPanel.SetActive(true);

        // Hide HUD (PC + VR)
        if (hudScoreText != null) hudScoreText.SetActive(false);
        if (hudTimerText != null) hudTimerText.SetActive(false);
        if (vrHudScoreText != null) vrHudScoreText.SetActive(false);
        if (vrHudTimerText != null) vrHudTimerText.SetActive(false);

        // Score text (PC + VR)
        string scoreLabel = "Score: " + finalScore;
        if (finalScoreText != null) finalScoreText.text = scoreLabel;
        if (vrFinalScoreText != null) vrFinalScoreText.text = scoreLabel;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;

        Debug.Log($"[GameOverUI] End-Screen. Score={finalScore}");
    }

    private void LoadNewGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(trainingSceneName);
    }

    private void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}