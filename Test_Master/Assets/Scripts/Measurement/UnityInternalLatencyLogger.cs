using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Logs the Unity-internal latency: time from UDP socket receive (in UDPServer's
/// background thread) until the bone is set and ready to render.
///
/// Runs IN THE GAME SCENE alongside the avatar and the apply scripts
/// (StreamSensorRotationController / GloveController). It reads the per-device
/// receive timestamp that UDPServer records, and measures in LateUpdate() -- i.e.
/// after the apply scripts' Update() has run this frame, so the bone already
/// reflects the newest packet.
///
/// What it captures: receive -> (wait for next frame, inherent in poll-based apply)
/// -> bone set. It does NOT include the Quest Link / display latency (motion-to-photon)
/// -- that part is separate and out of scope.
///
/// Setup: attach to any GameObject in the game scene, assign the UDPServer reference,
/// press Play, perform the same movements as in a real training session.
/// Output: unity_internal_<stamp>.csv  (t_ms, device, recv_to_render_ms)
///
/// NOTE: requires the ESPs to run the MEAS firmware AND UDPServer's StreamReceiveMessageTypes
/// to accept the 4-byte-longer packets (length check must be >=, not ==).
/// </summary>
public class UnityInternalLatencyLogger : MonoBehaviour
{
    [SerializeField] private UDPServer udpServer;
    [SerializeField] private int[] sensorIds = { 3, 4 };   // 3 = upper arm, 4 = forearm
    [SerializeField] private int gloveId = 20;
    [Tooltip("Leave empty to use Application.persistentDataPath")]
    [SerializeField] private string outputFolder = "";

    StreamWriter writer;
    readonly Dictionary<int, double> lastSensorRecv = new();
    readonly Dictionary<int, double> lastGloveRecv = new();

    void Start()
    {
        if (udpServer == null) udpServer = FindFirstObjectByType<UDPServer>();

        string folder = string.IsNullOrEmpty(outputFolder) ? Application.persistentDataPath : outputFolder;
        Directory.CreateDirectory(folder);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        writer = new StreamWriter(Path.Combine(folder, $"unity_internal_{stamp}.csv")) { AutoFlush = true };
        writer.WriteLine("t_ms,device,recv_to_render_ms");
        Debug.Log($"[UnityInternalLatency] Logging to {folder}");
    }

    void LateUpdate()
    {
        if (udpServer == null || writer == null) return;
        double now = UDPServer.NowMs();

        foreach (int id in sensorIds)
        {
            if (udpServer.TryGetSensorRecvMs(id, out double recv))
            {
                if (!lastSensorRecv.TryGetValue(id, out double prev) || recv != prev)
                {
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0:F3},{1},{2:F3}", now, Label(id), now - recv));
                    lastSensorRecv[id] = recv;
                }
            }
        }

        if (udpServer.TryGetGloveRecvMs(gloveId, out double grecv))
        {
            if (!lastGloveRecv.TryGetValue(gloveId, out double gprev) || grecv != gprev)
            {
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0:F3},Glove,{1:F3}", now, now - grecv));
                lastGloveRecv[gloveId] = grecv;
            }
        }
    }

    static string Label(int id) => id == 3 ? "IMU_UpperArm" : id == 4 ? "IMU_Forearm" : $"Sensor_{id}";

    void OnApplicationQuit()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }
    }
}