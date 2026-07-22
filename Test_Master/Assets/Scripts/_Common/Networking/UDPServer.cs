using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using STREAM;
using UnityEngine;

/// <summary>
/// Central UDP receiver for all sensor streams. Opens one listener socket per entry in
/// <see cref="listenPorts"/>, runs a dedicated background thread per socket, and decodes
/// incoming IMU-quaternion and glove packets into the thread-safe <see cref="SensorsMap"/>
/// and <see cref="GloveMap"/>. A monotonic receive timestamp is recorded per device for
/// latency measurement, and vibration commands can be sent back to a sensor.
/// </summary>
public class UDPServer : MonoBehaviour
{
    [SerializeField] private List<int> listenPorts = new List<int> { 9001 };

    private List<UdpClient> _udpClients = new List<UdpClient>();
    private List<Thread> _readThreads = new List<Thread>();

    // Latest decoded state per device. Written by the receive threads, read by the main thread,
    // so every access must go through the lock on the respective map.
    public readonly Dictionary<int, StreamSensor> SensorsMap = new();
    public readonly Dictionary<int, GloveSensorData> GloveMap = new();

    // Receive timestamp (ms) per device, set on the receive thread. Used for latency measurement.
    public readonly Dictionary<int, double> SensorRecvMs = new();
    public readonly Dictionary<int, double> GloveRecvMs = new();

    // Shared monotonic clock for the receive thread and the main thread. Not wall-clock time;
    // only differences between two readings are meaningful.
    public static double NowMs() =>
        System.Diagnostics.Stopwatch.GetTimestamp() * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

    private bool _reading = false;

    private void OnEnable()
    {
        CleanUp();
        _reading = true;

        // Open one UDP socket and one background receive thread per configured port.
        foreach (int port in listenPorts)
        {
            try
            {
                var client = new UdpClient(port);
                _udpClients.Add(client);

                Thread thread = new Thread(() => ThreadFunction(client, port));
                thread.IsBackground = true;
                thread.Start();
                _readThreads.Add(thread);

                Debug.Log($"UDP server is successfully waiting on port: {port}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error opening port {port}: {e.Message}");
            }
        }
    }

    private void OnDisable()
    {
        CleanUp();
    }

    // Stops the receive loop, interrupts the threads (to break the blocking Receive call) and
    // closes the sockets. Safe to call repeatedly.
    private void CleanUp()
    {
        _reading = false;

        foreach (var thread in _readThreads)
        {
            if (thread is { IsAlive: true })
            {
                thread.Interrupt();
            }
        }
        _readThreads.Clear();

        foreach (var client in _udpClients)
        {
            client?.Close();
        }
        _udpClients.Clear();
    }

