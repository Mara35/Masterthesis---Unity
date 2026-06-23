using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// Transport & latency measurement harness.
///
/// Decoupled design so high-rate reception is never stalled:
///   - rxClient   (port, default 9001): receives sensor data ONLY, exclusive bind.
///   - pingClient (ephemeral): sends RTT pings, receives echoes.
///   - Data path and ping/echo path use SEPARATE locks, so they never block each other.
///   - CSV writers are buffered (AutoFlush off), written outside any lock by a single
///     thread each, and flushed once at shutdown (after the threads are joined).
///   - Large socket receive buffer to absorb bursts.
///
/// Run INSTEAD of the game. Output (Application.persistentDataPath):
///   packets_<stamp>.csv : t_ms, device, seq, size, interarrival_ms
///   pings_<stamp>.csv   : t_send_ms, t_recv_ms, rtt_ms, device
/// </summary>
public class UdpMeasurementHarness : MonoBehaviour
{
    [Header("Network")]
    public int port = 9001;

    [Header("RTT pings")]
    public float pingIntervalSeconds = 0.1f;
    public bool sendPings = true;

    [Header("Output")]
    [Tooltip("Leave empty to use Application.persistentDataPath")]
    public string outputFolder = "";

    [Header("Live summary")]
    public float summaryIntervalSeconds = 2f;

    static string DeviceLabel(byte id)
    {
        switch (id)
        {
            case 20: return "Glove";
            case 3:  return "IMU_Forearm";
            case 4:  return "IMU_UpperArm";
            default: return "id" + id;
        }
    }

    UdpClient rxClient;
    UdpClient pingClient;
    Thread rxThread, echoThread;
    volatile bool running;

    // Separate locks: data path and ping path never contend with each other.
    readonly object lockStats = new object();   // guards stats + deviceEndpoints
    readonly object lockPing  = new object();   // guards pingSent + pingDevice + rttAgg

    StreamWriter pktWriter;    // written only by rxThread
    StreamWriter pingWriter;   // written only by echoThread

    uint nextPingId = 0;       // only touched on the Update thread
    readonly Dictionary<uint, double> pingSent = new Dictionary<uint, double>();
    readonly Dictionary<uint, byte> pingDevice = new Dictionary<uint, byte>();
    readonly Dictionary<byte, IPEndPoint> deviceEndpoints = new Dictionary<byte, IPEndPoint>();
    readonly HashSet<string> seenStreams = new HashSet<string>();

    class DevStat
    {
        public long expectedSeq = -1;
        public long received = 0;
        public long lost = 0;
        public double lastArrival = -1;
        public double lastInter = 0;
        public double jitter = 0;
        public int srcPort = 0;
        public string srcIp = null;
        public bool duplicateId = false;
    }
    readonly Dictionary<byte, DevStat> stats = new Dictionary<byte, DevStat>();
    readonly Dictionary<byte, (double sum, long n, double min, double max)> rttAgg
        = new Dictionary<byte, (double, long, double, double)>();

    readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
    double Now => clock.Elapsed.TotalMilliseconds;

    static void DisableConnReset(UdpClient c)
    {
        try
        {
            const int SIO_UDP_CONNRESET = -1744830452;
            c.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
        }
        catch { }
    }

