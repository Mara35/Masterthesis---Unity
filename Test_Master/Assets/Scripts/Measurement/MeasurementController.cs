using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Measurement controller for the end-to-end accuracy test.
/// Purely observational, it never grabs or moves the cylinder itself. It watches the cylinder
/// while the glove carries it, and on the log key freezes it in place and records the placement
/// error against the currently selected target (execution order 1000 -> runs after the grabber).
/// </summary>
[DefaultExecutionOrder(1000)]
public class MeasurementController : MonoBehaviour
{
    [Header("--- References ---")]
    [Tooltip("The GO with BlockItem (snaps onto HoldPoint, gets logged). Pivot = cylinder axis.")]
    public Transform cylinder;

    [Tooltip("The hand's HoldPoint. Used to detect whether the glove is currently carrying the cylinder.")]
    public Transform holdPoint;

    [Tooltip("Target GOs. Order = target 1..N. Overridden by targetParent if that is set.")]
    public Transform[] targets;

    [Tooltip("Optional: drag TargetRig here -> T1..T11 are collected automatically (Cube is ignored).")]
    public Transform targetParent;

    [Header("--- Keys ---")]
    public KeyCode logKey = KeyCode.Space;
    public KeyCode resetKey = KeyCode.R;
    // Target selection: 1..9 directly, 0 = target 10, - (minus) = target 11
    // Also left/right arrow = previous/next target (scales to any number of targets)

    [Header("--- Options ---")]
    [Tooltip("Stand the cylinder upright on freeze. Purely visual/QA - does NOT change the logged position.")]
    public bool forceUpright = true;
    [Tooltip("Keep the cylinder upright WHILE grabbed/carried too (prevents tipping at the HoldPoint).")]
    public bool keepUprightWhileHeld = true;
    public string customCsvPath = "";

    [Header("--- Status (read-only) ---")]
    public int currentTarget = 0;
    public bool isFrozen = false;
    public int trialCounter = 0;

    private Vector3 frozenPos;
    private Quaternion frozenRot;
    private string csvPath;

    private Rigidbody cylRb;
    private Vector3 startPos;
    private Quaternion startRot;

    void Start()
    {
        // Optional: collect T1..T11 automatically from the parent (Cube and anything else is ignored)
        if (targetParent != null)
            targets = CollectNumberedChildren(targetParent, 'T');

        csvPath = string.IsNullOrEmpty(customCsvPath)
            ? Path.Combine(Application.persistentDataPath, "placement_results.csv")
            : customCsvPath;

        if (!File.Exists(csvPath))
            File.AppendAllText(csvPath,
                "timestamp,trial,target,cyl_x,cyl_y,cyl_z,tgt_x,tgt_y,tgt_z,dx_mm,dy_mm,dz_mm,inplane_mm,height_mm\n");
        Debug.Log($"[Measurement] CSV: {csvPath}");
        Debug.Log("[Measurement] Error sign (cylinder - target): +dx=right, +dy=up, +dz=away from body.");
        Debug.Log($"[Measurement] {(targets != null ? targets.Length : 0)} targets loaded.");

        if (cylinder != null)
        {
            cylRb = cylinder.GetComponent<Rigidbody>();
            startPos = cylinder.position;
            startRot = cylinder.rotation;
        }
        else Debug.LogWarning("[Measurement] No cylinder assigned!");

        if (targets == null || targets.Length == 0)
            Debug.LogWarning("[Measurement] No targets!");
    }

    // Collects children named <prefix><number> (e.g. T1..T11), sorted by the number.
    private Transform[] CollectNumberedChildren(Transform parent, char prefix)
    {
        var found = new List<KeyValuePair<int, Transform>>();
        foreach (Transform child in parent)
        {
            int n = ParseIndex(child.name, prefix);
            if (n > 0) found.Add(new KeyValuePair<int, Transform>(n, child));
        }
        found.Sort((a, b) => a.Key.CompareTo(b.Key));
        var arr = new Transform[found.Count];
        for (int i = 0; i < found.Count; i++) arr[i] = found[i].Value;
        return arr;
    }

    private int ParseIndex(string name, char prefix)
    {
        if (string.IsNullOrEmpty(name) || name[0] != prefix) return -1;
        return int.TryParse(name.Substring(1), out int n) ? n : -1;
    }

    void Update()
    {
        HandleTargetSelection();
        if (Input.GetKeyDown(logKey)) CaptureAndLog();
        if (Input.GetKeyDown(resetKey)) ResetTrial();
    }

    void HandleTargetSelection()
    {
        if (targets == null || targets.Length == 0) return;

        // 1..9 -> target 1..9
        for (int i = 0; i < 9 && i < targets.Length; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) SelectTarget(i);

        // 0 -> target 10, minus -> target 11
        if (targets.Length >= 10 && Input.GetKeyDown(KeyCode.Alpha0)) SelectTarget(9);
        if (targets.Length >= 11 && Input.GetKeyDown(KeyCode.Minus)) SelectTarget(10);

        // Arrow keys: cycle through (works for any number of targets)
        if (Input.GetKeyDown(KeyCode.RightArrow)) SelectTarget((currentTarget + 1) % targets.Length);
        if (Input.GetKeyDown(KeyCode.LeftArrow)) SelectTarget((currentTarget - 1 + targets.Length) % targets.Length);
    }

