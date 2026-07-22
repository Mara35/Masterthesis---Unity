using UnityEngine;

/// <summary>
/// Grip-difficulty settings for one level (finger-angle thresholds, minimum fingers, release
/// hysteresis). <see cref="Selected"/> is a static slot the menu writes the chosen level into so
/// the gameplay scene (GloveGrabber) can read it after the scene load.
/// </summary>

[System.Serializable]
public class LevelConfig
{
    public string levelName;
    public float gripMcpThreshold;
    public float gripPipThreshold;
    public int minFingersForGrip;
    public float releaseHysteresis;

    public static LevelConfig Selected { get; set; }
}