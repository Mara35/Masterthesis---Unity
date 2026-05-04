using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector Fields
    // -----------------------------------------------------------------------

    [Header("Scene Namen (exakt wie in Build Settings)")]
    [SerializeField] private string trainingSceneName = "BoxBlock_Training";
    [SerializeField] private string competitionSceneName = "BoxBlock_Competition";

    [Header("Buttons")]
    [SerializeField] private Button trainingButton;
    [SerializeField] private Button competitionButton;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        if (trainingButton != null) trainingButton.onClick.AddListener(LoadTrainingScene);
        if (competitionButton != null) competitionButton.onClick.AddListener(LoadCompetitionScene);
    }

    private void OnDestroy()
    {
        if (trainingButton != null) trainingButton.onClick.RemoveListener(LoadTrainingScene);
        if (competitionButton != null) competitionButton.onClick.RemoveListener(LoadCompetitionScene);
    }

    // -----------------------------------------------------------------------
    // Button Callbacks
    // -----------------------------------------------------------------------

    /// <summary>
    /// TrainingButton --> BoxBlock_Training Scene
    /// </summary>
    public void LoadTrainingScene()
    {
        SceneManager.LoadScene(trainingSceneName);
    }

    /// <summary>
    /// CompetitionButton --> BoxBlock_Competition Scene
    /// </summary>
    public void LoadCompetitionScene()
    {
        SceneManager.LoadScene(competitionSceneName);
    }

    /// <summary>
    /// Optionaler Quit-Button (kann später hinzugefügt werden)
    /// </summary>
    public void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}