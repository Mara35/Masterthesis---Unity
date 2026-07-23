using UnityEngine;

/// <summary>
/// Holds the selected competition DifficultyLevel (Basic..Full) and carries it from the explanation
/// screen into the competition scene.
/// </summary>
public enum DifficultyLevel
{
    Basic = 0,
    Motor = 1,
    Reaction = 2,
    Cognitive = 3,  // Focus: Peg Challenge (no Reaction, no Memory)
    Memory = 4,  // Focus: Working Memory (no Reaction, no Peg)
    Full = 5   // Everything enabled
}

public class DifficultyManager : MonoBehaviour
{
    public static DifficultyLevel SelectedLevel { get; set; } = DifficultyLevel.Basic;

    private static DifficultyManager instance;

    // Persistent singleton so the level chosen on the explanation screen survives the scene load.
    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Which extras each level unlocks. Freeze and Reaction are cumulative (from Motor / Reaction up),
    // while Peg and Sequence are exclusive to their focus level (Cognitive / Memory), both on in Full.
    public static bool HasFreeze => SelectedLevel >= DifficultyLevel.Motor;
    public static bool HasReaction => SelectedLevel >= DifficultyLevel.Reaction;
    public static bool HasPeg => SelectedLevel == DifficultyLevel.Cognitive
                                   || SelectedLevel == DifficultyLevel.Full;
    public static bool HasSequence => SelectedLevel == DifficultyLevel.Memory
                                   || SelectedLevel == DifficultyLevel.Full;
}