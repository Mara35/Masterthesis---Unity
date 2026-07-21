using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string manuellesTrainingSceneName = "BBT_Training";
    [SerializeField] private string visuellesTrainingSceneName = "BBT_VisualTraining";
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
    [SerializeField] private Button manuellesTrainingButton;
    [SerializeField] private Button visuellesTrainingButton;
    [SerializeField] private Button zurueckButton;

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
        if (manuellesTrainingButton != null) manuellesTrainingButton.onClick.AddListener(ShowLevelSelect);
        if (visuellesTrainingButton != null) visuellesTrainingButton.onClick.AddListener(LoadVisuellesTraining);
        if (zurueckButton != null) zurueckButton.onClick.AddListener(ShowMainMenu);

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
        if (manuellesTrainingButton != null) manuellesTrainingButton.onClick.RemoveListener(ShowLevelSelect);
        if (visuellesTrainingButton != null) visuellesTrainingButton.onClick.RemoveListener(LoadVisuellesTraining);
        if (zurueckButton != null) zurueckButton.onClick.RemoveListener(ShowMainMenu);
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
        LoadManuellesTraining();
    }

    private void SelectMedium()
    {
        LevelConfig.Selected = mediumConfig;
        LoadManuellesTraining();
    }

    private void SelectHard()
    {
        LevelConfig.Selected = hardConfig;
        LoadManuellesTraining();
    }

    // --- Scene Loading ---

    private void LoadManuellesTraining()
    {
        SceneManager.LoadScene(manuellesTrainingSceneName);
    }

    private void LoadVisuellesTraining()
    {
        SceneManager.LoadScene(visuellesTrainingSceneName);
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