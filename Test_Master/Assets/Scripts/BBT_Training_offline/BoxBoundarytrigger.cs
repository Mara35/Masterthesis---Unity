using UnityEngine;

/// <summary>
/// Unsichtbare Trigger-Zone über dem Box-Rand.
/// Startet den Timer sobald die Hand (HandTarget) die Box betritt —
/// egal von welcher Seite.
///
/// SETUP:
///   1. Leeres GO "BoxBoundaryTrigger" erstellen
///   2. Box Collider hinzufügen ? Is Trigger = true
///   3. Collider so einstellen dass er die gesamte Box-Öffnung abdeckt
///      (etwas größer als die Box, Y-Position auf Rand-Höhe)
///   4. Dieses Script drauflegen
///   5. TestTimer zuweisen
/// </summary>
public class BoxBoundaryTrigger : MonoBehaviour
{
    [Tooltip("TestTimer zuweisen")]
    public Timer testTimer;

    [Tooltip("Name des Hand-Objekts")]
    public string handObjectName = "HandTarget";

    [Tooltip("Nur einmal triggern?")]
    public bool triggerOnce = true;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && hasTriggered) return;
        if (other.name != handObjectName) return;

        hasTriggered = true;
        Debug.Log($"[BoxBoundaryTrigger] Hand '{other.name}' hat Box betreten ? Timer startet!");

        if (testTimer != null)
            testTimer.StartTimer();
        else
            Debug.LogWarning("[BoxBoundaryTrigger] TestTimer nicht zugewiesen!");
    }

    public void Reset()
    {
        hasTriggered = false;
    }

    // Collider im Editor sichtbar machen
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