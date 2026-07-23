using UnityEngine;

/// <summary>
/// LEGACY. Detects whether a block sits in the start zone, for the old prototype grab flow.
/// </summary>
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