    void SelectTarget(int i)
    {
        currentTarget = Mathf.Clamp(i, 0, targets.Length - 1);
        Debug.Log($"[Measurement] Current target: {currentTarget + 1} ({targets[currentTarget].name})");
    }

    void LateUpdate()
    {
        if (cylinder == null) return;

        if (isFrozen)
        {
            // Frozen: pin position + upright rotation (wins over grabber/release).
            if (cylRb != null) { cylRb.isKinematic = true; cylRb.useGravity = false; }
            cylinder.SetParent(null);
            cylinder.position = frozenPos;
            cylinder.rotation = frozenRot;
            return;
        }

        // Not frozen but carried by the glove: force upright so the cylinder does not tip
        // around its bottom pivot. Position is left untouched.
        if (keepUprightWhileHeld && IsHeld())
            cylinder.rotation = startRot;
    }

    // "Held" = the glove is currently carrying the cylinder. On grab, BlockItem parents it under
    // HoldPoint, so parent == holdPoint means it is being carried.
    bool IsHeld()
    {
        if (cylinder == null) return false;
        return cylinder.parent != null && holdPoint != null && cylinder.parent == holdPoint;
    }

    void CaptureAndLog()
    {
        if (cylinder == null || targets == null || currentTarget >= targets.Length || targets[currentTarget] == null)
        {
            Debug.LogWarning("[Measurement] References incomplete - nothing logged.");
            return;
        }

        frozenPos = cylinder.position;
        frozenRot = forceUpright ? startRot : cylinder.rotation;
        isFrozen = true;
        trialCounter++;

        Vector3 c = frozenPos;
        Vector3 t = targets[currentTarget].position;

        // Error vector = cylinder, target. Convert into body-relative axes (relative to TargetRig,
        // if set) so that +dx=right, +dy=up, +dz=away from body holds, regardless of how the scene
        // is globally rotated. InverseTransformDirection = rotation only, magnitude is preserved.
        Vector3 dWorld = c - t;
        Vector3 d = (targetParent != null) ? targetParent.InverseTransformDirection(dWorld) : dWorld;

        float inPlane = new Vector2(d.x, d.z).magnitude;  // horizontal (XZ)
        float height = Mathf.Abs(d.y);                   // vertical (Y)

        Debug.Log($"[Measurement] Trial {trialCounter} | target {currentTarget + 1} ({targets[currentTarget].name}) " +
                  $"| dx {d.x * 1000f:F1} dy {d.y * 1000f:F1} dz {d.z * 1000f:F1} mm | InPlane {inPlane * 1000f:F1} mm");

        WriteCsvLine(c, t, d, inPlane, height);
    }

    void WriteCsvLine(Vector3 c, Vector3 t, Vector3 d, float inPlane, float height)
    {
        var ci = CultureInfo.InvariantCulture;
        string line = string.Join(",", new[]
        {
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", ci),
            trialCounter.ToString(ci),
            (currentTarget + 1).ToString(ci),
            c.x.ToString("F5", ci), c.y.ToString("F5", ci), c.z.ToString("F5", ci),
            t.x.ToString("F5", ci), t.y.ToString("F5", ci), t.z.ToString("F5", ci),
            (d.x * 1000f).ToString("F2", ci), (d.y * 1000f).ToString("F2", ci), (d.z * 1000f).ToString("F2", ci),
            (inPlane * 1000f).ToString("F2", ci),
            (height * 1000f).ToString("F2", ci)
        }) + "\n";

        try { File.AppendAllText(csvPath, line); }
        catch (Exception e) { Debug.LogError($"[Measurement] CSV write error: {e.Message}"); }
    }

    void ResetTrial()
    {
        isFrozen = false;
        if (cylinder != null)
        {
            cylinder.SetParent(null);
            cylinder.position = startPos;
            cylinder.rotation = startRot;
            if (cylRb != null)
            {
                cylRb.isKinematic = true; cylRb.useGravity = false;
                cylRb.velocity = Vector3.zero; cylRb.angularVelocity = Vector3.zero;
            }
        }
        Debug.Log("[Measurement] Released - cylinder back to A.");
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 270, 380, 70));
        string tname = (targets != null && currentTarget < targets.Length && targets[currentTarget] != null)
            ? targets[currentTarget].name : "-";
        GUILayout.Label($"[Measurement] target={currentTarget + 1} ({tname}) | Trial={trialCounter} | Frozen={isFrozen}");
        GUILayout.Label("Target: 1-9, 0=10, -=11, arrows to page | Space=log+freeze | R=reset");
        GUILayout.EndArea();
    }
}