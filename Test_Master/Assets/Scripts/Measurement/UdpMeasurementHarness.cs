using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// Standalone transport & latency measurement harness for the SensinGlove + IMU pipeline.
///
/// Run this INSTEAD of the game during a measurement session (it binds UDP 9001, so you
/// cannot run both at once). It does NOT depend on any other project script.
///
/// What it measures (all timing on ONE clock = this PC's high-res Stopwatch):
///   - Packet loss & jitter, per device, from the appended uint32 sequence counter.
///   - Round-trip time (RTT) per device, by sending FE EE FE EE ping packets that the
///     ESPs echo back. One-way latency is estimated as RTT/2 in the analysis script.
///
/// Output: two CSV files in Application.persistentDataPath (path is logged on Start).
///   packets_<stamp>.csv : t_ms, device, seq, size, interarrival_ms
///   pings_<stamp>.csv   : t_send_ms, t_recv_ms, rtt_ms, device
///
/// Setup: create an empty scene, add an empty GameObject, attach this component, press Play.
/// </summary>
public class UdpMeasurementHarness : MonoBehaviour
{
    [Header("Network")]
    public int port = 9001;

    [Header("RTT pings")]
    public float pingIntervalSeconds = 0.1f;   // 10 Hz per device
    public bool sendPings = true;

    [Header("Output")]
    [Tooltip("Leave empty to use Application.persistentDataPath")]
    public string outputFolder = "";

    [Header("Live summary")]
    public float summaryIntervalSeconds = 2f;

    // --- device labels (from firmware ids) ---
    static string DeviceLabel(byte id)
    {
        switch (id)
        {
            case 20: return "Glove";
            case 3: return "IMU_Forearm";
            case 4: return "IMU_UpperArm";
            default: return "id" + id;
        }
    }

    UdpClient udp;
    Thread rxThread;
    volatile bool running;
    readonly object gate = new object();

    StreamWriter pktWriter;
    StreamWriter pingWriter;

    // ping bookkeeping
    uint nextPingId = 0;
    readonly Dictionary<uint, double> pingSent = new Dictionary<uint, double>();
    readonly Dictionary<uint, byte> pingDevice = new Dictionary<uint, byte>();
    readonly Dictionary<byte, IPEndPoint> deviceEndpoints = new Dictionary<byte, IPEndPoint>();

    class DevStat
    {
        public long expectedSeq = -1;
        public long received = 0;
        public long lost = 0;
        public double lastArrival = -1;
        public double lastInter = 0;
        public double jitter = 0;     // RFC3550-style running estimate of interarrival jitter
    }
    readonly Dictionary<byte, DevStat> stats = new Dictionary<byte, DevStat>();

    // RTT running summary
    readonly Dictionary<byte, (double sum, long n, double min, double max)> rttAgg
        = new Dictionary<byte, (double, long, double, double)>();

    readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
    double Now => clock.Elapsed.TotalMilliseconds;

    void Start()
    {
        clock.Start();

        string folder = string.IsNullOrEmpty(outputFolder) ? Application.persistentDataPath : outputFolder;
        Directory.CreateDirectory(folder);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        pktWriter = new StreamWriter(Path.Combine(folder, $"packets_{stamp}.csv"));
        pingWriter = new StreamWriter(Path.Combine(folder, $"pings_{stamp}.csv"));
        pktWriter.WriteLine("t_ms,device,seq,size,interarrival_ms");
        pingWriter.WriteLine("t_send_ms,t_recv_ms,rtt_ms,device");
        pktWriter.AutoFlush = true;
        pingWriter.AutoFlush = true;

        Debug.Log($"[Harness] Logging to: {folder}");

        udp = new UdpClient(port);
        running = true;
        rxThread = new Thread(RxLoop) { IsBackground = true };
        rxThread.Start();
    }

    float pingTimer = 0f;
    float summaryTimer = 0f;

    void Update()
    {
        if (sendPings)
        {
            pingTimer += Time.unscaledDeltaTime;
            if (pingTimer >= pingIntervalSeconds) { pingTimer = 0f; SendPings(); }
        }

        summaryTimer += Time.unscaledDeltaTime;
        if (summaryTimer >= summaryIntervalSeconds) { summaryTimer = 0f; PrintSummary(); }
    }

