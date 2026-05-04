using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class VisualTrainingController : MonoBehaviour
{

    [Serializable]
    public class BlockEntry
    {
        [Tooltip("Der Würfel in der Startzone")]
        public Transform block;

        [Tooltip("Ablagepunkt in der Zielzone (leeres GameObject als Marker setzen)")]
        public Transform dropTarget;

        [Tooltip("Farb-Name für den Instruktionstext, z.B. 'grünen'")]
        public string colorNameDE = "grünen";
    }

    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Würfel-Sequenz (Reihenfolge der Übung)")]
    [SerializeField] private List<BlockEntry> blocks = new List<BlockEntry>();

    [Header("Hand")]
    [SerializeField] private AutoHandMover handMover;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private TextMeshProUGUI finishedText;

    [Header("Timing")]
    [SerializeField] private float instructionDisplayTime = 3.0f;  // Sek. Text lesen
    [SerializeField] private float pauseBetweenBlocks = 1.0f;  // Sek. zwischen Würfeln

    [Header("Texte (anpassbar)")]
    [SerializeField]
    private string instructionTemplate =
        "Stelle dir jetzt vor,\ndu hebst den {COLOR} Würfel auf.";
    [SerializeField]
    private string finishedMessage =
        "Gut gemacht!\nDu hast alle Würfel transportiert.";


    private void Start()
    {
        if (finishedText != null) finishedText.gameObject.SetActive(false);
        if (instructionText != null) instructionText.text = "";

        StartCoroutine(RunTrainingSequence());
    }

    private IEnumerator RunTrainingSequence()
    {
        // Kurze Startpause damit die Scene geladen ist
        yield return new WaitForSeconds(1.0f);

        for (int i = 0; i < blocks.Count; i++)
        {
            BlockEntry entry = blocks[i];

            if (entry.block == null || entry.dropTarget == null)
            {
                Debug.LogWarning($"[VisualTraining] Block {i} hat fehlende Referenzen – übersprungen.");
                continue;
            }

            // 1. Instruktionstext anzeigen
            ShowInstruction(entry.colorNameDE);

            // 2. Text lesen lassen
            yield return new WaitForSeconds(instructionDisplayTime);

            // 3. Hand-Sequenz starten und warten bis sie fertig ist
            bool done = false;
            handMover.RunSequence(entry.block, entry.dropTarget.position, () => done = true);

            yield return new WaitUntil(() => done);

            // 4. Pause zwischen Würfeln
            if (i < blocks.Count - 1)
            {
                ClearInstruction();
                yield return new WaitForSeconds(pauseBetweenBlocks);
            }
        }

        // Abschluss
        ShowFinished();
    }


    private void ShowInstruction(string colorName)
    {
        if (instructionText == null) return;
        instructionText.gameObject.SetActive(true);
        instructionText.text = instructionTemplate.Replace("{COLOR}", colorName);
    }

    private void ClearInstruction()
    {
        if (instructionText != null)
            instructionText.text = "";
    }

    private void ShowFinished()
    {
        ClearInstruction();

        if (finishedText != null)
        {
            finishedText.gameObject.SetActive(true);
            finishedText.text = finishedMessage;
        }

        Debug.Log("[VisualTraining] Alle Würfel abgeschlossen.");

    }
}
