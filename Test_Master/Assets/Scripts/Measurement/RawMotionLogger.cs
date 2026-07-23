using System;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Raw-motion logger for the body-size replay simulation.
///
/// Writes the world positions of the arm chain (shoulder, elbow, wrist, grip point)
/// every frame into a second CSV - tagged with the trial segment, the current target
/// and a marker for the measurement instant (space bar).
///
/// This lets the arm length be varied offline:
///   Hand' = Shoulder + L1'*dir(Shoulder->Elbow) + L2'*dir(Elbow->Wrist)
/// The directions are length-independent -> pure position data is enough.
///
/// Optionally the converted IMU quaternions are logged as well.
/// Runs AFTER MeasurementController (order 1100) so tags/positions are final.
/// </summary>
[DefaultExecutionOrder(1100)]
public class RawMotionLogger : MonoBehaviour
{
    [Header("--- Arm chain (world positions) ---")]
    [Tooltip("mixamorig:RightArm  (shoulder joint)")]
    public Transform shoulder;
    [Tooltip("mixamorig:RightForeArm  (elbow)")]
    public Transform elbow;
    [Tooltip("mixamorig:RightHand  (wrist)")]
    public Transform wrist;
    [Tooltip("HoldPoint or cylinder (grip point, reference)")]
    public Transform grip;

    [Header("--- Sync with MeasurementController ---")]
    [Tooltip("For currentTarget. Use the same measurement key as there.")]
    public MeasurementController measurement;
    public KeyCode logKey = KeyCode.Space;   // marks the measurement instant
    public KeyCode resetKey = KeyCode.R;      // starts a new trial segment

    [Header("--- Optional: converted quaternions ---")]
    public bool logQuaternions = true;
    public UDPServer streamController;
    // NOTE: elsewhere in the project strapId 3 = upper arm, strapId 4 = forearm.
    // These two defaults are the other way round - verify the mapping before trusting the quaternion columns.
    [Tooltip("strapId upper arm")] public int upperArmIndex = 3;
    [Tooltip("strapId forearm")] public int foreArmIndex = 4;

    [Header("--- Options ---")]
    [Tooltip("true = full stream per frame. false = measurement instant only (1 line/trial).")]
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
            Debug.LogError($"[RawMotion] Could not open file: {e.Message}");
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
        // New trial segment on reset -> the reach and its measurement instant share one ID.
        if (Input.GetKeyDown(resetKey)) segmentId++;
    }

    void LateUpdate()
    {
        if (writer == null) return;

        bool measurementNow = Input.GetKeyDown(logKey);

        // Instant-only mode: write only on the measurement instant.
        if (!logContinuously && !measurementNow) { PeriodicFlush(); return; }

        WriteRow(measurementNow);

        if (measurementNow) writer.Flush(); // persist the measurement line immediately
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

    // Same conversion as in StreamSensorRotationController
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