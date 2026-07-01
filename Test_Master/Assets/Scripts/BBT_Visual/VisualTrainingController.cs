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
        [Tooltip("The cube in the starting zone")]
        public Transform block;

        [Tooltip("Drop-off point in the target zone")]
        public Transform dropTarget;

        [Tooltip("Color name for the instruction text")]
        public string colorName_ = "green";

        [Tooltip("Color of the associated drop target")]
        public Color targetHighlightColor = Color.white;
    }

    [Header("Cube sequence (order of the exercise)")]
    [SerializeField] private List<BlockEntry> blocks = new List<BlockEntry>();

    [Header("Hand")]
    [SerializeField] private AutoHandMover handMover;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private TextMeshProUGUI finishedText;

    [Header("VR UI (World Space - display only)")]
    [Tooltip("Instruction text on Canvas_VR")]
    [SerializeField] private TextMeshProUGUI vrInstructionText;
    [Tooltip("Copy of FinishedPanel on Canvas_VR (buttons disabled)")]
    [SerializeField] private GameObject vrFinishedPanel;
    [Tooltip("Optional: separate finished text on Canvas_VR")]
    [SerializeField] private TextMeshProUGUI vrFinishedText;

    [Header("Buttons (at the end)")]
    [Tooltip("Assign FinishedPanel")]
    [SerializeField] private GameObject finishedPanel;
    [Tooltip("Repeat button")]
    [SerializeField] private Button repeatButton;
    [Tooltip("MainMenu button")]
    [SerializeField] private Button mainMenuButton;
    [Tooltip("Name of the main scene")]
    [SerializeField] private string mainSceneName = "MainMenu";

    [Header("Timing")]
    [SerializeField] private float instructionDisplayTime = 3.0f;
    [SerializeField] private float pauseBetweenBlocks = 1.0f;

    [Header("Texts")]
    [SerializeField]
    private string instructionTemplate =
        "Now imagine picking up \nthe {COLOR} cube.";

    // -----------------------------------------------------------------------
    private void Start()
    {
        if (instructionText != null) instructionText.text = "";
        if (vrInstructionText != null) vrInstructionText.text = "";

        // Hide the panel at the start
        if (finishedPanel != null) finishedPanel.SetActive(false);
        if (vrFinishedPanel != null) vrFinishedPanel.SetActive(false);

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
                Debug.LogWarning($"[VisualTraining] Block {i} is missing - skipped");
                continue;
            }

            // 1. Color the DropTarget and display it
            SetTargetColor(entry.dropTarget, entry.targetHighlightColor);

            // 2. Instructions
            ShowInstruction(entry.colorName_);

            // 3. Time to read
            yield return new WaitForSeconds(instructionDisplayTime);

            // 4. Drop position: X/Z from the target, Y from the cube
            Vector3 dropPos = new Vector3(
                entry.dropTarget.position.x,
                entry.block.position.y,
                entry.dropTarget.position.z
            );

            // 5. Hand sequence
            bool done = false;
            handMover.RunSequence(entry.block, dropPos, () => done = true);
            yield return new WaitUntil(() => done);

            // 6. Hide marker
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
        string text = instructionTemplate.Replace("{COLOR}", colorName);

        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(true);
            instructionText.text = text;
        }
        if (vrInstructionText != null)
        {
            vrInstructionText.gameObject.SetActive(true);
            vrInstructionText.text = text;
        }
    }

    private void ClearInstruction()
    {
        if (instructionText != null) instructionText.text = "";
        if (vrInstructionText != null) vrInstructionText.text = "";
    }

    private void ShowFinished()
    {
        ClearInstruction();
        if (instructionText != null) instructionText.gameObject.SetActive(false);
        if (vrInstructionText != null) vrInstructionText.gameObject.SetActive(false);

        // Show panel (PC + VR)
        if (finishedPanel != null) finishedPanel.SetActive(true);
        if (vrFinishedPanel != null) vrFinishedPanel.SetActive(true);

        // Optional VR finished text
        if (vrFinishedText != null && finishedText != null)
            vrFinishedText.text = finishedText.text;

        Debug.Log("[VisualTraining] All cubes completed.");
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