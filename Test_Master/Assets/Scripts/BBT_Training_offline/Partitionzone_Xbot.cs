using UnityEngine;

public class PartitionZone_Xbot : MonoBehaviour
{
    // Supports both: the old SimpleGrabber and the new GloveGrabber
    private SimpleGrabber simpleGrabber;
    private GloveGrabber gloveGrabber;

    [Tooltip("Names of the GameObjects that trigger this zone")]
    public string[] triggerObjectNames = { "HandProxy", "GrabTrigger", "HandTarget" };

    private void Start()
    {
        simpleGrabber = FindObjectOfType<SimpleGrabber>();
        gloveGrabber = FindObjectOfType<GloveGrabber>();
    }

    private void OnTriggerEnter(Collider other)
    {
        bool isHandObject = System.Array.IndexOf(triggerObjectNames, other.name) >= 0;
        if (!isHandObject) return;

        // GloveGrabber (new)
        if (gloveGrabber != null && gloveGrabber.HeldBlock != null)
        {
            gloveGrabber.HeldBlock.OnPassedThroughPartitionZone();
            Debug.Log("[PartitionZone] GloveGrabber: Block through the zone?");
            return;
        }

        // SimpleGrabber (old, Fallback)
        if (simpleGrabber != null && simpleGrabber.HeldBlock != null)
        {
            simpleGrabber.HeldBlock.OnPassedThroughPartitionZone();
            Debug.Log("[PartitionZone] SimpleGrabber: Block through the zone ?");
        }
    }
}