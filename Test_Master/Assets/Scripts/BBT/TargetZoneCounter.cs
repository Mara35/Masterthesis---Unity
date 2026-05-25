using System.Collections.Generic;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(BoxCollider))]
public class TargetZoneCounter : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI scoreText;

    private HashSet<BlockItem> blocksInZone = new HashSet<BlockItem>();

    public int CurrentCount => blocksInZone.Count;

    private void Start()
    {
        UpdateUI();
    }

    private void OnTriggerEnter(Collider other)
    {
        BlockItem block = other.GetComponent<BlockItem>();

        if (block == null) return;

        // Nur zählen wenn der Transfer als gültig markiert wurde
        if (!block.IsValidlyTransferred)
        {
            Debug.Log("[TargetZone] Block rejected – invalid transfer.");
            return;
        }

        blocksInZone.Add(block);
        UpdateUI();
        Debug.Log("Blocks in zone: " + blocksInZone.Count);
    }

    private void OnTriggerExit(Collider other)
    {
        BlockItem block = other.GetComponent<BlockItem>();

        if (block != null)
        {
            blocksInZone.Remove(block);
            UpdateUI();
            Debug.Log("Blocks in zone: " + blocksInZone.Count);
        }
    }

    private void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + blocksInZone.Count;
    }
}