using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Identisch mit dem ursprünglichen SimpleGrabber.
/// Einzige Änderung für Phase 1:
///   holdPoint ? auf den Hand-Bone des XBot (mixamorig:RightHand) zeigen lassen,
///   NICHT mehr auf das HandProxy.
///
/// Das Script selbst bleibt auf dem HandTarget-GameObject.
/// </summary>
public class SimpleGrabber_Xbot : MonoBehaviour
{
    [Tooltip("Hand-Bone des XBot (mixamorig:RightHand) – NICHT mehr HandProxy")]
    public Transform holdPoint;

    public KeyCode grabKey = KeyCode.E;
    public KeyCode releaseKey = KeyCode.Q;

    private readonly List<BlockItem> candidates = new List<BlockItem>();
    private BlockItem heldBlock;
    private TestTimer timer;

    // Öffentlich lesbar für PartitionZone
    public BlockItem HeldBlock => heldBlock;

    private void Start()
    {
        timer = FindObjectOfType<TestTimer>();
    }

    private void Update()
    {
        if (timer != null && !timer.IsRunning)
            return;

        if (Input.GetKeyDown(grabKey) && heldBlock == null)
            TryGrabNearest();

        if (Input.GetKeyDown(releaseKey) && heldBlock != null)
            ReleaseHeldBlock();
    }

    private void TryGrabNearest()
    {
        float bestDistance = float.MaxValue;
        BlockItem bestBlock = null;

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (candidates[i] == null) { candidates.RemoveAt(i); continue; }
            if (!candidates[i].CanBeGrabbed) continue;

            float d = Vector3.Distance(holdPoint.position, candidates[i].transform.position);
            if (d < bestDistance) { bestDistance = d; bestBlock = candidates[i]; }
        }

        if (bestBlock != null)
        {
            heldBlock = bestBlock;
            heldBlock.Grab(holdPoint);
            Debug.Log("[SimpleGrabber] Block gegriffen.");
        }
    }

    private void ReleaseHeldBlock()
    {
        if (heldBlock == null) return;

        bool valid = heldBlock.Release();
        if (valid) Debug.Log("[SimpleGrabber] VALID TRANSFER");

        heldBlock = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        BlockItem block = other.GetComponent<BlockItem>();
        if (block != null && !candidates.Contains(block))
            candidates.Add(block);
    }

    private void OnTriggerExit(Collider other)
    {
        BlockItem block = other.GetComponent<BlockItem>();
        if (block != null)
            candidates.Remove(block);
    }
}