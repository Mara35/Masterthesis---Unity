using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class GameOverUI_off : MonoBehaviour
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

    [Header("Scene Names")]
    [SerializeField] private string trainingSceneName = "BoxBlock_Training_offline";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void Start()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

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
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (hudScoreText != null) hudScoreText.SetActive(false);
        if (hudTimerText != null) hudTimerText.SetActive(false);

        if (finalScoreText != null)
            finalScoreText.text = "Score: " + finalScore;

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