    void Start()
    {
        clock.Start();

        string folder = string.IsNullOrEmpty(outputFolder) ? Application.persistentDataPath : outputFolder;
        Directory.CreateDirectory(folder);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // Buffered writers (NO AutoFlush): writing is a cheap in-memory append; the
        // buffer flushes to disk on its own when full, and we Flush() once at shutdown.
        pktWriter  = new StreamWriter(Path.Combine(folder, $"packets_{stamp}.csv")) { AutoFlush = false };
        pingWriter = new StreamWriter(Path.Combine(folder, $"pings_{stamp}.csv")) { AutoFlush = false };
        pktWriter.WriteLine("t_ms,device,seq,size,interarrival_ms");
        pingWriter.WriteLine("t_send_ms,t_recv_ms,rtt_ms,device");
        Debug.Log($"[Harness] Logging to: {folder}");

        running = true;

        try
        {
            rxClient = new UdpClient();
            rxClient.ExclusiveAddressUse = true;
            rxClient.Client.ReceiveBufferSize = 1 << 20;   // 1 MB, absorbs bursts
            rxClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            rxClient.EnableBroadcast = true;
            DisableConnReset(rxClient);
            Debug.Log($"[Harness] BIND OK – receiving data on port {port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Harness] KANN PORT {port} NICHT BINDEN: {e.Message}\n" +
                           "Ein anderer Socket haelt den Port noch. Unity komplett schliessen und neu oeffnen.");
            running = false;
            return;
        }

        rxThread = new Thread(RxLoopData) { IsBackground = true };
        rxThread.Start();

        if (sendPings)
        {
            pingClient = new UdpClient(0);
            pingClient.Client.ReceiveBufferSize = 1 << 18;
            DisableConnReset(pingClient);
            echoThread = new Thread(RxLoopEcho) { IsBackground = true };
            echoThread.Start();
            Debug.Log($"[Harness] pinging from port {((IPEndPoint)pingClient.Client.LocalEndPoint).Port}");
        }
    }

    float pingTimer = 0f;
    float summaryTimer = 0f;

    void Update()
    {
        if (sendPings && pingClient != null)
        {
            pingTimer += Time.unscaledDeltaTime;
            if (pingTimer >= pingIntervalSeconds) { pingTimer = 0f; SendPings(); }
        }

        summaryTimer += Time.unscaledDeltaTime;
        if (summaryTimer >= summaryIntervalSeconds) { summaryTimer = 0f; PrintSummary(); }
    }

    void SendPings()
    {
        // snapshot targets under lockStats, then send without holding it
        List<KeyValuePair<byte, IPEndPoint>> targets;
        lock (lockStats) { targets = deviceEndpoints.ToList(); }

        foreach (var kv in targets)
        {
            byte dev = kv.Key;
            IPEndPoint ep = kv.Value;
            uint id = nextPingId++;
            byte[] p = new byte[8];
            p[0] = 0xFE; p[1] = 0xEE; p[2] = 0xFE; p[3] = 0xEE;
            BitConverter.GetBytes(id).CopyTo(p, 4);
            lock (lockPing) { pingSent[id] = Now; pingDevice[id] = dev; }
            try { pingClient.Send(p, p.Length, ep); } catch { }
        }
    }

