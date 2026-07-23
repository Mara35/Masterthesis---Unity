using UnityEngine;

/// <summary>A collectible cube worth points. Positive pointValue = bonus, negative = penalty (red).</summary>

public class BonusCube : MonoBehaviour
{
    [Tooltip("How many points is the cube worth?")]
    public int pointValue = 5;
}

