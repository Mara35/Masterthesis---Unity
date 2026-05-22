/*
 * Project:    SensinGlove – Box & Block Rehab Game
 * File:       CompetitionGameManager.cs
 * Author:     Mari und Kiki (MCI – University of Applied Sciences)
 * Supervisor: Simon Winkler, BSc MSc
 * Year:       2025
 *
 * Attach to:  CompetitionGameManager GameObject
 *
 * Spielablauf:
 *   1. Warten bis PlayerOrb die Box berührt (BoxBoundaryTrigger)
 *   2. Countdown 60s startet, GhostOrb beginnt zu spielen
 *   3. Nach 60s: Result-Screen mit Scores, Bonus-Abzug, Win/Lose
 *
 * Setup im Inspector:
 *   - timerText          ? TimerText (TMP)
 *   - gameOverPanel      ? GameOverPanel
 *   - playerScoreCounter ? CompetitionScoreCounter auf Player-Seite
 *   - ghostScoreCounter  ? CompetitionScoreCounter auf Ghost-Seite
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
    public PlayerOrbController playerOrb;
    public BonusCubeSpawner bonusCubeSpawner;

    [Header("Start Trigger")]
    [Tooltip("BoxBoundaryTrigger – Spiel startet wenn PlayerOrb diesen berührt")]
    public Collider startTrigger;

    [Header("Result Screen")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI playerScoreText;
    public TextMeshProUGUI ghostScoreText;
    public TextMeshProUGUI playerBonusText;
    public TextMeshProUGUI ghostBonusText;
    public TextMeshProUGUI resultText;        // "You Win!" / "You Lose!"
    public Button playAgainButton;
    public Button mainMenuButton;
    public Button settingsButton;    // für spätere Schwierigkeitseinstellung

    [Header("Szenen")]
    public string mainMenuSceneName = "MainMenu";
    public string settingsSceneName = "Settings";

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private bool gameStarted = false;
    private bool gameFinished = false;

    // Bonus-Punkte die am Ende abgezogen werden
    // (werden später von Reaktions-Würfel / Farb-Matching befüllt)
    public static int playerBonusPoints = 0;
    public static int ghostBonusPoints = 0;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        // Buttons verdrahten
        if (playAgainButton != null)
            playAgainButton.onClick.AddListener(OnPlayAgain);
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenu);
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettings);

        // Panel verstecken
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        // Orbs noch nicht starten
        if (ghostOrb != null) ghostOrb.StopPlaying(); // Ghost wartet auf Trigger

    }

    // -----------------------------------------------------------------------
    // Spielstart (durch BoxBoundaryTrigger ausgelöst)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Wird vom BoxBoundaryTrigger aufgerufen wenn PlayerOrb die Box berührt.
    /// </summary>
    public void StartGame()
    {
        if (gameStarted) return;
        gameStarted = true;

        Debug.Log("[CompetitionGameManager] Spiel gestartet!");

        // Orbs aktivieren
        if (ghostOrb != null) ghostOrb.StartPlaying();
        else Debug.LogWarning("[CompetitionGameManager] ghostOrb ist nicht zugewiesen!");
        // PlayerOrb startet bereits in Start() automatisch

        // Spawner starten
        if (bonusCubeSpawner != null) bonusCubeSpawner.StartSpawning();

        // Bonus-Punkte zurücksetzen
        playerBonusPoints = 0;
        ghostBonusPoints = 0;
    }

    // -----------------------------------------------------------------------
    // Spielende
    // -----------------------------------------------------------------------

    public void EndGame()
    {
        if (gameFinished) return;
        gameFinished = true;

        Debug.Log("[CompetitionGameManager] Spiel beendet!");

        // Orbs stoppen
        if (ghostOrb != null) ghostOrb.StopPlaying(); // Ghost wartet auf Trigger

        // Spawner stoppen
        if (bonusCubeSpawner != null) bonusCubeSpawner.StopSpawning();

        // Kurze Pause dann Result-Screen
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
        StartCoroutine(RevealSequence());
    }

    private IEnumerator RevealSequence()
    {
        // TODO: Testweise Bonuspunkte – entfernen wenn echte Bonus-Würfel implementiert
        playerBonusPoints = 3;
        ghostBonusPoints = 5;

        int playerRaw = playerScoreCounter != null ? playerScoreCounter.GetScore() : 0;
        int ghostRaw = ghostScoreCounter != null ? ghostScoreCounter.GetScore() : 0;
        int playerFinal = Mathf.Max(0, playerRaw - playerBonusPoints);
        int ghostFinal = Mathf.Max(0, ghostRaw - ghostBonusPoints);

        // Alles verstecken
        if (resultText != null) resultText.text = "";
        if (playerBonusText != null) playerBonusText.text = "";
        if (ghostBonusText != null) ghostBonusText.text = "";

        // Schritt 1: Rohe Scores anzeigen
        if (playerScoreText != null) playerScoreText.text = playerRaw.ToString();
        if (ghostScoreText != null) ghostScoreText.text = ghostRaw.ToString();
        yield return new WaitForSecondsRealtime(1.5f);

        // Schritt 2: Bonus-Punkte einblenden
        if (playerBonusText != null)
            playerBonusText.text = $"Du: -{playerBonusPoints} Bonus";
        if (ghostBonusText != null)
            ghostBonusText.text = $"Ghost: -{ghostBonusPoints} Bonus";
        yield return new WaitForSecondsRealtime(1.5f);

        // Schritt 3: Score-Countdown von Raw zu Final
        yield return StartCoroutine(CountdownScore(playerScoreText, playerRaw, playerFinal));
        yield return StartCoroutine(CountdownScore(ghostScoreText, ghostRaw, ghostFinal));
        yield return new WaitForSecondsRealtime(0.5f);

        // Schritt 4: Win/Lose anzeigen
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

    // Zählt einen Score-Text schrittweise von start zu end
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