using UnityEngine;

public enum DifficultyLevel
{
    Basic = 0,
    Motor = 1,
    Reaction = 2,
    Cognitive = 3,  // Fokus: Peg (kein Sequence)
    Sequential = 4,  // Fokus: Sequence (kein Peg)
    Full = 5   // Alles + Ghost schneller
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
    public static bool HasSequence => SelectedLevel == DifficultyLevel.Sequential
                                   || SelectedLevel == DifficultyLevel.Full;
}