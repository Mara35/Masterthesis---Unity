using UnityEngine;

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