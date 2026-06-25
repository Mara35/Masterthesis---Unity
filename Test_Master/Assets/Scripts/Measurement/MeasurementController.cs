using System;
using System.IO;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Mess-Controller fuer den End-to-End Accuracy Test.
///
/// Ablauf:
///   - GloveGrabber/BlockItem uebernehmen Greifen + Tragen (unveraendert, wie im Spiel).
///   - Operator markiert den Mess-Instant per Taste, sobald der reale Zylinder
///     auf dem Nagel sitzt -> Position wird geloggt und der Zylinder eingefroren.
///   - Reset gibt das naechste Trial frei und setzt den virtuellen Zylinder zurueck auf A.
///
/// Das Skript greift NICHT in den GloveGrabber ein. Es beobachtet nur den
/// Zylinder und pinnt ihn beim Messen fest (laeuft via Execution Order NACH dem Grabber).
/// </summary>
[DefaultExecutionOrder(1000)] // laeuft NACH dem GloveGrabber, damit der Freeze-Pin gewinnt
public class MeasurementController : MonoBehaviour
{
    [Header("--- Referenzen ---")]
    [Tooltip("Das GO mit BlockItem (rastet auf HoldPoint, wird geloggt). Pivot = Zylinderachse.")]
    public Transform cylinder;
    [Tooltip("Die 5 leeren Ziel-GOs. Reihenfolge = Layout (Index 0 = Ziel 1 usw.).")]
    public Transform[] targets;

    [Header("--- Tasten ---")]
    [Tooltip("Mess-Schnappschuss + Freeze (Zylinder sitzt auf dem Nagel).")]
    public KeyCode logKey = KeyCode.Space;
    [Tooltip("Naechstes Trial freigeben, virtuellen Zylinder zurueck auf A.")]
    public KeyCode resetKey = KeyCode.R;
    // Zielwahl ueber Zifferntasten 1..5 (bis 9 unterstuetzt)

    [Header("--- Optionen ---")]
    [Tooltip("Eigenen CSV-Pfad erzwingen. Leer = Application.persistentDataPath.")]
    public string customCsvPath = "";

    [Header("--- Status (read-only) ---")]
    public int currentTarget = 0;
    public bool isFrozen = false;
    public int trialCounter = 0;

    private Vector3 frozenPos;
    private string csvPath;

    private Rigidbody cylRb;
    private Vector3 startPos;
    private Quaternion startRot;

    void Start()
    {
        // CSV vorbereiten
        csvPath = string.IsNullOrEmpty(customCsvPath)
            ? Path.Combine(Application.persistentDataPath, "placement_results.csv")
            : customCsvPath;

        if (!File.Exists(csvPath))
        {
            File.AppendAllText(csvPath,
                "timestamp,trial,target,cyl_x,cyl_y,cyl_z,tgt_x,tgt_y,tgt_z,inplane_mm,height_mm\n");
        }
        Debug.Log($"[Measurement] CSV: {csvPath}");

        // Zylinder-Ausgangszustand merken (= virtuelles A)
        if (cylinder != null)
        {
            cylRb = cylinder.GetComponent<Rigidbody>();
            startPos = cylinder.position;
            startRot = cylinder.rotation;
        }
        else
        {
            Debug.LogWarning("[Measurement] Kein Zylinder zugewiesen!");
        }

        if (targets == null || targets.Length == 0)
            Debug.LogWarning("[Measurement] Keine Targets zugewiesen!");
    }

    void Update()
    {
        // Zielwahl 1..9
        if (targets != null)
        {
            for (int i = 0; i < targets.Length && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    currentTarget = i;
                    Debug.Log($"[Measurement] Aktuelles Ziel: {i + 1}");
                }
            }
        }

        if (Input.GetKeyDown(logKey)) CaptureAndLog();
        if (Input.GetKeyDown(resetKey)) ResetTrial();
    }

    void LateUpdate()
    {
        // Solange eingefroren: gegen Grabber/Release pinnen.
        // Laeuft dank Execution Order (1000) nach dem GloveGrabber.
        if (isFrozen && cylinder != null)
        {
            if (cylRb != null) { cylRb.isKinematic = true; cylRb.useGravity = false; }
            cylinder.SetParent(null);
            cylinder.position = frozenPos;
        }
    }

    void CaptureAndLog()
    {
        if (cylinder == null || targets == null || currentTarget >= targets.Length || targets[currentTarget] == null)
        {
            Debug.LogWarning("[Measurement] Referenzen unvollstaendig - nichts geloggt.");
            return;
        }

        // Mess-Instant = genau jetzt
        frozenPos = cylinder.position;
        isFrozen = true;
        trialCounter++;

        Vector3 c = frozenPos;
        Vector3 t = targets[currentTarget].position;

        // In-Plane-Fehler (horizontal, XZ) = Hauptmetrik; Hoehe (Y) = Sekundaerwert
        float inPlane = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(t.x, t.z));
        float height = Mathf.Abs(c.y - t.y);

        Debug.Log($"[Measurement] Trial {trialCounter} | Ziel {currentTarget + 1} " +
                  $"| InPlane {inPlane * 1000f:F1} mm | Hoehe {height * 1000f:F1} mm");

        WriteCsvLine(c, t, inPlane, height);
    }

    void WriteCsvLine(Vector3 c, Vector3 t, float inPlane, float height)
    {
        // InvariantCulture erzwingt Punkt als Dezimaltrenner (deutsches Windows schreibt sonst Komma!)
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

        try
        {
            File.AppendAllText(csvPath, line);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Measurement] CSV-Schreibfehler: {e.Message}");
        }
    }

    void ResetTrial()
    {
        isFrozen = false;

        // Virtuellen Zylinder zurueck auf A
        if (cylinder != null)
        {
            cylinder.SetParent(null);
            cylinder.position = startPos;
            cylinder.rotation = startRot;
            if (cylRb != null)
            {
                cylRb.isKinematic = true;
                cylRb.useGravity = false;
                cylRb.velocity = Vector3.zero;
                cylRb.angularVelocity = Vector3.zero;
            }
        }

        Debug.Log("[Measurement] Freigegeben - Zylinder zurueck auf A, bereit fuers naechste Trial.");
    }

    // Optionale On-Screen-Anzeige fuer den Operator
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 270, 360, 90));
        GUILayout.Label($"[Measurement] Ziel={currentTarget + 1} | Trial={trialCounter} | Frozen={isFrozen}");
        GUILayout.Label("Tasten: 1-5 Ziel | Space loggen+freeze | R reset");
        GUILayout.EndArea();
    }
}