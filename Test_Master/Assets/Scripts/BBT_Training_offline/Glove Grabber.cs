using System.Collections.Generic;
using UnityEngine;

public class GloveGrabber : MonoBehaviour
{
    [Header("--- Grab Threshold ---")]
    public float gripMcpThreshold = -25f;
    public float gripPipThreshold = -30f;
    public int minFingersForGrip = 2;
    public float releaseHysteresis = 5f;

    [Header("--- Hold Point ---")]
    public Transform holdPoint;

    [Header("--- Debug ---")]
    public bool showDebugGUI = true;

    // Angles - set by CSVReplayController
    public float currentIndexMcp = 0f;
    public float currentIndexPip = 0f;
    public float currentMiddleMcp = 0f;
    public float currentMiddlePip = 0f;
    public float currentRingMcp = 0f;
    public float currentRingPip = 0f;
    public float currentPinkyMcp = 0f;
    public float currentPinkyPip = 0f;

    private readonly List<BlockItem> candidates = new List<BlockItem>();
    private BlockItem heldBlock;
    private bool isGripping = false;

    public BlockItem HeldBlock => heldBlock;
    public bool IsGripping => isGripping;

    private void Start()
    {
        if (LevelConfig.Selected != null)
        {
            gripMcpThreshold = LevelConfig.Selected.gripMcpThreshold;
            gripPipThreshold = LevelConfig.Selected.gripPipThreshold;
            minFingersForGrip = LevelConfig.Selected.minFingersForGrip;
            releaseHysteresis = LevelConfig.Selected.releaseHysteresis;
            Debug.Log($"[GloveGrabber] Level geladen: {LevelConfig.Selected.levelName}");
        }
        else
        {
            Debug.Log("[GloveGrabber] Kein Level ausgewählt - Inspector-Werte werden verwendet.");
        }
    }

    private void Update()
    {
        bool shouldGrip = CheckGripCondition();

        if (!isGripping && shouldGrip)
            TryGrab();
        else if (isGripping && !shouldGrip)
            Release();
    }

    bool CheckGripCondition()
    {
        float mcpT = isGripping ? gripMcpThreshold + releaseHysteresis : gripMcpThreshold;
        float pipT = isGripping ? gripPipThreshold + releaseHysteresis : gripPipThreshold;

        int count = 0;
        if (currentIndexMcp <= mcpT && currentIndexPip <= pipT) count++;
        if (currentMiddleMcp <= mcpT && currentMiddlePip <= pipT) count++;
        if (currentRingMcp <= mcpT && currentRingPip <= pipT) count++;
        if (currentPinkyMcp <= mcpT && currentPinkyPip <= pipT) count++;

        return count >= minFingersForGrip;
    }

    void TryGrab()
    {
        if (heldBlock != null) return;

        Vector3 searchPos = holdPoint ? holdPoint.position : transform.position;
        float radius = GetComponent<SphereCollider>()?.radius ?? 0.15f;

        if (candidates.Count == 0)
        {
            Collider[] hits = Physics.OverlapSphere(searchPos, radius);
            foreach (var h in hits)
            {
                BlockItem b = h.GetComponent<BlockItem>();
                if (b != null && !candidates.Contains(b))
                    candidates.Add(b);
            }
        }

        float bestDist = float.MaxValue;
        BlockItem best = null;

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (candidates[i] == null) { candidates.RemoveAt(i); continue; }
            float d = Vector3.Distance(searchPos, candidates[i].transform.position);
            if (d < bestDist) { bestDist = d; best = candidates[i]; }
        }

        if (best != null)
        {
            heldBlock = best;
            heldBlock.Grab(holdPoint ? holdPoint : transform);
            isGripping = true;
            Debug.Log($"[GloveGrabber] Gripped: {best.name}");
        }
    }

    void Release()
    {
        if (heldBlock == null) { isGripping = false; return; }

        // BlockItem.Release() checks PartitionZone and sets IsValidlyTransferred
        bool valid = heldBlock.Release();
        Debug.Log($"[GloveGrabber] Released: {heldBlock.name} - Transfer {(valid ? "valid" : "invalid")}");

        heldBlock = null;
        isGripping = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        BlockItem block = other.GetComponent<BlockItem>();
        if (block != null && !candidates.Contains(block))
        {
            candidates.Add(block);
            Debug.Log($"[GloveGrabber] TriggerEnter: {block.name}");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        BlockItem block = other.GetComponent<BlockItem>();
        if (block != null) candidates.Remove(block);
    }

    void OnGUI()
    {
        if (!showDebugGUI) return;
        GUILayout.BeginArea(new Rect(10, 150, 320, 110));
        GUILayout.Label($"[GloveGrabber] Gripping={isGripping} | Kandidaten={candidates.Count}");
        GUILayout.Label($"Index MCP={currentIndexMcp:F1}(T={gripMcpThreshold}) PIP={currentIndexPip:F1}(T={gripPipThreshold})");
        GUILayout.Label($"CheckGrip={CheckGripCondition()} | Block={heldBlock?.name ?? "-"}");
        GUILayout.EndArea();
    }
}