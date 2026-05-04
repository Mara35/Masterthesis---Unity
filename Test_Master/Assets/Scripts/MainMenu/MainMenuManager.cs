using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene Namen (exakt wie in Build Settings)")]
    [SerializeField] private string manuellesTrainingSceneName = "BoxBlock_Training";
    [SerializeField] private string visuellesTrainingSceneName = "BoxBlock_VisuellesTraining"; 
    [SerializeField] private string competitionSceneName = "BoxBlock_Competition";

    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;      
    [SerializeField] private GameObject trainingInfoPanel;  

    [Header("Hauptmenü Buttons")]
    [SerializeField] private Button trainingButton;
    [SerializeField] private Button competitionButton;

    [Header("Trainings-Info Buttons")]
    [SerializeField] private Button manuellesTrainingButton;
    [SerializeField] private Button visuellesTrainingButton;
    [SerializeField] private Button zurueckButton;


    private void Start()
    {
        // Sicherstellen: Hauptmenü sichtbar, Info-Panel versteckt
        ShowMainMenu();

        // Hauptmenü-Buttons
        if (trainingButton != null) trainingButton.onClick.AddListener(ShowTrainingInfo);
        if (competitionButton != null) competitionButton.onClick.AddListener(LoadCompetitionScene);

        // Trainings-Info-Buttons
        if (manuellesTrainingButton != null) manuellesTrainingButton.onClick.AddListener(LoadManuellesTraining);
        if (visuellesTrainingButton != null) visuellesTrainingButton.onClick.AddListener(LoadVisuellesTraining);
    }

    private void OnDestroy()
    {
        if (trainingButton != null) trainingButton.onClick.RemoveListener(ShowTrainingInfo);
        if (competitionButton != null) competitionButton.onClick.RemoveListener(LoadCompetitionScene);
        if (manuellesTrainingButton != null) manuellesTrainingButton.onClick.RemoveListener(LoadManuellesTraining);
        if (visuellesTrainingButton != null) visuellesTrainingButton.onClick.RemoveListener(LoadVisuellesTraining);
    }

    
    private void ShowMainMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (trainingInfoPanel != null) trainingInfoPanel.SetActive(false);
    }

    
    private void ShowTrainingInfo()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (trainingInfoPanel != null) trainingInfoPanel.SetActive(true);
    }

    
    private void LoadManuellesTraining()
    {
        SceneManager.LoadScene(manuellesTrainingSceneName);
    }

    
    private void LoadVisuellesTraining()
    {
        // TODO: Ersetzen sobald BoxBlock_VisuellesTraining Scene vorhanden ist
        Debug.Log("[MainMenu] Visuelles Training noch nicht verfügbar.");

        
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