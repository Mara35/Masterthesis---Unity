using UnityEngine;

public class PegChallengeCube : MonoBehaviour
{
    [Tooltip("Farb-ID – muss mit der Zone übereinstimmen (0=Rot, 1=Blau, 2=Gelb)")]
    public int colorId = 0;

    public bool IsPlaced { get; set; } = false;

    // Farben für die drei Zylinder
    public static readonly Color[] PegColors = {
        new Color(0.9f, 0.2f, 0.2f), // Rot
        new Color(0.2f, 0.4f, 0.9f), // Blau
        new Color(0.9f, 0.8f, 0.1f)  // Gelb
    };

    private void Start()
    {
        // Farbe setzen
        Renderer r = GetComponent<Renderer>();
        if (r != null && colorId < PegColors.Length)
            r.material.color = PegColors[colorId];
    }
}