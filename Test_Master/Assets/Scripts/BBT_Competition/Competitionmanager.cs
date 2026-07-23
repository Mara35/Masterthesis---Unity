using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Orchestrates a competition round: starts on the box trigger, runs the ghost orb and spawner, tracks
/// both scores, and on timeout shows the result screen (win/lose plus bonus totals) on the PC and VR
/// canvases with play-again / menu / settings buttons.
/// </summary>

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
    [Tooltip("BoxBoundaryTrigger, Game starts when Hand touches it")]
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

    [Header("VR UI (World Space, display only)")]
    [Tooltip("Live score/timer copies on Canvas_VR (hidden when game ends)")]
    public GameObject vrLiveScoreGhost;
    public GameObject vrLiveScorePlayer;
    public TextMeshProUGUI vrTimerText;
    [Tooltip("Result screen copies on Canvas_VR (buttons disabled)")]
    public GameObject vrGameOverPanel;
    public TextMeshProUGUI vrPlayerScoreText;
    public TextMeshProUGUI vrGhostScoreText;
    public TextMeshProUGUI vrPlayerBonusText;
    public TextMeshProUGUI vrGhostBonusText;
    public TextMeshProUGUI vrResultText;

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

    // Bonus points accumulated during the round (by ReactionCube etc.), applied as a
    // deduction at the end. Static so the cubes can add to them from anywhere in the scene.
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

        // Hide Panel (PC + VR)
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        if (vrGameOverPanel != null)
            vrGameOverPanel.SetActive(false);


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
        // PlayerOrb already starts automatically in Start() // Replaced by Hand + GloveGrabber

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

        // Freeze the score immediately - the GhostOrb will no longer move
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
        if (vrGameOverPanel != null) vrGameOverPanel.SetActive(true);

        // Hide Live-Score and Timer (PC + VR)
        if (liveScoreGhost != null) liveScoreGhost.SetActive(false);
        if (liveScorePlayer != null) liveScorePlayer.SetActive(false);
        if (timerText != null) timerText.gameObject.SetActive(false);
        if (vrLiveScoreGhost != null) vrLiveScoreGhost.SetActive(false);
        if (vrLiveScorePlayer != null) vrLiveScorePlayer.SetActive(false);
        if (vrTimerText != null) vrTimerText.gameObject.SetActive(false);

        StartCoroutine(RevealSequence());
    }

    private IEnumerator RevealSequence()
    {
        int playerRaw = frozenPlayerScore;
        int ghostRaw = frozenGhostScore;
        int playerFinal = playerRaw - playerBonusPoints;
        int ghostFinal = ghostRaw - ghostBonusPoints;
        // NOTE: the raw score counts cubes still on your OWN side, so a LOWER score is better
        // (you have transferred more away). A successful bonus (positive) lowers the final further;
        // a failure (negative) raises it. Lowest final wins, see the comparison in Step 4.

        // Hide everything (PC + VR)
        if (resultText != null) resultText.text = "";
        if (playerBonusText != null) playerBonusText.text = "";
        if (ghostBonusText != null) ghostBonusText.text = "";
        if (vrResultText != null) vrResultText.text = "";
        if (vrPlayerBonusText != null) vrPlayerBonusText.text = "";
        if (vrGhostBonusText != null) vrGhostBonusText.text = "";

        // Step 1: View raw scores (PC + VR)
        if (playerScoreText != null) playerScoreText.text = playerRaw.ToString();
        if (ghostScoreText != null) ghostScoreText.text = ghostRaw.ToString();
        if (vrPlayerScoreText != null) vrPlayerScoreText.text = playerRaw.ToString();
        if (vrGhostScoreText != null) vrGhostScoreText.text = ghostRaw.ToString();
        yield return new WaitForSecondsRealtime(1.5f);

        // Step 2: Show Bonus Points (deductions)
        // +X = Success (score decreases), -X = Failure (score increases)
        string pSign = playerBonusPoints > 0 ? "+" : "";
        string playerBonusLabel = $"You: {pSign}{playerBonusPoints} Bonus";
        if (playerBonusText != null) playerBonusText.text = playerBonusLabel;
        if (vrPlayerBonusText != null) vrPlayerBonusText.text = playerBonusLabel;

        string gSign = ghostBonusPoints > 0 ? "+" : "";
        string ghostBonusLabel = $"Ghost: {gSign}{ghostBonusPoints} Bonus";
        if (ghostBonusText != null) ghostBonusText.text = ghostBonusLabel;
        if (vrGhostBonusText != null) vrGhostBonusText.text = ghostBonusLabel;
        yield return new WaitForSecondsRealtime(1.5f);

        // Step 3: Score Countdown from Raw to Final (PC + VR in parallel)
        yield return StartCoroutine(CountdownScore(playerScoreText, vrPlayerScoreText, playerRaw, playerFinal));
        yield return StartCoroutine(CountdownScore(ghostScoreText, vrGhostScoreText, ghostRaw, ghostFinal));
        yield return new WaitForSecondsRealtime(0.5f);

        // Step 4: lower final wins (fewer cubes left on own side = more transferred)
        string result;
        if (playerFinal < ghostFinal)
            result = "You Win!";
        else if (playerFinal > ghostFinal)
            result = "You Lose!";
        else
            result = "Draw!";

        if (resultText != null) resultText.text = result;
        if (vrResultText != null) vrResultText.text = result;

        Time.timeScale = 0f; // freeze the scene on the result screen; buttons reset it to 1
    }


    private IEnumerator CountdownScore(TextMeshProUGUI text, TextMeshProUGUI vrText, int start, int end)
    {
        int steps = Mathf.Abs(start - end);
        int direction = start > end ? -1 : 1;
        for (int i = 0; i <= steps; i++)
        {
            string val = (start + i * direction).ToString();
            if (text != null) text.text = val;
            if (vrText != null) vrText.text = val;
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