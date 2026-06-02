using UnityEngine;

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

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static bool HasFreeze => SelectedLevel >= DifficultyLevel.Motor;
    public static bool HasReaction => SelectedLevel >= DifficultyLevel.Reaction;
    public static bool HasPeg => SelectedLevel == DifficultyLevel.Cognitive
                                   || SelectedLevel == DifficultyLevel.Full;
    public static bool HasSequence => SelectedLevel == DifficultyLevel.Memory
                                   || SelectedLevel == DifficultyLevel.Full;
}