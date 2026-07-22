using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Counts blocks that reach the target side. Two validation modes selectable in the Inspector:
/// BlockItem's own transfer flag (glove mode) or a simple "is past the partition" position check
/// (auto-hand mode). Each block is counted at most once (tracked by instance id) and un-counted if
/// it leaves the zone without a valid transfer. Mirrors the score to a world-space VR label.
/// </summary>

[RequireComponent(typeof(BoxCollider))]
public class ScoreCounter : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI scoreText;

    [Header("VR UI (World Space)")]
    [Tooltip("Score text on Canvas_VR")]
    public TextMeshProUGUI vrScoreText;

    [Header("Partition")]
    [Tooltip("Will be automatically filled in if left blank")]
    public Transform partitionTransform;

    [Header("Validation")]
    [Tooltip("FALSE = AutoHandMover\nTRUE = GloveGrabber")]
    public bool useBlockItemValidation = false;

    private HashSet<int> countedInstanceIDs = new HashSet<int>();
    private int score = 0;
    public int CurrentCount => score;

    private void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (partitionTransform == null)
        {
            var go = GameObject.Find("CenterPartition");
            if (go != null) partitionTransform = go.transform;
        }
        UpdateUI();
    }

    private void OnTriggerEnter(Collider other)
    {
        BlockItem block = other.GetComponent<BlockItem>();
        if (block == null) return;

        int id = other.gameObject.GetInstanceID();
        if (countedInstanceIDs.Contains(id)) return; // already scored this block, ignore

        bool valid = false;

        if (useBlockItemValidation)
        {
            valid = block.IsValidlyTransferred;
        }
        else
        {
            if (partitionTransform != null)
                valid = other.transform.position.x < partitionTransform.position.x;
            else
                valid = true;
        }

        if (valid)
        {
            countedInstanceIDs.Add(id);
            score++;
            UpdateUI();
            Debug.Log($"[ScoreCounter] '{block.name}' counted - VALID transfer. Score={score}");
        }
        else
        {
            Debug.Log($"[ScoreCounter] '{block.name}' rejected - invalid transfer.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        BlockItem block = other.GetComponent<BlockItem>();
        if (block == null) return;

        int id = other.gameObject.GetInstanceID();

        if (countedInstanceIDs.Contains(id) && !block.IsValidlyTransferred)
        {
            countedInstanceIDs.Remove(id);
            score = Mathf.Max(0, score - 1);
            UpdateUI();
            Debug.Log($"[ScoreCounter] '{block.name}' left zone invalidly - Score={score}");
        }
    }

    private void UpdateUI()
    {
        string label = "Score: " + score;
        if (scoreText != null) scoreText.text = label;
        if (vrScoreText != null) vrScoreText.text = label;
    }

    public int GetScore() => score;
    public void ResetScore() { countedInstanceIDs.Clear(); score = 0; UpdateUI(); }
}