    void SendPings()
    {
        lock (gate)
        {
            foreach (var kv in deviceEndpoints)
            {
                byte dev = kv.Key;
                IPEndPoint ep = kv.Value;
                uint id = nextPingId++;
                byte[] p = new byte[8];
                p[0] = 0xFE; p[1] = 0xEE; p[2] = 0xFE; p[3] = 0xEE;
                BitConverter.GetBytes(id).CopyTo(p, 4);
                pingSent[id] = Now;
                pingDevice[id] = dev;
                try { udp.Send(p, p.Length, ep); } catch { /* device may be transiently gone */ }
            }
        }
    }

    void RxLoop()
    {
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            byte[] data;
            try { data = udp.Receive(ref remote); }
            catch { if (!running) break; else continue; }
            double t = Now;
            ProcessPacket(data, remote, t);
        }
    }

    void ProcessPacket(byte[] d, IPEndPoint remote, double t)
    {
        if (d.Length < 4) return;

        // --- ping echo? ---
        if (d.Length >= 8 && d[0] == 0xFE && d[1] == 0xEE && d[2] == 0xFE && d[3] == 0xEE)
        {
            uint id = BitConverter.ToUInt32(d, 4);
            lock (gate)
            {
                if (pingSent.TryGetValue(id, out double ts))
                {
                    pingDevice.TryGetValue(id, out byte dev);
                    double rtt = t - ts;
                    pingWriter.WriteLine($"{ts:F3},{t:F3},{rtt:F3},{DeviceLabel(dev)}");
                    pingSent.Remove(id);
                    pingDevice.Remove(id);

                    if (!rttAgg.TryGetValue(dev, out var a)) a = (0, 0, double.MaxValue, double.MinValue);
                    a.sum += rtt; a.n += 1;
                    if (rtt < a.min) a.min = rtt;
                    if (rtt > a.max) a.max = rtt;
                    rttAgg[dev] = a;
                }
            }
            return;
        }

        // --- data packet: header FF xx FF id, seq = last 4 bytes ---
        if (!(d[0] == 0xFF && d[2] == 0xFF)) return;
        if (d.Length < 8) return;
        byte id2 = d[3];
        uint seq = BitConverter.ToUInt32(d, d.Length - 4);

        lock (gate)
        {
            deviceEndpoints[id2] = new IPEndPoint(remote.Address, remote.Port);

            if (!stats.TryGetValue(id2, out var s)) { s = new DevStat(); stats[id2] = s; }

            double inter = (s.lastArrival < 0) ? 0 : (t - s.lastArrival);

            if (s.expectedSeq >= 0)
            {
                long gap = (long)seq - s.expectedSeq;
                if (gap > 0) s.lost += gap;   // wrap-around after 2^32 packets is not a concern here
            }
            s.expectedSeq = (long)seq + 1;
            s.received++;

            if (s.lastArrival >= 0)
            {
                double dabs = Math.Abs(inter - s.lastInter);
                s.jitter += (dabs - s.jitter) / 16.0;   // RFC3550 interarrival jitter
            }
            s.lastInter = inter;
            s.lastArrival = t;

            pktWriter.WriteLine($"{t:F3},{DeviceLabel(id2)},{seq},{d.Length},{inter:F3}");
        }
    }

    void PrintSummary()
    {
        lock (gate)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[Harness] --- live summary ---");
            foreach (var kv in stats)
            {
                var s = kv.Value;
                long expected = s.received + s.lost;
                double lossPct = expected > 0 ? 100.0 * s.lost / expected : 0;
                double rttMean = 0;
                if (rttAgg.TryGetValue(kv.Key, out var a) && a.n > 0) rttMean = a.sum / a.n;
                sb.AppendLine($"  {DeviceLabel(kv.Key),-14} recv={s.received,-7} lost={s.lost,-5} " +
                              $"loss={lossPct,5:F2}%  jitter={s.jitter,5:F2}ms  rttMean={rttMean,6:F2}ms");
            }
            Debug.Log(sb.ToString());
        }
    }

    void OnDestroy() { Shutdown(); }
    void OnApplicationQuit() { Shutdown(); }

    void Shutdown()
    {
        if (!running) return;
        running = false;
        try { udp?.Close(); } catch { }
        try { rxThread?.Join(500); } catch { }
        lock (gate)
        {
            try { pktWriter?.Flush(); pktWriter?.Close(); } catch { }
            try { pingWriter?.Flush(); pingWriter?.Close(); } catch { }
        }
        Debug.Log("[Harness] Stopped, files closed.");
    }
}