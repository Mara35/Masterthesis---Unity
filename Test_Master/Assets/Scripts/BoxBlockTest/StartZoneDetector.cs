using UnityEngine;

public class StartZoneDetector : MonoBehaviour
{
    public bool IsInStartZone { get; private set; }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("StartZone"))
        {
            IsInStartZone = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("StartZone"))
        {
            IsInStartZone = false;
        }
    }
}