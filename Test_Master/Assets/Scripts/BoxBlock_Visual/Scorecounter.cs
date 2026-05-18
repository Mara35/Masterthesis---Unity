using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Score counter for BoxBlock_VisualTraining and BoxBlock_Training_offline.
/// BoxBlock_VisualTraining (AutoHandMover):
///   useBlockItemValidation = false: checks whether the cube is to the left of the partition
/// BoxBlock_Training_offline (GloveGrabber):
///   useBlockItemValidation = true: checks BlockItem.IsValidlyTransferred
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ScoreCounter : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI scoreText;

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
        if (countedInstanceIDs.Contains(id)) return;

        bool valid = false;

        if (useBlockItemValidation)
        {
            // GloveGrabber Flow
            valid = block.IsValidlyTransferred;
        }
        else
        {
            // AutoHandMover Flow: Cube to the left of the partition = valid transfer
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
            Debug.Log($"[ScoreCounter] '{block.name}' counted – VALID transfer. Score={score}");
        }
        else
        {
            Debug.Log($"[ScoreCounter] '{block.name}' rejected – invalid transfer.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        int id = other.gameObject.GetInstanceID();
        if (countedInstanceIDs.Remove(id))
        {
            score = Mathf.Max(0, score - 1);
            UpdateUI();
            Debug.Log($"[ScoreCounter] leave block. Score={score}");
        }
    }

    private void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + score;
    }

    public int GetScore() => score;
    public void ResetScore() { countedInstanceIDs.Clear(); score = 0; UpdateUI(); }
}