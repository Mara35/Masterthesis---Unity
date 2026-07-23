using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drives the main menu on both the PC and VR (world-space) canvases. Toggles between the main,
/// training-info and level-select panels, and loads the chosen scene (manual training, visual
/// training, or the competition explanation). For manual training it writes the picked difficulty
/// into <see cref="LevelConfig.Selected"/> so the gameplay scene can read it after the load.
/// The Easy/Medium/Hard defaults are seeded in <see cref="Reset"/>.
/// </summary>

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string manualTrainingSceneName = "BBT_Training";
    [SerializeField] private string visualTrainingSceneName = "BBT_VisualTraining";
    [SerializeField] private string competitionSceneName = "BBT_Explanation_Competition";

    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject trainingInfoPanel;
    [SerializeField] private GameObject levelSelectPanel;

    [Header("VR Panels")]
    [SerializeField] private GameObject vrMainMenuPanel;
    [SerializeField] private GameObject vrTrainingInfoPanel;
    [SerializeField] private GameObject vrLevelSelectPanel;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button trainingButton;
    [SerializeField] private Button competitionButton;

    [Header("Training Info Buttons")]
    [SerializeField] private Button manualTrainingButton;
    [SerializeField] private Button visualTrainingButton;
    [SerializeField] private Button backButton;

    [Header("Level Select Buttons")]
    [SerializeField] private Button easyButton;
    [SerializeField] private Button mediumButton;
    [SerializeField] private Button hardButton;
    [SerializeField] private Button levelBackButton;

    [Header("Level Configs")]
    [SerializeField] private LevelConfig easyConfig;
    [SerializeField] private LevelConfig mediumConfig;
    [SerializeField] private LevelConfig hardConfig;

    private void Reset()
    {
        easyConfig = new LevelConfig
        {
            levelName = "Easy",
            gripMcpThreshold = -25f,
            gripPipThreshold = -30f,
            minFingersForGrip = 2,
            releaseHysteresis = 5f
        };
        mediumConfig = new LevelConfig
        {
            levelName = "Medium",
            gripMcpThreshold = -40f,
            gripPipThreshold = -50f,
            minFingersForGrip = 3,
            releaseHysteresis = 5f
        };
        hardConfig = new LevelConfig
        {
            levelName = "Hard",
            gripMcpThreshold = -55f,
            gripPipThreshold = -70f,
            minFingersForGrip = 4,
            releaseHysteresis = 5f
        };
    }

    private void Start()
    {
        ShowMainMenu();

        // MainMenu
        if (trainingButton != null) trainingButton.onClick.AddListener(ShowTrainingInfo);
        if (competitionButton != null) competitionButton.onClick.AddListener(LoadCompetitionScene);

        // Training Info
        if (manualTrainingButton != null) manualTrainingButton.onClick.AddListener(ShowLevelSelect);
        if (visualTrainingButton != null) visualTrainingButton.onClick.AddListener(LoadVisualTraining);
        if (backButton != null) backButton.onClick.AddListener(ShowMainMenu);

        // Level Select
        if (easyButton != null) easyButton.onClick.AddListener(SelectEasy);
        if (mediumButton != null) mediumButton.onClick.AddListener(SelectMedium);
        if (hardButton != null) hardButton.onClick.AddListener(SelectHard);
        if (levelBackButton != null) levelBackButton.onClick.AddListener(ShowTrainingInfo);
    }

    private void OnDestroy()
    {
        if (trainingButton != null) trainingButton.onClick.RemoveListener(ShowTrainingInfo);
        if (competitionButton != null) competitionButton.onClick.RemoveListener(LoadCompetitionScene);
        if (manualTrainingButton != null) manualTrainingButton.onClick.RemoveListener(ShowLevelSelect);
        if (visualTrainingButton != null) visualTrainingButton.onClick.RemoveListener(LoadVisualTraining);
        if (backButton != null) backButton.onClick.RemoveListener(ShowMainMenu);
        if (easyButton != null) easyButton.onClick.RemoveListener(SelectEasy);
        if (mediumButton != null) mediumButton.onClick.RemoveListener(SelectMedium);
        if (hardButton != null) hardButton.onClick.RemoveListener(SelectHard);
        if (levelBackButton != null) levelBackButton.onClick.RemoveListener(ShowTrainingInfo);
    }

    // --- Panel Navigation ---

    private void ShowMainMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (trainingInfoPanel != null) trainingInfoPanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);

        if (vrMainMenuPanel != null) vrMainMenuPanel.SetActive(true);
        if (vrTrainingInfoPanel != null) vrTrainingInfoPanel.SetActive(false);
        if (vrLevelSelectPanel != null) vrLevelSelectPanel.SetActive(false);
    }

    private void ShowTrainingInfo()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (trainingInfoPanel != null) trainingInfoPanel.SetActive(true);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);

        if (vrMainMenuPanel != null) vrMainMenuPanel.SetActive(false);
        if (vrTrainingInfoPanel != null) vrTrainingInfoPanel.SetActive(true);
        if (vrLevelSelectPanel != null) vrLevelSelectPanel.SetActive(false);
    }

    private void ShowLevelSelect()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (trainingInfoPanel != null) trainingInfoPanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(true);

        if (vrMainMenuPanel != null) vrMainMenuPanel.SetActive(false);
        if (vrTrainingInfoPanel != null) vrTrainingInfoPanel.SetActive(false);
        if (vrLevelSelectPanel != null) vrLevelSelectPanel.SetActive(true);
    }

    // --- Level Selection ---

    private void SelectEasy()
    {
        LevelConfig.Selected = easyConfig;
        LoadManualTraining();
    }

    private void SelectMedium()
    {
        LevelConfig.Selected = mediumConfig;
        LoadManualTraining();
    }

    private void SelectHard()
    {
        LevelConfig.Selected = hardConfig;
        LoadManualTraining();
    }

    // --- Scene Loading ---

    private void LoadManualTraining()
    {
        SceneManager.LoadScene(manualTrainingSceneName);
    }

    private void LoadVisualTraining()
    {
        SceneManager.LoadScene(visualTrainingSceneName);
    }

    private void LoadCompetitionScene()
    {
        SceneManager.LoadScene(competitionSceneName);
    }

    public void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}