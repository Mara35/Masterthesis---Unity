using UnityEngine;

/// <summary>A colored peg for the peg challenge. colorId must match its target PegChallengeZone (0=Red, 1=Blue, 2=Yellow).</summary>
public class PegChallengeCube : MonoBehaviour
{
    [Tooltip("Color ID, must match the zone (0=Red, 1=Blue, 2=Yellow)")]
    public int colorId = 0;

    public bool IsPlaced { get; set; } = false;

    // Colors for the three cylinders
    public static readonly Color[] PegColors = {
        new Color(0.9f, 0.2f, 0.2f), // red
        new Color(0.2f, 0.4f, 0.9f), // blue
        new Color(0.9f, 0.8f, 0.1f)  // yellow
    };

    private void Start()
    {
        // set color based on colorId
        Renderer r = GetComponent<Renderer>();
        if (r != null && colorId < PegColors.Length)
            r.material.color = PegColors[colorId];
    }
}