    void RxLoopData()
    {
        while (running)
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] data;
            try { data = rxClient.Receive(ref remote); }
            catch { if (!running) break; else continue; }
            double t = Now;
            try { ProcessData(data, remote, t); }
            catch { }
        }
    }

    void RxLoopEcho()
    {
        while (running)
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] data;
            try { data = pingClient.Receive(ref remote); }
            catch { if (!running) break; else continue; }
            double t = Now;
            try { ProcessEcho(data, t); }
            catch { }
        }
    }

    void ProcessEcho(byte[] d, double t)
    {
        if (d.Length < 8) return;
        if (!(d[0] == 0xFE && d[1] == 0xEE && d[2] == 0xFE && d[3] == 0xEE)) return;
        uint id = BitConverter.ToUInt32(d, 4);

        bool matched = false; byte dev = 0; double ts = 0, rtt = 0;
        lock (lockPing)
        {
            if (pingSent.TryGetValue(id, out ts))
            {
                pingDevice.TryGetValue(id, out dev);
                rtt = t - ts;
                pingSent.Remove(id);
                pingDevice.Remove(id);
                if (!rttAgg.TryGetValue(dev, out var a)) a = (0, 0, double.MaxValue, double.MinValue);
                a.sum += rtt; a.n += 1;
                if (rtt < a.min) a.min = rtt;
                if (rtt > a.max) a.max = rtt;
                rttAgg[dev] = a;
                matched = true;
            }
        }
        // disk write OUTSIDE the lock; pingWriter is only touched by this thread
        if (matched) pingWriter.WriteLine($"{ts:F3},{t:F3},{rtt:F3},{DeviceLabel(dev)}");
    }

    void ProcessData(byte[] d, IPEndPoint remote, double t)
    {
        if (d.Length < 8) return;
        if (!(d[0] == 0xFF && d[2] == 0xFF)) return;
        byte id2 = d[3];
        uint seq = BitConverter.ToUInt32(d, d.Length - 4);
        string ip = remote.Address.ToString();
        int rport = remote.Port;

        bool isNew = false;
        double inter = 0;
        lock (lockStats)
        {
            deviceEndpoints[id2] = new IPEndPoint(remote.Address, rport);

            string streamKey = $"{DeviceLabel(id2)} @ {ip}:{rport}";
            isNew = seenStreams.Add(streamKey);

            if (!stats.TryGetValue(id2, out var s)) { s = new DevStat(); stats[id2] = s; }
            if (s.srcIp != null && s.srcIp != ip) s.duplicateId = true;
            s.srcIp = ip;
            s.srcPort = rport;

            inter = (s.lastArrival < 0) ? 0 : (t - s.lastArrival);
            if (s.expectedSeq >= 0)
            {
                long gap = (long)seq - s.expectedSeq;
                if (gap > 0) s.lost += gap;
            }
            s.expectedSeq = (long)seq + 1;
            s.received++;
            if (s.lastArrival >= 0)
            {
                double dabs = Math.Abs(inter - s.lastInter);
                s.jitter += (dabs - s.jitter) / 16.0;
            }
            s.lastInter = inter;
            s.lastArrival = t;
        }

        // disk write OUTSIDE the lock; pktWriter is only touched by this thread
        pktWriter.WriteLine($"{t:F3},{DeviceLabel(id2)},{seq},{d.Length},{inter:F3}");
        if (isNew) Debug.Log($"[Harness] NEW STREAM: {DeviceLabel(id2)} @ {ip}:{rport}  (id={id2})");
    }

    void PrintSummary()
    {
        // snapshot under each lock, then log without holding locks
        var snap = new List<(byte id, long recv, long lost, double jitter, int port, string ip, bool dup, double rttMean)>();
        lock (lockStats)
        {
            foreach (var kv in stats)
            {
                var s = kv.Value;
                snap.Add((kv.Key, s.received, s.lost, s.jitter, s.srcPort, s.srcIp, s.duplicateId, 0));
            }
        }
        if (snap.Count == 0) { Debug.Log("[Harness] (noch keine Daten)"); return; }

        for (int i = 0; i < snap.Count; i++)
        {
            double rttMean = 0, rttMin = 0;
            lock (lockPing)
            {
                if (rttAgg.TryGetValue(snap[i].id, out var a) && a.n > 0)
                {
                    rttMean = a.sum / a.n;
                    rttMin = a.min;
                }
            }
            var e = snap[i];
            long expected = e.recv + e.lost;
            double lossPct = expected > 0 ? 100.0 * e.lost / expected : 0;
            string warn = e.dup ? "  <<< SAME ID FROM 2 DEVICES!" : "";
            Debug.Log($"[Harness] {DeviceLabel(e.id),-13} (port {e.port}, ip {e.ip})  " +
                      $"recv={e.recv,-6} lost={e.lost,-5} loss={lossPct,5:F2}%  " +
                      $"jitter={e.jitter,5:F2}ms  rttMin={rttMin,6:F1}ms  rttMean={rttMean,7:F1}ms{warn}");
        }
    }

    void OnDestroy() { Shutdown(); }
    void OnApplicationQuit() { Shutdown(); }

    void Shutdown()
    {
        if (!running) { TryCloseWriters(); return; }
        running = false;
        try { rxClient?.Close(); } catch { }
        try { pingClient?.Close(); } catch { }
        try { rxThread?.Join(500); } catch { }
        try { echoThread?.Join(500); } catch { }
        TryCloseWriters();
        Debug.Log("[Harness] Stopped, files closed.");
    }

    bool writersClosed = false;
    void TryCloseWriters()
    {
        if (writersClosed) return;
        writersClosed = true;
        try { pktWriter?.Flush(); pktWriter?.Close(); } catch { }
        try { pingWriter?.Flush(); pingWriter?.Close(); } catch { }
    }
}