    // Runs on a background thread: blocks on Receive, classifies each packet by its header and
    // writes the decoded values into the shared maps.
    private void ThreadFunction(UdpClient client, int localPort)
    {
        try
        {
            while (_reading)
            {
                var clientEndpoint = new IPEndPoint(IPAddress.Any, 0);
                var receivedBytes = client.Receive(ref clientEndpoint);   // blocks until a packet arrives

                if (receivedBytes == null || receivedBytes.Length == 0)
                    continue;

                double recvMs = NowMs();   // stamp the receive time immediately, before decoding

                var receivedList = receivedBytes.ToList();

                // -------- IMU quaternion --------
                if (StreamReceiveMessageTypes.Matches(StreamReceiveMessageType.Quaternion, receivedList))
                {
                    var (nr, quat) = StreamReceiveMessageTypes.DecodeQuaternionData(receivedBytes);

                    // Lock while touching SensorsMap: the main thread reads it concurrently.
                    lock (SensorsMap)
                    {
                        if (!SensorsMap.ContainsKey(nr))
                        {
                            SensorsMap[nr] = new StreamSensor
                            {
                                Id = (byte)nr,
                            };
                        }

                        SensorsMap[nr].IpAddress = clientEndpoint.Address;
                        SensorsMap[nr].Quaternion = quat;

                        SensorRecvMs[nr] = recvMs;
                    }
                    continue;
                }

                // -------- Glove data --------
                if (StreamReceiveMessageTypes.Matches(StreamReceiveMessageType.Glove, receivedList))
                {
                    var gloveData = StreamReceiveMessageTypes.DecodeGloveData(receivedBytes);

                    // Lock while touching GloveMap: the main thread reads it concurrently.
                    lock (GloveMap)
                    {
                        if (!GloveMap.ContainsKey(gloveData.Id))
                        {
                            GloveMap[gloveData.Id] = new GloveSensorData
                            {
                                Id = gloveData.Id
                            };
                        }

                        GloveMap[gloveData.Id].IpAddress = clientEndpoint.Address;

                        GloveMap[gloveData.Id].Thumb_MCP = gloveData.Thumb_MCP;
                        GloveMap[gloveData.Id].Thumb_PIP = gloveData.Thumb_PIP;
                        GloveMap[gloveData.Id].Index_MCP = gloveData.Index_MCP;
                        GloveMap[gloveData.Id].Index_PIP = gloveData.Index_PIP;
                        GloveMap[gloveData.Id].Middle_MCP = gloveData.Middle_MCP;
                        GloveMap[gloveData.Id].Middle_PIP = gloveData.Middle_PIP;
                        GloveMap[gloveData.Id].Ring_MCP = gloveData.Ring_MCP;
                        GloveMap[gloveData.Id].Ring_PIP = gloveData.Ring_PIP;
                        GloveMap[gloveData.Id].Pinky_MCP = gloveData.Pinky_MCP;
                        GloveMap[gloveData.Id].Pinky_PIP = gloveData.Pinky_PIP;

                        GloveRecvMs[gloveData.Id] = recvMs;
                    }
                    continue;
                }

                Debug.LogWarning($"Unknown UDP packet received on port {localPort}. Length: {receivedBytes.Length}");
            }
        }
        catch (ThreadInterruptedException) { }   // expected on shutdown (CleanUp interrupts the thread)
        catch (SocketException) { }              // expected when the socket is closed during shutdown
        catch (Exception e)
        {
            Debug.LogError($"UDPServer error on port {localPort}: {e.Message}");
        }
    }

    private void OnApplicationQuit()
    {
        CleanUp();
    }

    // Sends a single vibration command to the sensor with the given id, using the IP the sensor
    // last sent from. The reply always goes to port 9001 regardless of the receiving port.
    public void SendVibrateOne(VibrationRequest vibrationRequest)
    {
        if (!SensorsMap.TryGetValue(vibrationRequest.SensorId, out var sensor))
            return;

        var data = StreamSendMessages.CreateVibrateOneRequest(vibrationRequest);

        int targetPort = 9001;

        if (_udpClients.Count > 0)
        {
            _udpClients[0].Send(data, data.Length, new IPEndPoint(sensor.IpAddress, targetPort));
        }
    }

    // --- Thread-safe accessors for the main thread ---

    public bool TryGetSensor(int sensorId, out StreamSensor sensor)
    {
        lock (SensorsMap)
        {
            return SensorsMap.TryGetValue(sensorId, out sensor);
        }
    }

    public bool TryGetGlove(int gloveId, out GloveSensorData glove)
    {
        lock (GloveMap)
        {
            return GloveMap.TryGetValue(gloveId, out glove);
        }
    }

    // Read the last receive timestamp (used by UnityInternalLatencyLogger).
    public bool TryGetSensorRecvMs(int sensorId, out double ms)
    {
        lock (SensorsMap)
        {
            return SensorRecvMs.TryGetValue(sensorId, out ms);
        }
    }

    public bool TryGetGloveRecvMs(int gloveId, out double ms)
    {
        lock (GloveMap)
        {
            return GloveRecvMs.TryGetValue(gloveId, out ms);
        }
    }
}