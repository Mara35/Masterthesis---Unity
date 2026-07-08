using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Mess-Controller fuer den End-to-End Accuracy Test.
/// Greift NICHT in den GloveGrabber ein - beobachtet nur den Zylinder und pinnt
/// ihn beim Messen fest (Execution Order 1000 -> nach dem Grabber).
/// </summary>
[DefaultExecutionOrder(1000)]
public class MeasurementController : MonoBehaviour
{
    [Header("--- Referenzen ---")]
    [Tooltip("Das GO mit BlockItem (rastet auf HoldPoint, wird geloggt). Pivot = Zylinderachse.")]
    public Transform cylinder;

    [Tooltip("Ziel-GOs. Reihenfolge = Ziel 1..N. Wird von targetParent ueberschrieben, falls gesetzt.")]
    public Transform[] targets;

    [Tooltip("Optional: TargetRig hier reinziehen -> T1..T11 werden automatisch gesammelt (Cube wird ignoriert).")]
    public Transform targetParent;

    [Header("--- Tasten ---")]
    public KeyCode logKey = KeyCode.Space;
    public KeyCode resetKey = KeyCode.R;
    // Zielwahl: 1..9 direkt, 0 = Ziel 10, - (Minus) = Ziel 11
    // ausserdem Pfeil links/rechts = vorheriges/naechstes Ziel (skaliert auf beliebig viele)

    [Header("--- Optionen ---")]
    [Tooltip("Zylinder beim Freeze aufrecht stellen. Rein optisch/QA - aendert die geloggte Position NICHT.")]
    public bool forceUpright = true;
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
        // Optional: T1..T11 automatisch aus dem Parent sammeln (Cube & Fremdes werden ignoriert)
        if (targetParent != null)
            targets = CollectNumberedChildren(targetParent, 'T');

        csvPath = string.IsNullOrEmpty(customCsvPath)
            ? Path.Combine(Application.persistentDataPath, "placement_results.csv")
            : customCsvPath;

        if (!File.Exists(csvPath))
            File.AppendAllText(csvPath,
                "timestamp,trial,target,cyl_x,cyl_y,cyl_z,tgt_x,tgt_y,tgt_z,inplane_mm,height_mm\n");
        Debug.Log($"[Measurement] CSV: {csvPath}");
        Debug.Log($"[Measurement] {(targets != null ? targets.Length : 0)} Ziele geladen.");

        if (cylinder != null)
        {
            cylRb = cylinder.GetComponent<Rigidbody>();
            startPos = cylinder.position;
            startRot = cylinder.rotation;
        }
        else Debug.LogWarning("[Measurement] Kein Zylinder zugewiesen!");

        if (targets == null || targets.Length == 0)
            Debug.LogWarning("[Measurement] Keine Targets!");
    }

    // Sammelt Kinder namens <prefix><Zahl> (z.B. T1..T11), sortiert nach der Zahl.
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

        // 1..9 -> Ziel 1..9
        for (int i = 0; i < 9 && i < targets.Length; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) SelectTarget(i);

        // 0 -> Ziel 10, Minus -> Ziel 11
        if (targets.Length >= 10 && Input.GetKeyDown(KeyCode.Alpha0)) SelectTarget(9);
        if (targets.Length >= 11 && Input.GetKeyDown(KeyCode.Minus)) SelectTarget(10);

        // Pfeiltasten: durchschalten (funktioniert fuer beliebig viele Ziele)
        if (Input.GetKeyDown(KeyCode.RightArrow)) SelectTarget((currentTarget + 1) % targets.Length);
        if (Input.GetKeyDown(KeyCode.LeftArrow)) SelectTarget((currentTarget - 1 + targets.Length) % targets.Length);
    }

    void SelectTarget(int i)
    {
        currentTarget = Mathf.Clamp(i, 0, targets.Length - 1);
        Debug.Log($"[Measurement] Aktuelles Ziel: {currentTarget + 1} ({targets[currentTarget].name})");
    }

    void LateUpdate()
    {
        if (isFrozen && cylinder != null)
        {
            if (cylRb != null) { cylRb.isKinematic = true; cylRb.useGravity = false; }
            cylinder.SetParent(null);
            cylinder.position = frozenPos;
            cylinder.rotation = frozenRot;
        }
    }

    void CaptureAndLog()
    {
        if (cylinder == null || targets == null || currentTarget >= targets.Length || targets[currentTarget] == null)
        {
            Debug.LogWarning("[Measurement] Referenzen unvollstaendig - nichts geloggt.");
            return;
        }

        frozenPos = cylinder.position;
        frozenRot = forceUpright ? startRot : cylinder.rotation;
        isFrozen = true;
        trialCounter++;

        Vector3 c = frozenPos;
        Vector3 t = targets[currentTarget].position;

        float inPlane = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(t.x, t.z));
        float height = Mathf.Abs(c.y - t.y);

        Debug.Log($"[Measurement] Trial {trialCounter} | Ziel {currentTarget + 1} ({targets[currentTarget].name}) " +
                  $"| InPlane {inPlane * 1000f:F1} mm | Hoehe {height * 1000f:F1} mm");

        WriteCsvLine(c, t, inPlane, height);
    }

    void WriteCsvLine(Vector3 c, Vector3 t, float inPlane, float height)
    {
        var ci = CultureInfo.InvariantCulture;
        string line = string.Join(",", new[]
        {
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", ci),
            trialCounter.ToString(ci),
            (currentTarget + 1).ToString(ci),
            c.x.ToString("F5", ci), c.y.ToString("F5", ci), c.z.ToString("F5", ci),
            t.x.ToString("F5", ci), t.y.ToString("F5", ci), t.z.ToString("F5", ci),
            (inPlane * 1000f).ToString("F2", ci),
            (height * 1000f).ToString("F2", ci)
        }) + "\n";

        try { File.AppendAllText(csvPath, line); }
        catch (Exception e) { Debug.LogError($"[Measurement] CSV-Schreibfehler: {e.Message}"); }
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
        Debug.Log("[Measurement] Freigegeben - Zylinder zurueck auf A.");
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 270, 380, 90));
        string tname = (targets != null && currentTarget < targets.Length && targets[currentTarget] != null)
            ? targets[currentTarget].name : "-";
        GUILayout.Label($"[Measurement] Ziel={currentTarget + 1} ({tname}) | Trial={trialCounter} | Frozen={isFrozen}");
        GUILayout.Label("Ziel: 1-9, 0=10, -=11, Pfeile blaettern | Space=log+freeze | R=reset");
        GUILayout.EndArea();
    }
}