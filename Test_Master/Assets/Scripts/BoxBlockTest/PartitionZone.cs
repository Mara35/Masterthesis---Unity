using UnityEngine;

public class PartitionZone : MonoBehaviour
{
    [Tooltip("Namen der GameObjects die diese Zone auslösen")]
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
        bool isHandObject = System.Array.IndexOf(triggerObjectNames, other.name) >= 0;
        if (!isHandObject) return;

        Debug.Log($"[PartitionZone] '{other.name}' hat Zone betreten. AutoHandMover IsIdle={autoHandMover?.IsIdle}");

        // AutoHandMover: wenn nicht idle = Block wird getragen
        if (autoHandMover != null && !autoHandMover.IsIdle)
        {
            autoHandMover.NotifyPartitionPassed();
            Debug.Log("[PartitionZone] AutoHandMover: NotifyPartitionPassed aufgerufen ?");
            return;
        }

        // GloveGrabber
        if (gloveGrabber != null && gloveGrabber.HeldBlock != null)
        {
            gloveGrabber.HeldBlock.OnPassedThroughPartitionZone();
            Debug.Log($"[PartitionZone] GloveGrabber: '{gloveGrabber.HeldBlock.name}' Zone passiert ?");
            return;
        }

        // SimpleGrabber
        if (simpleGrabber != null && simpleGrabber.HeldBlock != null)
        {
            simpleGrabber.HeldBlock.OnPassedThroughPartitionZone();
            Debug.Log($"[PartitionZone] SimpleGrabber: '{simpleGrabber.HeldBlock.name}' Zone passiert ?");
            return;
        }

        Debug.Log($"[PartitionZone] '{other.name}' - kein Block gehalten.");
    }
}