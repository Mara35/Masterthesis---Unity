using UnityEngine;

public class PlacementLogger : MonoBehaviour
{
    public Transform cylinder;        // dein Zylinder (bzw. das Achs-Parent-GO)
    public Transform[] targets;       // die 5 leeren Ziel-GOs hier reinziehen

    // beim Release aufrufen, z. B. targetIndex = aktuelles Ziel
    public void LogPlacement(int targetIndex)
    {
        Vector3 c = cylinder.position;
        Vector3 t = targets[targetIndex].position;

        // In-Plane-Fehler (horizontal): Y ignorieren
        Vector2 cFlat = new Vector2(c.x, c.z);
        Vector2 tFlat = new Vector2(t.x, t.z);
        float inPlane = Vector2.Distance(cFlat, tFlat);

        float height = Mathf.Abs(c.y - t.y);   // Sekundärwert

        Debug.Log($"Target {targetIndex} | InPlane: {inPlane * 1000f:F1} mm | Height: {height * 1000f:F1} mm");
    }
}