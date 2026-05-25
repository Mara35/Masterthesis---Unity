using UnityEngine;

/// <summary>
/// Invisible trigger zone above the edge of the box.
/// Starts the timer as soon as the hand (HandTarget) enters the box —
/// regardless of which side.
/// </summary>
public class BoxBoundaryTrigger : MonoBehaviour
{
    [Tooltip("Assign TestTimer")]
    public Timer testTimer;

    [Tooltip("Name of the Hand object")]
    public string handObjectName = "HandTarget";

    [Tooltip("Trigger only once?")]
    public bool triggerOnce = true;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && hasTriggered) return;
        if (other.name != handObjectName) return;

        hasTriggered = true;
        Debug.Log($"Has the hand ‘{other.name}’ entered the box? The timer starts!");

        if (testTimer != null)
            testTimer.StartTimer();
        else
            Debug.LogWarning("[BoxBoundaryTrigger] TestTimer not assigned!");
    }

    public void Reset()
    {
        hasTriggered = false;
    }

    // Make colliders visible in the editor
    private void OnDrawGizmos()
    {
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc == null) return;
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(bc.center, bc.size);
        Gizmos.color = new Color(0, 1, 0, 0.8f);
        Gizmos.DrawWireCube(bc.center, bc.size);
    }
}