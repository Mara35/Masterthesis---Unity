using UnityEngine;

public class PartitionZone_Xbot : MonoBehaviour
{
    // Unterstützt beide: alten SimpleGrabber und neuen GloveGrabber
    private SimpleGrabber simpleGrabber;
    private GloveGrabber gloveGrabber;

    [Tooltip("Namen der GameObjects die diese Zone auslösen")]
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

        // GloveGrabber (neu)
        if (gloveGrabber != null && gloveGrabber.HeldBlock != null)
        {
            gloveGrabber.HeldBlock.OnPassedThroughPartitionZone();
            Debug.Log("[PartitionZone] GloveGrabber: Block durch Zone ?");
            return;
        }

        // SimpleGrabber (alt, Fallback)
        if (simpleGrabber != null && simpleGrabber.HeldBlock != null)
        {
            simpleGrabber.HeldBlock.OnPassedThroughPartitionZone();
            Debug.Log("[PartitionZone] SimpleGrabber: Block durch Zone ?");
        }
    }
}