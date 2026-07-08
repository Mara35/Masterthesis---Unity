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
    [Tooltip("Zylinder auch WAEHREND des Greifens/Tragens aufrecht halten (verhindert Kippen am HoldPoint).")]
    public bool keepUprightWhileHeld = true;
    public string customCsvPath = "";

    [Header("--- Manueller Greif-Fallback ---")]
    [Tooltip("Taste zum manuellen Greifen/Loslassen, falls der Handschuh nicht ausloest.")]
    public KeyCode manualGrabKey = KeyCode.G;
    [Tooltip("HoldPoint der Hand - dorthin rastet der Zylinder (wie beim echten Greifen).")]
    public Transform holdPoint;
    [Tooltip("Optional: GloveGrabber - wird beim manuellen Greifen kurz stummgeschaltet, damit sich beide nicht streiten.")]
    public GloveGrabber gloveGrabber;

    [Header("--- Status (read-only) ---")]
    public int currentTarget = 0;
    public bool isFrozen = false;
    public int trialCounter = 0;

    private Vector3 frozenPos;
    private Quaternion frozenRot;
    private string csvPath;

    private bool manualHeld = false;
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
                "timestamp,trial,target,cyl_x,cyl_y,cyl_z,tgt_x,tgt_y,tgt_z,dx_mm,dy_mm,dz_mm,inplane_mm,height_mm\n");
        Debug.Log($"[Measurement] CSV: {csvPath}");
        Debug.Log("[Measurement] Fehlervorzeichen (Zylinder - Ziel): +dx=rechts, +dy=hoch, +dz=vom Koerper weg.");
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
        if (Input.GetKeyDown(manualGrabKey)) ToggleManualGrab();
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
        if (Input.GetKeyDown(KeyCode.LeftArrow))  SelectTarget((currentTarget - 1 + targets.Length) % targets.Length);
    }

    void SelectTarget(int i)
    {
        currentTarget = Mathf.Clamp(i, 0, targets.Length - 1);
        Debug.Log($"[Measurement] Aktuelles Ziel: {currentTarget + 1} ({targets[currentTarget].name})");
    }

    void LateUpdate()
    {
        if (cylinder == null) return;

        if (isFrozen)
        {
            // Eingefroren: Position + aufrechte Rotation pinnen (gewinnt gegen Grabber/Release).
            if (cylRb != null) { cylRb.isKinematic = true; cylRb.useGravity = false; }
            cylinder.SetParent(null);
            cylinder.position = frozenPos;
            cylinder.rotation = frozenRot;
            return;
        }

        // Nicht eingefroren, aber gegriffen (folgt dem HoldPoint): aufrecht erzwingen,
        // damit der Zylinder nicht um den Boden-Pivot kippt. Position bleibt unberuehrt.
        if (keepUprightWhileHeld && IsHeld())
            cylinder.rotation = startRot;
    }

    // "Gegriffen" = Zylinder haengt gerade an der Hand (per Glove ODER manuell).
    bool IsHeld()
    {
        if (manualHeld) return true;
        // Beim Greifen parentet BlockItem den Zylinder unter HoldPoint -> Parent != null & != urspruenglich.
        return cylinder.parent != null && holdPoint != null && cylinder.parent == holdPoint;
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

        // Fehlervektor = Zylinder - Ziel. In koerperbezogene Achsen umrechnen (relativ zu TargetRig,
        // falls gesetzt), damit +dx=rechts, +dy=hoch, +dz=vom Koerper weg gilt - unabhaengig davon,
        // wie die Szene global gedreht ist. InverseTransformDirection = nur Rotation, Betrag bleibt erhalten.
        Vector3 dWorld = c - t;
        Vector3 d = (targetParent != null) ? targetParent.InverseTransformDirection(dWorld) : dWorld;

        float inPlane = new Vector2(d.x, d.z).magnitude;  // horizontal (XZ)
        float height  = Mathf.Abs(d.y);                   // vertikal (Y)

        Debug.Log($"[Measurement] Trial {trialCounter} | Ziel {currentTarget + 1} ({targets[currentTarget].name}) " +
                  $"| dx {d.x*1000f:F1} dy {d.y*1000f:F1} dz {d.z*1000f:F1} mm | InPlane {inPlane*1000f:F1} mm");

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
        catch (Exception e) { Debug.LogError($"[Measurement] CSV-Schreibfehler: {e.Message}"); }
    }

    // Manueller Greif-Fallback: nutzt exakt denselben Weg wie der GloveGrabber
    // (BlockItem.Grab -> Snap auf HoldPoint). Nochmaliges Druecken laesst wieder los.
    void ToggleManualGrab()
    {
        if (cylinder == null) return;
        BlockItem block = cylinder.GetComponent<BlockItem>();
        if (block == null) { Debug.LogWarning("[Measurement] Zylinder hat kein BlockItem - manuelles Greifen nicht moeglich."); return; }

        if (!manualHeld)
        {
            isFrozen = false; // falls noch eingefroren -> freigeben
            Transform hp = holdPoint != null ? holdPoint : cylinder;
            block.Grab(hp);
            if (gloveGrabber != null) gloveGrabber.enabled = false; // Grabber kurz aus, damit er nicht dazwischenfunkt
            manualHeld = true;
            Debug.Log("[Measurement] Manuell gegriffen (Taste " + manualGrabKey + ").");
        }
        else
        {
            block.Release();
            if (gloveGrabber != null) gloveGrabber.enabled = true;
            manualHeld = false;
            Debug.Log("[Measurement] Manuell losgelassen (Taste " + manualGrabKey + ").");
        }
    }

    void ResetTrial()
    {
        isFrozen = false;
        manualHeld = false;
        if (gloveGrabber != null) gloveGrabber.enabled = true;
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
        GUILayout.Label($"G = manuell greifen/loslassen (Fallback) | manualHeld={manualHeld}");
        GUILayout.EndArea();
    }
}