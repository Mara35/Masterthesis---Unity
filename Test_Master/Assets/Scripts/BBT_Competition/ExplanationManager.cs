/*
 * Project:    SensinGlove – Box & Block Rehab Game
 * File:       ExplanationManager.cs
 * Author:     Mari und Kiki (MCI – University of Applied Sciences)
 * Supervisor: Simon Winkler, BSc MSc
 * Year:       2025
 *
 * Attach to:  ExplanationManager GameObject in BBT_Explanation_Competition
 *
 * Setup im Inspector:
 *   - pageSelect     ? Page_Select GameObject
 *   - pageDetail     ? Page_Detail GameObject
 *   - detailTitle    ? DetailTitle TMP
 *   - detailSubtitle ? DetailSubtitle TMP
 *   - cubeDesc       ? CubeDesc TMP
 *   - cubeVisual     ? CubeVisual Image
 *   - startButton    ? StartButton
 *   - backButton     ? BackButton
 *   - Cube colors in Inspector for each level
 */

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class ExplanationManager : MonoBehaviour
{
    [Header("Pages")]
    public GameObject pageSelect;
    public GameObject pageDetail;

    [Header("Detail Page UI")]
    public TextMeshProUGUI detailTitle;
    public TextMeshProUGUI detailSubtitle;
    public TextMeshProUGUI cubeDesc;
    public TextMeshProUGUI newLabel;

    [Header("Cube Preview Panels (one per level, assign in order)")]
    [Tooltip("6 Panels: Basic, Motor, Reaction, Cognitive, Sequential, Full")]
    public GameObject[] previewPanels;

    [Header("Buttons")]
    public Button backButton;
    public Button startButton;
    public Button mainMenuButton;

    [Header("Scenes")]
    public string competitionSceneName = "BBT_Competition";
    public string mainMenuSceneName = "MainMenu";

    [Header("Cube Colors")]
    public Color colorGreen = new Color(0.30f, 0.69f, 0.31f);
    public Color colorRed = new Color(0.96f, 0.26f, 0.21f);
    public Color colorBlue = new Color(0.16f, 0.71f, 0.96f);
    public Color colorWhite = Color.white;
    public Color colorOrange = new Color(1f, 0.60f, 0f);
    public Color colorPurple = new Color(0.67f, 0.28f, 0.73f);

    // -----------------------------------------------------------------------
    // Level Data
    // -----------------------------------------------------------------------

    private struct LevelData
    {
        public string title;
        public string subtitle;
        public string newBlockLabel;
        public string description;
        public Color cubeColor;
        public DifficultyLevel difficulty;
    }

    private LevelData[] levels;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        levels = new LevelData[]
        {
            new LevelData {
                title         = "Basic",
                subtitle      = "Learn the core mechanic — transfer as many blocks as possible in 60 seconds.",
                newBlockLabel = "Bonus & Malus Blocks",
                description   = "Green blocks give you bonus points — your final score drops. Red blocks give malus points — your score rises. Bonus points are subtracted from your final score at the end.",
                cubeColor     = colorGreen,
                difficulty    = DifficultyLevel.Basic
            },
            new LevelData {
                title         = "Motor",
                subtitle      = "Everything from Basic, plus a freeze mechanic to disrupt your opponent.",
                newBlockLabel = "Freeze Block",
                description   = "A blue block appears in the field. Pick it up and place it in the opponent's Freeze Zone to freeze them for 5 seconds. They cannot move any blocks during that time. Trains motor inhibition and strategic thinking.",
                cubeColor     = colorBlue,
                difficulty    = DifficultyLevel.Motor
            },
            new LevelData {
                title         = "Reaction",
                subtitle      = "Everything from Motor, plus a timed reaction challenge.",
                newBlockLabel = "Reaction Block",
                description   = "A blinking white block appears for only 3 seconds. Pick it up in time for +2 bonus points. Miss it and receive ?2 points. Trains reaction time and sustained attention — both commonly affected after stroke.",
                cubeColor     = colorWhite,
                difficulty    = DifficultyLevel.Reaction
            },
            new LevelData {
                title         = "Cognitive",
                subtitle      = "Focus on the Peg Challenge — a colour-matching dual task.",
                newBlockLabel = "Peg Challenge",
                description   = "3 coloured cylinders spawn in your zone with 3 matching target zones outside the box. Place each cylinder in the correct colour zone within 8 seconds. Combines fine motor control with colour recognition — a classic dual task for neurorehabilitation.",
                cubeColor     = colorRed,
                difficulty    = DifficultyLevel.Cognitive
            },
            new LevelData {
                title         = "Sequential",
                subtitle      = "Focus on the Sequence Challenge — a working memory task.",
                newBlockLabel = "Sequence Blocks",
                description   = "3 purple blocks appear with numbers 1, 2 and 3. Remember the order — the numbers disappear after 3 seconds. Transfer them in the correct sequence for +5 bonus points. A wrong order means ?2 points. Trains working memory combined with motor execution.",
                cubeColor     = colorPurple,
                difficulty    = DifficultyLevel.Sequential
            },
            new LevelData {
                title         = "Full Challenge",
                subtitle      = "All block types active — maximum motor and cognitive challenge.",
                newBlockLabel = "Everything Combined",
                description   = "All block types are active at once: Bonus/Malus, Freeze, Reaction, Peg Challenge and Sequence Blocks. This level combines multiple cognitive functions simultaneously — attention, working memory, colour recognition and motor control — for the highest level of dual-task rehabilitation training.",
                cubeColor     = colorPurple,
                difficulty    = DifficultyLevel.Full
            }
        };

        if (backButton != null) backButton.onClick.AddListener(OnBack);
        if (startButton != null) startButton.onClick.AddListener(OnStart);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenu);

        ShowSelectPage();
    }

    // -----------------------------------------------------------------------
    // Level Button Callbacks (assign in Inspector per Button)
    // -----------------------------------------------------------------------

    public void OnLevelSelected(int index)
    {
        if (index < 0 || index >= levels.Length) return;

        LevelData l = levels[index];

        if (detailTitle != null) detailTitle.text = l.title;
        if (detailSubtitle != null) detailSubtitle.text = l.subtitle;
        if (cubeDesc != null) cubeDesc.text = l.description;
        if (newLabel != null) newLabel.text = l.newBlockLabel.ToUpper();
        

        // Preview Panel tauschen – nur das passende Panel anzeigen
        if (previewPanels != null)
        {
            for (int i = 0; i < previewPanels.Length; i++)
                if (previewPanels[i] != null)
                    previewPanels[i].SetActive(i == index);
        }

        // Ausgewähltes Level speichern
        DifficultyManager.SelectedLevel = l.difficulty;

        ShowDetailPage();
    }

    // -----------------------------------------------------------------------
    // Navigation
    // -----------------------------------------------------------------------

    private void ShowSelectPage()
    {
        if (pageSelect != null) pageSelect.SetActive(true);
        if (pageDetail != null) pageDetail.SetActive(false);
    }

    private void ShowDetailPage()
    {
        if (pageSelect != null) pageSelect.SetActive(false);
        if (pageDetail != null) pageDetail.SetActive(true);
    }

    private void OnBack() => ShowSelectPage();
    private void OnMainMenu() => SceneManager.LoadScene(mainMenuSceneName);
    private void OnStart()
    {
        SceneManager.LoadScene(competitionSceneName);
    }

    private void OnDestroy()
    {
        if (backButton != null) backButton.onClick.RemoveListener(OnBack);
        if (startButton != null) startButton.onClick.RemoveListener(OnStart);
        if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(OnMainMenu);
    }
}