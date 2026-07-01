using System;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Roh-Bewegungs-Logger fuer die Koerpergroessen-Replay-Simulation.
///
/// Schreibt pro Frame die Welt-Positionen der Armkette (Schulter, Ellbogen,
/// Handgelenk, Greifpunkt) in eine zweite CSV - getaggt mit Trial-Segment,
/// aktuellem Ziel und einem Marker fuer den Mess-Instant (Leertaste).
///
/// Damit laesst sich offline die Armlaenge variieren:
///   Hand' = Schulter + L1'*dir(Schulter->Ellbogen) + L2'*dir(Ellbogen->Handgelenk)
/// Die Richtungen sind laengenunabhaengig -> reine Positionsdaten genuegen.
///
/// Optional werden zusaetzlich die konvertierten IMU-Quaternionen mitgeschrieben.
/// Laeuft NACH MeasurementController (Order 1100), damit Tags/Positionen final sind.
/// </summary>
[DefaultExecutionOrder(1100)]
public class RawMotionLogger : MonoBehaviour
{
    [Header("--- Armkette (Welt-Positionen) ---")]
    [Tooltip("mixamorig:RightArm  (Schultergelenk)")]
    public Transform shoulder;
    [Tooltip("mixamorig:RightForeArm  (Ellbogen)")]
    public Transform elbow;
    [Tooltip("mixamorig:RightHand  (Handgelenk)")]
    public Transform wrist;
    [Tooltip("HoldPoint oder Zylinder (Greifpunkt, Referenz)")]
    public Transform grip;

    [Header("--- Sync mit MeasurementController ---")]
    [Tooltip("Fuer currentTarget. Gleiche Mess-Taste wie dort verwenden.")]
    public MeasurementController measurement;
    public KeyCode logKey = KeyCode.Space;   // markiert den Mess-Instant
    public KeyCode resetKey = KeyCode.R;      // startet ein neues Trial-Segment

    [Header("--- Optional: konvertierte Quaternionen ---")]
    public bool logQuaternions = true;
    public UDPServer streamController;
    [Tooltip("strapId Oberarm")] public int upperArmIndex = 4;
    [Tooltip("strapId Unterarm")] public int foreArmIndex = 3;

    [Header("--- Optionen ---")]
    [Tooltip("true = ganzer Stream pro Frame. false = nur der Mess-Instant (1 Zeile/Trial).")]
    public bool logContinuously = true;
    public string customCsvPath = "";

    private StreamWriter writer;
    private int segmentId = 1;
    private float flushTimer = 0f;

    void Start()
    {
        string path = string.IsNullOrEmpty(customCsvPath)
            ? Path.Combine(Application.persistentDataPath, "raw_motion.csv")
            : customCsvPath;

        bool existed = File.Exists(path);
        try
        {
            writer = new StreamWriter(path, append: true, Encoding.UTF8) { AutoFlush = false };
        }
        catch (Exception e)
        {
            Debug.LogError($"[RawMotion] Datei konnte nicht geoeffnet werden: {e.Message}");
            enabled = false;
            return;
        }

        if (!existed) writer.WriteLine(BuildHeader());
        writer.Flush();
        Debug.Log($"[RawMotion] CSV: {path}");
    }

    private string BuildHeader()
    {
        string h = "time,segment,target,is_measurement," +
                   "sh_x,sh_y,sh_z,el_x,el_y,el_z,wr_x,wr_y,wr_z,grip_x,grip_y,grip_z";
        if (logQuaternions)
            h += ",qUp_x,qUp_y,qUp_z,qUp_w,qFore_x,qFore_y,qFore_z,qFore_w";
        return h;
    }

    void Update()
    {
        // Neues Trial-Segment bei Reset -> Reach + zugehoeriger Mess-Instant teilen sich eine ID.
        if (Input.GetKeyDown(resetKey)) segmentId++;
    }

    void LateUpdate()
    {
        if (writer == null) return;

        bool measurementNow = Input.GetKeyDown(logKey);

        // Nur-Instant-Modus: nur beim Mess-Instant schreiben.
        if (!logContinuously && !measurementNow) { PeriodicFlush(); return; }

        WriteRow(measurementNow);

        if (measurementNow) writer.Flush(); // Mess-Zeile sofort sichern
        PeriodicFlush();
    }

    private void WriteRow(bool measurementNow)
    {
        var ci = CultureInfo.InvariantCulture;
        int target = measurement != null ? measurement.currentTarget + 1 : -1;

        var sb = new StringBuilder(160);
        sb.Append(Time.timeAsDouble.ToString("F4", ci)).Append(',');
        sb.Append(segmentId.ToString(ci)).Append(',');
        sb.Append(target.ToString(ci)).Append(',');
        sb.Append(measurementNow ? "1" : "0");

        AppendPos(sb, shoulder, ci);
        AppendPos(sb, elbow, ci);
        AppendPos(sb, wrist, ci);
        AppendPos(sb, grip, ci);

        if (logQuaternions)
        {
            AppendQuat(sb, GetConvertedQuat(upperArmIndex), ci);
            AppendQuat(sb, GetConvertedQuat(foreArmIndex), ci);
        }

        writer.WriteLine(sb.ToString());
    }

    private static void AppendPos(StringBuilder sb, Transform t, IFormatProvider ci)
    {
        if (t == null) { sb.Append(",,,"); return; }
        Vector3 p = t.position;
        sb.Append(',').Append(p.x.ToString("F5", ci))
          .Append(',').Append(p.y.ToString("F5", ci))
          .Append(',').Append(p.z.ToString("F5", ci));
    }

    private static void AppendQuat(StringBuilder sb, Quaternion? q, IFormatProvider ci)
    {
        if (q == null) { sb.Append(",,,,"); return; }
        Quaternion v = q.Value;
        sb.Append(',').Append(v.x.ToString("F6", ci))
          .Append(',').Append(v.y.ToString("F6", ci))
          .Append(',').Append(v.z.ToString("F6", ci))
          .Append(',').Append(v.w.ToString("F6", ci));
    }

    // Gleiche Konvertierung wie im StreamSensorRotationController
    private Quaternion? GetConvertedQuat(int index)
    {
        if (streamController == null || streamController.SensorsMap == null) return null;
        if (!streamController.SensorsMap.ContainsKey(index)) return null;

        Quaternion raw = streamController.SensorsMap[index].Quaternion;
        Quaternion converted = new Quaternion(-raw.y, raw.x, raw.z, raw.w);
        Quaternion mountingOffset = Quaternion.Euler(0f, -90f, 180f);
        return converted * mountingOffset;
    }

    private void PeriodicFlush()
    {
        flushTimer += Time.unscaledDeltaTime;
        if (flushTimer >= 1.0f) { writer.Flush(); flushTimer = 0f; }
    }

    void OnApplicationQuit() { CloseWriter(); }
    void OnDestroy() { CloseWriter(); }

    private void CloseWriter()
    {
        if (writer != null)
        {
            try { writer.Flush(); writer.Close(); } catch { }
            writer = null;
        }
    }
}