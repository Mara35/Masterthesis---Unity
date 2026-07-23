using UnityEngine;
using TMPro;

/// <summary>
/// Counts the cubes that have reached one side of the board (RightSide = player, LeftSide = ghost),
/// polling at updateInterval, and mirrors the score to the PC and VR labels. GetScore returns the total.
/// </summary>

public class CompetitionScoreCounter : MonoBehaviour
{
    public enum Side { RightSide, LeftSide }

    [Header("Which side does this script count?")]
    [Tooltip("RightSide = Player (StartZone)\nLeftSide = Ghost (TargetZone)")]
    public Side countSide = Side.RightSide;

    [Header("UI")]
    public TextMeshProUGUI scoreText;
    public string prefix = "Score: ";

    [Header("VR UI (World Space)")]
    [Tooltip("Score text on Canvas_VR")]
    public TextMeshProUGUI vrScoreText;

    [Header("Cube")]
    [Tooltip("Tag of Cubes (e.g. Block), or leave blank to search by name")]
    public string cubeTag = "Block";

    [Header("Update rate")]
    [Tooltip("How many times per second the count is performed (performance)")]
    public float updateInterval = 0.1f;

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private float partitionX = 0f;
    private float nextUpdate = 0f;
    private int currentScore = 0;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        GameObject cp = GameObject.Find("CenterPartition");
        if (cp != null)
            partitionX = cp.transform.position.x;
        else
            Debug.LogWarning("[CompetitionScoreCounter] CenterPartition not found!");

        // Wait one frame until all GameObjects have been initialized
        StartCoroutine(InitialCount());
    }

    private System.Collections.IEnumerator InitialCount()
    {
        yield return null; // wait one frame
        CountAndUpdate();
    }

    private void Update()
    {
        if (Time.time < nextUpdate) return;
        nextUpdate = Time.time + updateInterval;
        CountAndUpdate();
    }

    // -----------------------------------------------------------------------
    // Count
    // -----------------------------------------------------------------------
    // Counts cubes purely by X position relative to the partition (a cube "belongs" to the side
    // it currently sits on), so cubes carried over by either orb are counted correctly.

    private void CountAndUpdate()
    {
        int count = 0;

        GameObject[] allCubes = GetAllCubes();
        foreach (GameObject cube in allCubes)
        {
            if (!cube.activeInHierarchy) continue;

            float x = cube.transform.position.x;
            bool onMySide = (countSide == Side.RightSide && x > partitionX)
                         || (countSide == Side.LeftSide && x < partitionX);

            if (!onMySide) continue;

            // BonusCube count for their point value; regular cubes count as 1
            BonusCube bonus = cube.GetComponent<BonusCube>();
            count += (bonus != null) ? bonus.pointValue : 1;
        }

        if (count != currentScore)
        {
            currentScore = count;
            UpdateUI();
        }
    }

    private GameObject[] GetAllCubes()
    {
        try
        {
            if (!string.IsNullOrEmpty(cubeTag))
                return GameObject.FindGameObjectsWithTag(cubeTag);
        }
        catch { }

        // Fallback: all GameObjects that start with "Block"
        var list = new System.Collections.Generic.List<GameObject>();
        foreach (GameObject go in FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("Block")) list.Add(go);
        return list.ToArray();
    }

    private void UpdateUI()
    {
        string label = prefix + currentScore;
        if (scoreText != null) scoreText.text = label;
        if (vrScoreText != null) vrScoreText.text = label;
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public int GetScore() => currentScore;
}