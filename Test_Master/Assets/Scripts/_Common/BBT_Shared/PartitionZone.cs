using UnityEngine;

/// <summary>
/// Trigger on the center partition. When a hand object passes through, it tells whichever grabber
/// currently holds a block that the block crossed the partition (used by BlockItem's transfer
/// validation). Checks the active input in priority order: AutoHandMover, then GloveGrabber, then
/// the legacy SimpleGrabber.
/// </summary>

public class PartitionZone : MonoBehaviour
{
    [Tooltip("Names of GameObjects that trigger this zone")]
    public string[] triggerObjectNames = { "HandProxy", "GrabTrigger", "HandTarget", "HandMoverGhost" };

    private SimpleGrabber simpleGrabber;
    private GloveGrabber gloveGrabber;
    private AutoHandMover autoHandMover;

    private void Start()
    {
        simpleGrabber = FindObjectOfType<SimpleGrabber>();
        gloveGrabber = FindObjectOfType<GloveGrabber>();
        autoHandMover = FindObjectOfType<AutoHandMover>();
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckTrigger(other);
    }

    private void OnTriggerStay(Collider other)
    {
        CheckTrigger(other);
    }

    private void CheckTrigger(Collider other)
    {
        bool isHandObject = System.Array.IndexOf(triggerObjectNames, other.name) >= 0;
        if (!isHandObject) return;

        // AutoHandMover: if not idle = block is being carried
        if (autoHandMover != null && !autoHandMover.IsIdle)
        {
            autoHandMover.NotifyPartitionPassed();
            return;
        }

        // GloveGrabber
        if (gloveGrabber != null && gloveGrabber.HeldBlock != null)
        {
            gloveGrabber.HeldBlock.OnPassedThroughPartitionZone();
            Debug.Log($"[PartitionZone] GloveGrabber: '{gloveGrabber.HeldBlock.name}' Crossed the zone?");
            return;
        }

        // SimpleGrabber
        if (simpleGrabber != null && simpleGrabber.HeldBlock != null)
        {
            simpleGrabber.HeldBlock.OnPassedThroughPartitionZone();
            Debug.Log($"[PartitionZone] SimpleGrabber: '{simpleGrabber.HeldBlock.name}' Crossed the zone?");
        }
    }
}