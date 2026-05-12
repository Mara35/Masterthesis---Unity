using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class VisualTrainingController : MonoBehaviour
{
    [Serializable]
    public class BlockEntry
    {
        [Tooltip("Der Würfel in der Startzone")]
        public Transform block;

        [Tooltip("Ablagepunkt in der Zielzone")]
        public Transform dropTarget;

        [Tooltip("Farb-Name für den Instruktionstext, z.B. 'grünen'")]
        public string colorNameDE = "grünen";

        [Tooltip("Farbe des zugehörigen DropTargets")]
        public Color targetHighlightColor = Color.white;
    }

    [Header("Würfel-Sequenz (Reihenfolge der Übung)")]
    [SerializeField] private List<BlockEntry> blocks = new List<BlockEntry>();

    [Header("Hand")]
    [SerializeField] private AutoHandMover handMover;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private TextMeshProUGUI finishedText;

    [Header("Buttons (am Ende)")]
    [Tooltip("FinishedPanel zuweisen – wird am Ende eingeblendet (enthält Text + Buttons).")]
    [SerializeField] private GameObject finishedPanel;
    [Tooltip("Button zum Wiederholen der Übung.")]
    [SerializeField] private Button repeatButton;
    [Tooltip("Button zum Zurückkehren zur Hauptszene.")]
    [SerializeField] private Button mainMenuButton;
    [Tooltip("Name der Hauptszene (muss in Build Settings eingetragen sein).")]
    [SerializeField] private string mainSceneName = "MainMenu";

    [Header("Timing")]
    [SerializeField] private float instructionDisplayTime = 3.0f;
    [SerializeField] private float pauseBetweenBlocks = 1.0f;

    [Header("Texte (anpassbar)")]
    [SerializeField]
    private string instructionTemplate =
        "Stelle dir jetzt vor,\ndu hebst den {COLOR} Würfel auf.";

    // -----------------------------------------------------------------------
    private void Start()
    {
        if (instructionText != null) instructionText.text = "";

        // Panel zu Beginn verstecken (Text ist Kind des Panels)
        if (finishedPanel != null) finishedPanel.SetActive(false);
        if (repeatButton != null) repeatButton.onClick.AddListener(OnRepeat);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenu);

        StartCoroutine(RunTrainingSequence());
    }

    // -----------------------------------------------------------------------
    private IEnumerator RunTrainingSequence()
    {
        yield return new WaitForSeconds(1.0f);

        for (int i = 0; i < blocks.Count; i++)
        {
            BlockEntry entry = blocks[i];

            if (entry.block == null || entry.dropTarget == null)
            {
                Debug.LogWarning($"[VisualTraining] Block {i} fehlt – übersprungen.");
                continue;
            }

            // 1. DropTarget einfärben und anzeigen
            SetTargetColor(entry.dropTarget, entry.targetHighlightColor);

            // 2. Instruktionstext
            ShowInstruction(entry.colorNameDE);

            // 3. Lesen lassen
            yield return new WaitForSeconds(instructionDisplayTime);

            // 4. Drop-Position: X/Z vom Target, Y vom Würfel
            Vector3 dropPos = new Vector3(
                entry.dropTarget.position.x,
                entry.block.position.y,
                entry.dropTarget.position.z
            );

            // 5. Hand-Sequenz
            bool done = false;
            handMover.RunSequence(entry.block, dropPos, () => done = true);
            yield return new WaitUntil(() => done);

            // 6. Marker ausblenden
            ResetTargetColor(entry.dropTarget);

            // 7. Pause
            if (i < blocks.Count - 1)
            {
                ClearInstruction();
                yield return new WaitForSeconds(pauseBetweenBlocks);
            }
        }

        ShowFinished();
    }

    // -----------------------------------------------------------------------
    private void SetTargetColor(Transform target, Color color)
    {
        if (target == null) return;
        var marker = target.GetComponent<DropTargetMarker>();
        if (marker != null)
        {
            marker.SetColor(color);
            marker.SetVisible(true);
        }
    }

    private void ResetTargetColor(Transform target)
    {
        if (target == null) return;
        var marker = target.GetComponent<DropTargetMarker>();
        if (marker != null) marker.SetVisible(false);
    }

    // -----------------------------------------------------------------------
    private void ShowInstruction(string colorName)
    {
        if (instructionText == null) return;
        instructionText.gameObject.SetActive(true);
        instructionText.text = instructionTemplate.Replace("{COLOR}", colorName);
    }

    private void ClearInstruction()
    {
        if (instructionText != null) instructionText.text = "";
    }

    private void ShowFinished()
    {
        ClearInstruction();
        if (instructionText != null) instructionText.gameObject.SetActive(false);

        // Panel einblenden (enthält Text + Buttons)
        if (finishedPanel != null)
            finishedPanel.SetActive(true);

        Debug.Log("[VisualTraining] Alle Würfel abgeschlossen.");
    }

    // -----------------------------------------------------------------------
    private void OnRepeat()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnMainMenu()
    {
        SceneManager.LoadScene(mainSceneName);
    }
}