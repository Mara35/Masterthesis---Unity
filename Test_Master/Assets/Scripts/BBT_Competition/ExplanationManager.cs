using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Drives the pre-competition explanation screen (PC and VR). Shows a level-select page and a detail
/// page describing each of the six levels (title, cube preview, colors), and launches the competition
/// or returns to the menu.
/// </summary>
public class ExplanationManager : MonoBehaviour
{
    [Header("Pages")]
    public GameObject pageSelect;
    public GameObject pageDetail;

    [Header("VR Pages")]
    public GameObject vrpageSelect;
    public GameObject vrpageDetail;

    [Header("Detail Page UI")]
    public TextMeshProUGUI detailTitle;
    public TextMeshProUGUI detailSubtitle;
    public TextMeshProUGUI cubeDesc;
    public Image cubeVisual;
    public TextMeshProUGUI newLabel;

    [Header("VR Detail Page UI")]
    public TextMeshProUGUI vrdetailTitle;
    public TextMeshProUGUI vrdetailSubtitle;
    public TextMeshProUGUI vrcubeDesc;
    public Image vrcubeVisual;
    public TextMeshProUGUI vrnewLabel;

    [Header("Cube Preview Panels (one per level, assign in order)")]
    [Tooltip("6 Panels: Basic, Motor, Reaction, Cognitive, Memory, Full")]
    public GameObject[] previewPanels;

    [Header("VR Cube Preview Panels (one per level, assign in order)")]
    [Tooltip("6 Panels: Basic, Motor, Reaction, Cognitive, Memory, Full")]
    public GameObject[] vrpreviewPanels;

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
                subtitle      = "Learn the core mechanic. Transfer as many blocks as possible in 60 seconds.",
                newBlockLabel = "Bonus & Malus Blocks",
                description   = "Green dice give you bonus points when they're on your side of the board, your score goes down. Try to steal them from your opponent's side.\r\n\r\nRed dice give you penalty points when they're on your side of the board, your score goes up. Try to place them on your opponent's side",
                cubeColor     = colorGreen,
                difficulty    = DifficultyLevel.Basic
            },
            new LevelData {
                title         = "Motor",
                subtitle      = "Everything from Basic, plus a freeze mechanic to disrupt your opponent.",
                newBlockLabel = "Freeze Block",
                description   = "A blue block appears in the field. Pick it up and place it in the opponent's Freeze Zone to freeze them for 5 seconds. They cannot move any blocks during that time.",
                cubeColor     = colorBlue,
                difficulty    = DifficultyLevel.Motor
            },
            new LevelData {
                title         = "Reaction",
                subtitle      = "Everything from Motor, plus a timed reaction challenge.",
                newBlockLabel = "Reaction Block",
                description   = "A blinking white block appears for only 3 seconds. Pick it up in time for +2 bonus points. Miss it and receive -2 points. Trains reaction time and sustained attention.",
                cubeColor     = colorWhite,
                difficulty    = DifficultyLevel.Reaction
            },
            new LevelData {
                title         = "Cognitive",
                subtitle      = "Focus on the Peg Challenge, a colour-matching dual task.",
                newBlockLabel = "Peg Challenge",
                description   = "3 coloured cylinders spawn in your zone with 3 matching target zones outside the box. Place each cylinder in the correct colour zone within 8 seconds. Combines fine motor control with colour recognition.",
                cubeColor     = colorRed,
                difficulty    = DifficultyLevel.Cognitive
            },
            new LevelData {
                title         = "Memory",
                subtitle      = "Focus on working memory, remember and reproduce the correct sequence.",
                newBlockLabel = "Sequence Blocks",
                description   = "3 purple blocks appear with numbers 1, 2 and 3. Remember the order, the numbers disappear after 3 seconds. Transfer them in the correct sequence for +5 bonus points. A wrong order means -2 points.",
                cubeColor     = colorPurple,
                difficulty    = DifficultyLevel.Memory
            },
            new LevelData {
                title         = "Full Challenge",
                subtitle      = "All block types active, maximum motor and cognitive challenge.",
                newBlockLabel = "Everything Combined",
                description   = "All block types are active at once: Bonus/Malus, Freeze, Reaction, Peg Challenge and Sequence Blocks. This level combines multiple cognitive functions simultaneously, attention, working memory, colour recognition and motor control.",
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

        // PC Canvas
        if (detailTitle != null) detailTitle.text = l.title;
        if (detailSubtitle != null) detailSubtitle.text = l.subtitle;
        if (cubeDesc != null) cubeDesc.text = l.description;
        if (newLabel != null) newLabel.text = l.newBlockLabel.ToUpper();
        if (cubeVisual != null) cubeVisual.color = l.cubeColor;

        //VR Canvas
        if (vrdetailTitle != null) vrdetailTitle.text = l.title;
        if (vrdetailSubtitle != null) vrdetailSubtitle.text = l.subtitle;
        if (vrcubeDesc != null) vrcubeDesc.text = l.description;
        if (vrnewLabel != null) vrnewLabel.text = l.newBlockLabel.ToUpper();
        if (vrcubeVisual != null) vrcubeVisual.color = l.cubeColor;

        // PC Preview Panels
        if (previewPanels != null)
        {
            for (int i = 0; i < previewPanels.Length; i++)
                if (previewPanels[i] != null)
                    previewPanels[i].SetActive(i == index);
        }

        // VR Preview Panels
        if (vrpreviewPanels != null)
        {
            for (int i = 0; i < vrpreviewPanels.Length; i++)
                if (vrpreviewPanels[i] != null)
                    vrpreviewPanels[i].SetActive(i == index);
        }

        // Persist the chosen level for the competition scene, then show its detail page.
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

        if (vrpageSelect != null) vrpageSelect.SetActive(true);
        if (vrpageDetail != null) vrpageDetail.SetActive(false);
    }

    private void ShowDetailPage()
    {
        if (pageSelect != null) pageSelect.SetActive(false);
        if (pageDetail != null) pageDetail.SetActive(true);

        if (vrpageSelect != null) vrpageSelect.SetActive(false);
        if (vrpageDetail != null) vrpageDetail.SetActive(true);
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