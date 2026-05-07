using UnityEngine;


public class PartitionZone : MonoBehaviour
{
    private SimpleGrabber grabber;

    [Tooltip("Namen der GameObjects die diese Zone auslösen (HandProxy, GrabTrigger, ...)")]
    public string[] triggerObjectNames = { "HandProxy", "GrabTrigger" };

    private void Start()
    {
        grabber = FindObjectOfType<SimpleGrabber>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Prüfe ob der Name des Objekts in der Liste ist
        bool isHandObject = System.Array.IndexOf(triggerObjectNames, other.name) >= 0;
        if (!isHandObject) return;

        // Prüfe ob der Grabber gerade einen Block hält
        if (grabber != null && grabber.HeldBlock != null)
        {
            grabber.HeldBlock.OnPassedThroughPartitionZone();
            Debug.Log("[PartitionZone] Hand mit Block erkannt – Zone passiert ?");
        }
    }
}


