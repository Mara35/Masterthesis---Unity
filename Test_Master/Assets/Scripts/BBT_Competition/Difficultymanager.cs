/*
 * Project:    SensinGlove – Box & Block Rehab Game
 * File:       DifficultyManager.cs
 * Author:     Mari und Kiki (MCI – University of Applied Sciences)
 * Supervisor: Simon Winkler, BSc MSc
 * Year:       2025
 *
 * Singleton – überlebt Scenenwechsel (DontDestroyOnLoad)
 * Speichert das gewählte Level und wird in BBT_Competition ausgelesen.
 *
 * Verwendung in BBT_Competition:
 *   var level = DifficultyManager.SelectedLevel;
 *   if (level >= DifficultyLevel.Motor) { // Freeze aktiv }
 */

using UnityEngine;

public enum DifficultyLevel
{
    Basic = 0,
    Motor = 1,
    Reaction = 2,
    Cognitive = 3,
    Sequential = 4,
    Full = 5
}

public class DifficultyManager : MonoBehaviour
{
    public static DifficultyLevel SelectedLevel { get; set; } = DifficultyLevel.Basic;

    private static DifficultyManager instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // -----------------------------------------------------------------------
    // Helper: welche Features sind aktiv?
    // -----------------------------------------------------------------------

    public static bool HasFreeze => SelectedLevel >= DifficultyLevel.Motor;
    public static bool HasReaction => SelectedLevel >= DifficultyLevel.Reaction;
    public static bool HasPeg => SelectedLevel >= DifficultyLevel.Cognitive;
    public static bool HasSequence => SelectedLevel >= DifficultyLevel.Sequential;
    public static bool HasAll => SelectedLevel == DifficultyLevel.Full;
}