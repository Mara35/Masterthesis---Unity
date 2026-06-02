/* 
 * Summary
 * 
 * Attach to:  CompetitionGameManager GameObject
 *
 * Gameplay:
 *   1. Wait until the hand touches the box (BoxBoundaryTrigger)
 *   2. 60-second countdown starts, GhostOrb begins playing
 *   3. After 60 seconds: Result screen with scores, bonus deduction, Win/Lose
 *
 * Setup in the Inspector:
 *   - timerText          ? TimerText (TMP)
 *   - gameOverPanel      ? GameOverPanel
 *   - playerScoreCounter ? CompetitionScoreCounter on the player side
 *   - ghostScoreCounter  ? CompetitionScoreCounter on the ghost side
 *   - ghostOrb           ? GhostOrbController
 *   - bonusCubeSpawner   ? BonusCubeSpawner
 *   - startTrigger       ? BoxBoundaryTrigger Collider
 */

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class CompetitionGameManager : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Score")]
    public CompetitionScoreCounter playerScoreCounter;
    public CompetitionScoreCounter ghostScoreCounter;

    [Header("Orbs & Spawner")]
    public GhostOrbController ghostOrb;
    // public PlayerOrbController playerOrb; // PlayerOrb replaced by Hand + GloveGrabber
    public BonusCubeSpawner bonusCubeSpawner;

    [Header("Start Trigger")]
    [Tooltip("BoxBoundaryTrigger – Game starts when Hand touches it")]
    public Collider startTrigger;

    [Header("Live Score (will be hidden when the game ends)")]
    public GameObject liveScoreGhost;
    public GameObject liveScorePlayer;
    public TextMeshProUGUI timerText;

    [Header("Result Screen")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI playerScoreText;
    public TextMeshProUGUI ghostScoreText;
    public TextMeshProUGUI playerBonusText;
    public TextMeshProUGUI ghostBonusText;
    public TextMeshProUGUI resultText;        // "You Win!" / "You Lose!"
    public Button playAgainButton;
    public Button mainMenuButton;
    public Button settingsButton;    

    [Header("Scene")]
    public string mainMenuSceneName = "MainMenu";
    public string settingsSceneName = "BBT_Explanation_Competition";

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private bool gameStarted = false;
    private bool gameFinished = false;
    private int frozenPlayerScore = 0;
    private int frozenGhostScore = 0;

    // Bonus points that are deducted at the end
    public static int playerBonusPoints = 0;
    public static int ghostBonusPoints = 0;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        
        if (playAgainButton != null)
            playAgainButton.onClick.AddListener(OnPlayAgain);
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenu);
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettings);

        // Hide Panel
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        
        if (ghostOrb != null) ghostOrb.StopPlaying(); // GhostOrb waiting for Trigger

    }

    // -----------------------------------------------------------------------
    // Game Start (triggered by BoxBoundaryTrigger)
    // -----------------------------------------------------------------------

    public void StartGame()
    {
        if (gameStarted) return;
        gameStarted = true;

        Debug.Log("[CompetitionGameManager] The game has started!");

        // Activate Orbs
        if (ghostOrb != null) ghostOrb.StartPlaying();
        else Debug.LogWarning("[CompetitionGameManager] ghostOrb not assigned!");
        // PlayerOrb startet bereits in Start() automatisch // Replaced by Hand + GloveGrabber

        // Start Spawners
        if (bonusCubeSpawner != null) bonusCubeSpawner.StartSpawning();

        // Reset Bonus Points
        playerBonusPoints = 0;
        ghostBonusPoints = 0;
    }

    // -----------------------------------------------------------------------
    // Game End
    // -----------------------------------------------------------------------

    public void EndGame()
    {
        if (gameFinished) return;
        gameFinished = true;

        Debug.Log("[CompetitionGameManager] Game Over!");

        // Stop everything immediately
        if (ghostOrb != null) ghostOrb.StopPlaying();
        //if (playerOrb != null) playerOrb.StopPlaying(); // Replaced by Hand + GloveGrabber
        GloveGrabber gloveGrabber = FindObjectOfType<GloveGrabber>();
        if (gloveGrabber != null) gloveGrabber.enabled = false;
        if (bonusCubeSpawner != null) bonusCubeSpawner.StopSpawning();

        // Freeze the score immediately – the GhostOrb will no longer move
        frozenPlayerScore = playerScoreCounter != null ? playerScoreCounter.GetScore() : 0;
        frozenGhostScore = ghostScoreCounter != null ? ghostScoreCounter.GetScore() : 0;

        Debug.Log($"[CompetitionGameManager] Scores frozen: Player={frozenPlayerScore}, Ghost={frozenGhostScore}");

        // Short pause, then the results screen
        StartCoroutine(ShowResultAfterDelay(1.0f));
    }

    private IEnumerator ShowResultAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowResult();
    }

    private void ShowResult()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        // Hide Live-Score and Timer 
        if (liveScoreGhost != null) liveScoreGhost.SetActive(false);
        if (liveScorePlayer != null) liveScorePlayer.SetActive(false);
        if (timerText != null) timerText.gameObject.SetActive(false);

        StartCoroutine(RevealSequence());
    }

    private IEnumerator RevealSequence()
    {
        int playerRaw = frozenPlayerScore;
        int ghostRaw = frozenGhostScore;
        int playerFinal = Mathf.Max(0, playerRaw - playerBonusPoints);
        int ghostFinal = Mathf.Max(0, ghostRaw - ghostBonusPoints);

        // Hide everything
        if (resultText != null) resultText.text = "";
        if (playerBonusText != null) playerBonusText.text = "";
        if (ghostBonusText != null) ghostBonusText.text = "";

        // Step 1: View raw scores
        if (playerScoreText != null) playerScoreText.text = playerRaw.ToString();
        if (ghostScoreText != null) ghostScoreText.text = ghostRaw.ToString();
        yield return new WaitForSecondsRealtime(1.5f);

        // Step 2: Show Bonus Points (deductions)
        // +X = Success (score decreases), -X = Failure (score increases)
        if (playerBonusText != null)
        {
            string pSign = playerBonusPoints > 0 ? "+" : "";
            playerBonusText.text = $"You: {pSign}{playerBonusPoints} Bonus";
        }
        if (ghostBonusText != null)
        {
            string gSign = ghostBonusPoints > 0 ? "+" : "";
            ghostBonusText.text = $"Ghost: {gSign}{ghostBonusPoints} Bonus";
        }
        yield return new WaitForSecondsRealtime(1.5f);

        // Step 3: Score Countdown from Raw to Final
        yield return StartCoroutine(CountdownScore(playerScoreText, playerRaw, playerFinal));
        yield return StartCoroutine(CountdownScore(ghostScoreText, ghostRaw, ghostFinal));
        yield return new WaitForSecondsRealtime(0.5f);

        // Step 4: Show Result Text
        if (resultText != null)
        {
            if (playerFinal < ghostFinal)
                resultText.text = "You Win!";
            else if (playerFinal > ghostFinal)
                resultText.text = "You Lose!";
            else
                resultText.text = "Draw!";
        }

        Time.timeScale = 0f;
    }

    
    private IEnumerator CountdownScore(TextMeshProUGUI text, int start, int end)
    {
        if (text == null) yield break;
        int steps = Mathf.Abs(start - end);
        int direction = start > end ? -1 : 1;
        for (int i = 0; i <= steps; i++)
        {
            text.text = (start + i * direction).ToString();
            yield return new WaitForSecondsRealtime(0.08f);
        }
    }

    // -----------------------------------------------------------------------
    // Timer UI
    // -----------------------------------------------------------------------

    // -----------------------------------------------------------------------
    // Button Callbacks
    // -----------------------------------------------------------------------

    private void OnPlayAgain()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void OnSettings()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(settingsSceneName);
    }

    private void OnDestroy()
    {
        if (playAgainButton != null) playAgainButton.onClick.RemoveListener(OnPlayAgain);
        if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(OnMainMenu);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettings);
    }
}