using UnityEngine;

/// <summary>
/// Temporäres Debug-Script auf HandTarget legen.
/// Zeigt alle relevanten Grab-Informationen in Console und GUI.
/// Nach dem Debuggen löschen.
/// </summary>
public class GrabDebugHelper : MonoBehaviour
{
    public GloveGrabber gloveGrabber;
    public SphereCollider grabTrigger;

    private int frameCount = 0;

    void Update()
    {
        frameCount++;
        if (frameCount % 40 != 0) return; // Alle 2 Sekunden

        if (gloveGrabber == null) { Debug.LogError("[GrabDebug] GloveGrabber nicht zugewiesen!"); return; }

        Debug.Log($"[GrabDebug] t={Time.time:F1}s | " +
                  $"IndexMCP={gloveGrabber.currentIndexMcp:F1}° " +
                  $"IndexPIP={gloveGrabber.currentIndexPip:F1}° | " +
                  $"Gripping={gloveGrabber.IsGripping} | " +
                  $"HeldBlock={gloveGrabber.HeldBlock?.name ?? "–"}");

        if (grabTrigger != null)
        {
            // Prüfe ob Würfel im Trigger-Radius liegen
            Collider[] nearby = Physics.OverlapSphere(
                transform.position, grabTrigger.radius);
            int blockCount = 0;
            foreach (var c in nearby)
                if (c.GetComponent<BlockItem>() != null) blockCount++;

            Debug.Log($"[GrabDebug] HandTarget Pos={transform.position} | " +
                      $"Trigger Radius={grabTrigger.radius} | " +
                      $"Würfel in Radius: {blockCount}/{nearby.Length} Collider");
        }
    }

    void OnGUI()
    {
        if (gloveGrabber == null) return;
        GUILayout.BeginArea(new Rect(10, 280, 320, 100));
        GUILayout.Label($"[GrabDebug] HandTarget: {transform.position:F3}");
        GUILayout.Label($"IndexMCP={gloveGrabber.currentIndexMcp:F1}° (Threshold: -40°)");
        GUILayout.Label($"IndexPIP={gloveGrabber.currentIndexPip:F1}° (Threshold: -50°)");
        GUILayout.Label($"Gripping={gloveGrabber.IsGripping} | Block={gloveGrabber.HeldBlock?.name ?? "–"}");
        GUILayout.EndArea();
    }
}